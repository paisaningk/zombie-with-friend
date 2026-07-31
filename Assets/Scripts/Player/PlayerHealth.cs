using System;
using FishNet.Object;
using FishNet.Object.Synchronizing;
using UnityEngine;

namespace Player
{
    /// <summary>
    /// Server-authoritative player health. STORE-ONLY: it holds HP, applies damage, and
    /// drives the one transition it owns — HP hits 0 → Downed (via <see cref="PlayerState"/>).
    ///
    /// It does NOT own respawn/refill: revive (task 7) and wave-clear (task 11) set HP
    /// explicitly from outside, and choose the amount (partial vs full). Knockback is NOT a
    /// PlayerHealth concern — <see cref="ReceiveHit"/> uses only the damage, never the impulse.
    ///
    /// Damage enters via <see cref="IHitReceiver"/> (server-side, from the combat pipeline) or
    /// the public <see cref="ApplyDamage"/>. HP only drops while the player is Alive.
    ///
    /// Consumer contract (same as PlayerState): subscribe to <see cref="OnHealthChanged"/> in
    /// Awake. You get exactly ONE initial call reporting spawn HP (prev == next == max), then
    /// one call per real change. Read via the event / properties — never touch the SyncVar.
    /// </summary>
    [RequireComponent(typeof(PlayerState))]
    public class PlayerHealth : NetworkBehaviour, IHitReceiver
    {
        [Tooltip("Full HP a freshly spawned player has. Class-based / data-driven value (SO) comes later (task 14).")]
        [SerializeField] private float _maxHp = 100f;

        // Hidden on purpose: consumers read via the properties and react via OnHealthChanged.
        private readonly SyncVar<float> _health = new SyncVar<float>();

        /// <summary>(previous, next) HP. Fired once on start (prev == next == spawn HP), then on every change.</summary>
        public event Action<float, float> OnHealthChanged;

        // Last value we announced. Guards the initial fire and the host's double SyncVar callback.
        private float _notified;
        private bool _hasNotified;

        private PlayerState _state;

        public float Current => _health.Value;
        public float Max => _maxHp;
        public float Normalized => _maxHp > 0f ? Mathf.Clamp01(_health.Value / _maxHp) : 0f;
        public bool IsFull => Mathf.Approximately(_health.Value, _maxHp);

        private void Awake()
        {
            _state = GetComponent<PlayerState>();
            _health.OnChange += HandleSyncChange;
        }

        private void OnDestroy()
        {
            _health.OnChange -= HandleSyncChange;
        }

        public override void OnStartServer()
        {
            base.OnStartServer();
            // Spawn at full HP. Setting it here (not via a field default the event can't see as
            // "max") means the first OnHealthChanged already reports max, and observers receive
            // full HP as the spawn-synced value — no bogus 0 ever reaches a consumer.
            _health.Value = _maxHp;
        }

        public override void OnStartClient()
        {
            base.OnStartClient();
            // Guarantee the initial fire on clients (HP is synced to max by now, since the
            // server set it in OnStartServer which runs first). Notify dedupes if the spawn
            // sync already raised OnChange, so this never double-fires.
            Notify(_health.Value);
        }

        /// <summary>
        /// Server damage entry from the combat pipeline. Only the damage is used; knockback is
        /// deliberately ignored. No-op off-server. Callers guard with IsServerInitialized (as
        /// NetworkProjectile does), and we re-check here for safety.
        /// </summary>
        public void ReceiveHit(in HitInfo hit)
        {
            if (!IsServerInitialized) return;
            ApplyDamage(hit.Damage);
        }

        /// <summary>
        /// The single damage entry point. Reduces HP by <paramref name="amount"/> (ignored if
        /// &lt;= 0), clamped at 0, and only while Alive (Downed/Dead take no damage — no
        /// finish-off-downed). Reaching 0 puts the player Downed. Server-only.
        /// Callers: <see cref="ReceiveHit"/>, and future direct sources (enemy melee/DoT, tests).
        /// </summary>
        [Server]
        public void ApplyDamage(float amount)
        {
            if (amount <= 0f) return;
            if (!_state.IsAlive) return;

            float next = Mathf.Max(0f, _health.Value - amount);
            if (next == _health.Value) return;
            _health.Value = next;

            if (next <= 0f)
                _state.SetState(PlayerLifeState.Downed);
        }

        /// <summary>
        /// Directly set HP, clamped to [0, Max]. For revive (task 7, partial) and wave-clear
        /// (task 11, full). NO Alive-guard — unlike <see cref="ApplyDamage"/> this is meant to
        /// set HP on a Downed player. Store-only: does NOT touch state — the caller sets
        /// PlayerState.Alive separately. Server-only.
        /// </summary>
        [Server]
        public void SetHp(float amount)
        {
            float next = Mathf.Clamp(amount, 0f, _maxHp);
            if (next == _health.Value) return;
            _health.Value = next;
        }

        // --- SyncVar → event plumbing (mirrors PlayerState) ---
        // We ignore asServer and dedupe by value in Notify(), collapsing the host's server+client
        // double callback into a single announcement; the initial fire reports prev == next.

        private void HandleSyncChange(float prev, float next, bool asServer)
        {
            Notify(next);
        }

        private void Notify(float next)
        {
            if (_hasNotified && Mathf.Approximately(_notified, next)) return;

            var prev = _hasNotified ? _notified : next; // initial fire => prev == next
            _notified = next;
            _hasNotified = true;
            OnHealthChanged?.Invoke(prev, next);
        }

#if UNITY_EDITOR
        private void OnValidate()
        {
            if (_maxHp < 1f) _maxHp = 1f;
        }
#endif
    }
}
