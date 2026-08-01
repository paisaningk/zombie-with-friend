using FishNet.Object;
using UnityEngine;

namespace Player
{
    /// <summary>
    /// First-person locomotion for the owning client. WASD (Input System "Move") drives a
    /// Rigidbody velocity relative to the body's facing (yaw), which <see cref="PlayerLook"/>
    /// controls. Hold "Sprint" for a flat speed multiplier (no stamina — MVP). Gravity is left
    /// to the Rigidbody (only the XZ velocity is driven here).
    ///
    /// Owner-authoritative: only the owner simulates; NetworkTransform (client authoritative)
    /// syncs the result. Non-owners are made kinematic and just follow the synced transform.
    /// </summary>
    [RequireComponent(typeof(Rigidbody))]
    public class PlayerMovement : NetworkBehaviour
    {
        [Header("Move (owner)")]
        [SerializeField] private float maxSpeed = 7f;
        [SerializeField] private float accel = 28f;
        [SerializeField] private float decel = 40f;
        [SerializeField] private float sprintMultiplier = 1.6f;

        private Rigidbody _rb;
        private InputSystem_Actions _input;
        private Vector2 _moveInput;
        private bool _sprinting;

        private void Awake()
        {
            _rb = GetComponent<Rigidbody>();
            _rb.interpolation = RigidbodyInterpolation.Interpolate;
            _rb.collisionDetectionMode = CollisionDetectionMode.ContinuousDynamic;
            _rb.constraints = RigidbodyConstraints.FreezeRotation;
        }

        public override void OnStartClient()
        {
            base.OnStartClient();
            if (IsOwner)
            {
                _input = new InputSystem_Actions();
                _input.Player.Enable();
            }
            else
            {
                // Non-owner follows NetworkTransform; no local physics.
                _rb.isKinematic = true;
            }
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
            _moveInput = _input.Player.Move.ReadValue<Vector2>();
            _sprinting = _input.Player.Sprint.IsPressed();
        }

        private void FixedUpdate()
        {
            // Skip when kinematic: writing linearVelocity to a kinematic body is unsupported (Unity
            // warns). Normally the owner is dynamic while Alive, but a kinematic owner can occur
            // (e.g. parked/paused, or a test harness), and this keeps that path warning-free.
            if (!IsOwner || _input == null || _rb.isKinematic) return;

            // Wish direction relative to body facing (yaw set by PlayerLook).
            Vector3 wish = transform.forward * _moveInput.y + transform.right * _moveInput.x;
            wish.y = 0f;
            wish = Vector3.ClampMagnitude(wish, 1f);

            float speed = maxSpeed * (_sprinting ? sprintMultiplier : 1f);
            Vector3 v = _rb.linearVelocity;
            Vector3 vXZ = new Vector3(v.x, 0f, v.z);
            Vector3 target = wish * speed;
            float a = wish.sqrMagnitude > 0f ? accel : decel;

            Vector3 newXZ = Vector3.MoveTowards(vXZ, target, a * Time.fixedDeltaTime);
            _rb.linearVelocity = new Vector3(newXZ.x, v.y, newXZ.z); // keep gravity on Y
        }
    }
}
