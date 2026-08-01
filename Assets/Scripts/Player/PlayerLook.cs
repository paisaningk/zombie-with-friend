using FishNet.Object;
using Game;
using UnityEngine;

namespace Player
{
    /// <summary>
    /// First-person look for the owning client, split along the network seam:
    ///   - YAW rotates the body (this transform / root) → synced to others via NetworkTransform.
    ///   - PITCH rotates <see cref="cameraHolder"/> locally, clamped ±<see cref="pitchClamp"/> →
    ///     camera-only, NOT synced (others never see where you look up/down).
    ///
    /// Only the owner reads input and drives the camera; it enables the per-player camera +
    /// audio listener (disabled on the prefab so non-owners stay dark) and locks the cursor.
    /// Kept intentionally independent of the Rigidbody: yaw is a direct transform rotation.
    /// </summary>
    public class PlayerLook : NetworkBehaviour
    {
        [Header("Refs")]
        [Tooltip("Pitch pivot at eye height. The owner Camera + AudioListener live under here.")]
        [SerializeField] private Transform cameraHolder;
        [SerializeField] private Camera ownerCamera;
        [SerializeField] private AudioListener ownerListener;

        [Header("Look (owner)")]
        [SerializeField] private float sensitivity = 0.1f; // degrees per mouse unit
        [SerializeField] private float pitchClamp = 80f;

        private InputSystem_Actions _input;
        private float _pitch;

        public override void OnStartClient()
        {
            base.OnStartClient();
            if (!IsOwner)
                return; // non-owner: camera/listener stay disabled (prefab default), no input

            if (ownerCamera != null) ownerCamera.enabled = true;
            if (ownerListener != null) ownerListener.enabled = true;

            // Cursor is NOT locked here: the player spawns into the staging screen (GameState.Lobby),
            // which needs a free cursor to click class/ready (decision 0013). Update locks it once the
            // match is live; StagingController owns the cursor while staging.
            _input = new InputSystem_Actions();
            _input.Player.Enable();
        }

        public override void OnStopClient()
        {
            base.OnStopClient();
            if (IsOwner)
            {
                Cursor.lockState = CursorLockMode.None;
                Cursor.visible = true;
            }
            if (_input != null)
            {
                _input.Dispose();
                _input = null;
            }
        }

        private void Update()
        {
            if (!IsOwner || _input == null) return;

            // Freeze look during the pre-match staging screen (and any non-live state). The cursor is
            // left free for the staging UI; StagingController manages it while GameState == Lobby.
            if (GameManager.Instance == null || GameManager.Instance.State != GameState.Playing)
                return;

            // Match is live → own the cursor (lock, idempotent) and drive the camera.
            if (Cursor.lockState != CursorLockMode.Locked)
            {
                Cursor.lockState = CursorLockMode.Locked;
                Cursor.visible = false;
            }

            Vector2 look = _input.Player.Look.ReadValue<Vector2>();

            // Yaw: rotate the body (synced via NetworkTransform on the root).
            if (!Mathf.Approximately(look.x, 0f))
                transform.Rotate(Vector3.up, look.x * sensitivity, Space.Self);

            // Pitch: rotate the camera holder locally, clamped (not synced). Mouse up = look up.
            _pitch = Mathf.Clamp(_pitch - look.y * sensitivity, -pitchClamp, pitchClamp);
            if (cameraHolder != null)
                cameraHolder.localRotation = Quaternion.Euler(_pitch, 0f, 0f);
        }
    }
}
