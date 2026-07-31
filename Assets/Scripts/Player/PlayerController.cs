using FishNet.Object;
using UnityEngine;

namespace Player
{
    /// <summary>
    /// Thin lifecycle coordinator for the player. It owns NO gameplay logic — HP lives in
    /// PlayerHealth, locomotion in PlayerMovement, aiming in PlayerLook, firing in PlayerWeapon.
    /// It only REACTS to <see cref="PlayerState"/> transitions: gating the input-driven components,
    /// freezing the body when going down, and running the server-side bleed-out timer
    /// (Downed → Dead). Central hook for revive (task 7) / wave-clear (task 11): they just call
    /// <c>PlayerState.SetState(Alive)</c> and this re-enables everything + the bleed-out stops.
    ///
    /// Rule: Movement + Weapon are active ONLY while Alive (off in both Downed and Dead).
    /// PlayerLook is intentionally left alone — a downed/dead player can still look around.
    /// </summary>
    public class PlayerController : NetworkBehaviour
    {
        [Tooltip("Seconds a Downed player bleeds out before dying (server-authoritative).")]
        [SerializeField] private float _bleedOutSeconds = 30f;

        private PlayerState _state;
        private PlayerMovement _movement;
        private Combat.PlayerWeapon _weapon;
        private Rigidbody _rb;

        private float _bleedOutEnd;

        private void Awake()
        {
            _state = GetComponent<PlayerState>();
            _movement = GetComponent<PlayerMovement>();
            _weapon = GetComponent<Combat.PlayerWeapon>();
            _rb = GetComponent<Rigidbody>();

            if (_state != null)
                _state.OnStateChanged += HandleStateChanged;
            else
                Debug.LogWarning("[PlayerController] No PlayerState found; coordinator is inert.");
        }

        private void OnDestroy()
        {
            if (_state != null)
                _state.OnStateChanged -= HandleStateChanged;
        }

        // Fires once on start (prev == next == current) then once per real change, on every peer.
        private void HandleStateChanged(PlayerLifeState prev, PlayerLifeState next)
        {
            bool alive = next == PlayerLifeState.Alive;

            // Gate input-driven components. Look is deliberately untouched.
            if (_movement != null) _movement.enabled = alive;
            if (_weapon != null) _weapon.enabled = alive;

            // Freeze horizontal motion when going down so the body doesn't slide (owner simulates;
            // no-op for a kinematic non-owner). Gravity (y) is preserved so it settles on the floor.
            if (!alive && _rb != null && !_rb.isKinematic)
            {
                Vector3 v = _rb.linearVelocity;
                _rb.linearVelocity = new Vector3(0f, v.y, 0f);
            }

            // Server: start the bleed-out clock on entering Downed. Leaving Downed (revive → Alive,
            // or → Dead) makes the Update guard below stop it — no explicit cancel needed.
            if (IsServerInitialized && next == PlayerLifeState.Downed)
                _bleedOutEnd = Time.time + _bleedOutSeconds;
        }

        private void Update()
        {
            if (!IsServerInitialized) return;
            if (_state != null && _state.IsDowned && Time.time >= _bleedOutEnd)
                _state.SetState(PlayerLifeState.Dead);
        }
    }
}
