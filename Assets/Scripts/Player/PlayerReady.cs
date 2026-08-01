using System;
using FishNet.Object;
using FishNet.Object.Synchronizing;

namespace Player
{
    /// <summary>
    /// Per-player readiness flag (see decision 0011). Used by the between-wave shop window
    /// (task 12) — the wave loop advances once every connected player is ready (or a timer
    /// elapses) — and reused by the lobby class-select ready-check (task 14).
    ///
    /// The SyncVar is all-read (default permission), UNLIKE gold/upgrades: readiness is team
    /// information (a future shop UI shows "2/4 ready"), so every peer receives it.
    ///
    /// Owner toggles via <see cref="CmdSetReady"/>; the server resets everyone at the start of
    /// each shop window via <see cref="ServerSetReady"/>. Consumer contract mirrors PlayerHealth:
    /// subscribe to <see cref="OnReadyChanged"/> in Awake, get one initial fire then one per change.
    /// </summary>
    public class PlayerReady : NetworkBehaviour
    {
        // All-read: teammates need to see who is ready (shop UI, task 16).
        private readonly SyncVar<bool> _isReady = new SyncVar<bool>();

        /// <summary>(previous, next) readiness. Fires once on start (prev == next), then per change.</summary>
        public event Action<bool, bool> OnReadyChanged;

        private bool _notified;
        private bool _hasNotified;

        public bool IsReady => _isReady.Value;

        private void Awake()
        {
            _isReady.OnChange += HandleSyncChange;
        }

        private void OnDestroy()
        {
            _isReady.OnChange -= HandleSyncChange;
        }

        public override void OnStartServer()
        {
            base.OnStartServer();
            _isReady.Value = false; // fresh per match
        }

        public override void OnStartClient()
        {
            base.OnStartClient();
            Notify(_isReady.Value); // guarantee an initial fire on every peer (dedupes)
        }

        /// <summary>Owner sets its own readiness (e.g. clicking Ready in the shop). Server validates ownership.</summary>
        [ServerRpc]
        public void CmdSetReady(bool ready)
        {
            _isReady.Value = ready;
        }

        /// <summary>Server-side set/reset (shop window opens → reset all to false). Server-only.</summary>
        [Server]
        public void ServerSetReady(bool ready)
        {
            _isReady.Value = ready;
        }

        // --- SyncVar → event plumbing (mirrors PlayerHealth/PlayerWallet) ---
        private void HandleSyncChange(bool prev, bool next, bool asServer) => Notify(next);

        private void Notify(bool next)
        {
            if (_hasNotified && _notified == next) return;

            bool prev = _hasNotified ? _notified : next; // initial fire => prev == next
            _notified = next;
            _hasNotified = true;
            OnReadyChanged?.Invoke(prev, next);
        }
    }
}
