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
        [Tooltip("Body model hidden for the owner (first-person). Remote players still see it.")]
        [SerializeField] private GameObject bodyModel;

        [Header("Look (owner)")]
        [SerializeField] private float sensitivity = 0.1f; // degrees per mouse unit
        [SerializeField] private float pitchClamp = 80f;

        private InputSystem_Actions _input;
        private float _pitch;

        // Yaw is accumulated from input each frame and pushed to the Rigidbody in FixedUpdate.
        // Writing transform.rotation directly fights the Rigidbody's Interpolate mode (PlayerMovement
        // enables it): Unity re-drives the transform from the interpolated physics pose every rendered
        // frame, so a manual write gets reverted → the view snaps back. MoveRotation goes through the
        // physics pose instead, so interpolation smooths it rather than undoing it.
        private Rigidbody _rb;
        private float _yaw;

        private void Awake()
        {
            _rb = GetComponent<Rigidbody>();
        }

        public override void OnStartClient()
        {
            base.OnStartClient();
            if (!IsOwner)
                return; // non-owner: camera/listener stay disabled (prefab default), no input

            // Seed from the spawn rotation so the first FixedUpdate doesn't snap the player to yaw 0.
            _yaw = transform.eulerAngles.y;

            if (ownerCamera != null) ownerCamera.enabled = true;
            if (ownerListener != null) ownerListener.enabled = true;

            // First-person: hide the owner's own body model so it doesn't clip into the camera
            // (decision P2, task 16c). Non-owners returned above, so remote players still see the
            // full body. No viewmodel/hands (cut from MVP).
            if (bodyModel != null)
                foreach (Renderer r in bodyModel.GetComponentsInChildren<Renderer>(true))
                    r.enabled = false;

            // The cursor is NOT this component's concern — CursorController is the single owner of it
            // (decision 0014), derived from GameState. PlayerLook only reads look input while Playing.
            _input = new InputSystem_Actions();
            _input.Player.Enable();
        }

        public override void OnStopClient()
        {
            base.OnStopClient();
            if (_input != null)
            {
                _input.Dispose();
                _input = null;
            }
        }

        private void Update()
        {
            if (!IsOwner || _input == null) return;

            // Freeze look whenever the cursor is free (staging, the between-wave shop, result screen) so
            // moving the mouse to click UI doesn't swing the camera. The cursor itself is owned by
            // CursorController (decision 0014); this mirrors its rule and only gates look input.
            if (GameManager.Instance == null ||
                GameManager.Instance.State != GameState.Playing ||
                GameManager.Instance.ShopOpen)
                return;

            Vector2 look = _input.Player.Look.ReadValue<Vector2>();

            // Yaw: accumulate here (frame-rate input), applied to the body in FixedUpdate so the
            // Rigidbody's interpolation carries it instead of overwriting it. Synced via NetworkTransform.
            _yaw += look.x * sensitivity;

            // Pitch: rotate the camera holder locally, clamped (not synced). Mouse up = look up.
            // The holder is a child, not the Rigidbody, so physics never touches it — safe in Update.
            _pitch = Mathf.Clamp(_pitch - look.y * sensitivity, -pitchClamp, pitchClamp);
            if (cameraHolder != null)
                cameraHolder.localRotation = Quaternion.Euler(_pitch, 0f, 0f);

            // No Rigidbody (shouldn't happen on the player prefab): fall back to a direct write.
            if (_rb == null)
                transform.rotation = Quaternion.Euler(0f, _yaw, 0f);
        }

        private void FixedUpdate()
        {
            if (!IsOwner || _rb == null || _input == null) return;
            _rb.MoveRotation(Quaternion.Euler(0f, _yaw, 0f));
        }
    }
}
