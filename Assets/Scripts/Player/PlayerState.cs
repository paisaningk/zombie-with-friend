using System;
using FishNet.Object;
using FishNet.Object.Synchronizing;
using UnityEngine;

namespace Player
{
    /// <summary>
    /// Server-authoritative holder for a single player's <see cref="PlayerLifeState"/>.
    ///
    /// Pure data holder — NO game rules live here (HP thresholds, bleed-out timers,
    /// enemy-layer changes, movement/combat toggles belong to their own systems).
    /// This component only stores the state, syncs it, and announces changes.
    ///
    /// Contract for consumers:
    ///   - Subscribe to <see cref="OnStateChanged"/> in your Awake().
    ///   - You receive exactly ONE initial call on start (prev == next == current),
    ///     then one call per real change. So a single handler covers init + updates.
    ///   - Never write state directly; only the server via <see cref="SetState"/> may.
    /// </summary>
    public class PlayerState : NetworkBehaviour
    {
        // Hidden on purpose: consumers must not touch the FishNet SyncVar type.
        // They read via the properties below and react via OnStateChanged.
        private readonly SyncVar<PlayerLifeState> _state = new SyncVar<PlayerLifeState>();

        /// <summary>(previous, next). Fired once on start (prev == next), then on every change.</summary>
        public event Action<PlayerLifeState, PlayerLifeState> OnStateChanged;

        // Tracks the last value we announced. Guards two things:
        //   1. the initial start-fire, and
        //   2. the host's double SyncVar callback (asServer true + false, same value).
        private PlayerLifeState _notified;
        private bool _hasNotified;

        public PlayerLifeState Current => _state.Value;
        public bool IsAlive => _state.Value == PlayerLifeState.Alive;
        public bool IsDowned => _state.Value == PlayerLifeState.Downed;
        public bool IsDead => _state.Value == PlayerLifeState.Dead;

        private void Awake()
        {
            _state.OnChange += HandleSyncChange;
        }

        private void OnDestroy()
        {
            _state.OnChange -= HandleSyncChange;
        }

        // Runs exactly once per peer (server and client). Awake has already run, so any
        // consumer that subscribed in its own Awake is guaranteed to catch this fire.
        public override void OnStartNetwork()
        {
            base.OnStartNetwork();
            Notify(_state.Value);
        }

        // Registration with GameManager moved to PlayerController (task 11): the registry holds the
        // player facade (PlayerController), not this bare state holder. See decision 0010.

        /// <summary>
        /// The single mutation point for player state. Server-only.
        /// No-op if already in <paramref name="next"/> (so we never announce a redundant change).
        /// Callers: PlayerHealth (→ Downed), bleed-out (→ Dead), revive / wave-clear (→ Alive).
        /// </summary>
        [Server]
        public void SetState(PlayerLifeState next)
        {
            if (_state.Value == next) return;
            _state.Value = next;
        }

        private void HandleSyncChange(PlayerLifeState prev, PlayerLifeState next, bool asServer)
        {
            // We deliberately ignore asServer and dedupe by value in Notify(), which collapses
            // the host's server+client double callback into a single announcement.
            Notify(next);
        }

        private void Notify(PlayerLifeState next)
        {
            if (_hasNotified && _notified == next) return;

            var prev = _hasNotified ? _notified : next; // initial fire => prev == next
            _notified = next;
            _hasNotified = true;
            OnStateChanged?.Invoke(prev, next);
        }
    }
}
