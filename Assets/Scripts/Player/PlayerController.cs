using FishNet.Object;
using Game;
using UnityEngine;

namespace Player
{
    /// <summary>
    /// The player's root component — it plays two roles (see decision 0010):
    ///
    /// 1. HUB / facade. It caches every sibling sub-component and exposes them as properties
    ///    (<see cref="State"/>, <see cref="Health"/>, <see cref="Wallet"/>, <see cref="Movement"/>,
    ///    <see cref="Weapon"/>). External systems reference the player through this one handle
    ///    instead of scattering GetComponent calls, and <see cref="GameManager"/>'s registry holds
    ///    PlayerControllers — so it also registers/unregisters the player with GameManager.
    ///
    /// 2. Lifecycle coordinator. It owns NO gameplay logic (HP lives in PlayerHealth, gold in
    ///    PlayerWallet, etc.) — it only REACTS to <see cref="PlayerState"/> transitions: gating the
    ///    input-driven components, freezing the body when going down, and running the server-side
    ///    bleed-out timer (Downed → Dead). Revive / wave-clear just call
    ///    <c>State.SetState(Alive)</c> and this re-enables everything + the bleed-out stops.
    ///
    /// Rule: Movement + Weapon are active ONLY while Alive (off in both Downed and Dead). PlayerLook
    /// is intentionally left alone — a downed/dead player can still look around.
    /// </summary>
    public class PlayerController : NetworkBehaviour
    {
        [Tooltip("Seconds a Downed player bleeds out before dying (server-authoritative).")]
        [SerializeField] private float _bleedOutSeconds = 30f;

        // --- facade: cached sibling refs (add more as components grow) ---
        public PlayerState State { get; private set; }
        public PlayerHealth Health { get; private set; }
        public PlayerWallet Wallet { get; private set; }
        public PlayerReady Ready { get; private set; }
        public PlayerMovement Movement { get; private set; }
        public Combat.PlayerWeapon Weapon { get; private set; }

        private Rigidbody _rb;
        private float _bleedOutEnd;

        private void Awake()
        {
            State = GetComponent<PlayerState>();
            Health = GetComponent<PlayerHealth>();
            Wallet = GetComponent<PlayerWallet>();
            Ready = GetComponent<PlayerReady>();
            Movement = GetComponent<PlayerMovement>();
            Weapon = GetComponent<Combat.PlayerWeapon>();
            _rb = GetComponent<Rigidbody>();

            if (State != null)
                State.OnStateChanged += HandleStateChanged;
            else
                Debug.LogWarning("[PlayerController] No PlayerState found; coordinator is inert.");
        }

        private void OnDestroy()
        {
            if (State != null)
                State.OnStateChanged -= HandleStateChanged;
        }

        // The player IS this PlayerController to the rest of the game — register the facade, not the
        // bare state (moved here from PlayerState so the registry holds a full player handle).
        public override void OnStartServer()
        {
            base.OnStartServer();
            if (GameManager.Instance != null)
            {
                GameManager.Instance.RegisterPlayer(this);
            }
            else
            {
                Debug.LogWarning("[PlayerController] GameManager.Instance is null on OnStartServer — " +
                                 "player was not registered. Ensure a GameManager exists in the " +
                                 "scene before players spawn.");
            }
        }

        public override void OnStopServer()
        {
            base.OnStopServer();
            if (GameManager.Instance != null)
                GameManager.Instance.UnregisterPlayer(this);
        }

        // Fires once on start (prev == next == current) then once per real change, on every peer.
        private void HandleStateChanged(PlayerLifeState prev, PlayerLifeState next)
        {
            bool alive = next == PlayerLifeState.Alive;

            // Gate input-driven components. Look is deliberately untouched.
            if (Movement != null) Movement.enabled = alive;
            if (Weapon != null) Weapon.enabled = alive;

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
            if (State != null && State.IsDowned && Time.time >= _bleedOutEnd)
                State.SetState(PlayerLifeState.Dead);
        }
    }
}
