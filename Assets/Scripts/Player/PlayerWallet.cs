using System;
using FishNet.Object;
using FishNet.Object.Synchronizing;
using UnityEngine;

namespace Player
{
    /// <summary>
    /// Server-authoritative per-player gold (see decision 0010). Mirrors <see cref="PlayerHealth"/>'s
    /// shape: a hidden SyncVar, an <see cref="OnGoldChanged"/> event, a read-only property, and
    /// server-only mutators. Earned via split-on-kill / survival bonus (GameManager) and spent by
    /// the shop (task 12, <see cref="TrySpend"/>).
    ///
    /// The SyncVar is <see cref="ReadPermission.OwnerOnly"/> — gold is private to its owner (no
    /// teammate-gold leak, and it keeps the door open for a future kill-credit economy). Only the
    /// owner client receives the value and the initial fire; the server always has it authoritatively.
    /// </summary>
    public class PlayerWallet : NetworkBehaviour
    {
        // Owner-only: non-owner clients never receive this, so nothing outside the owner reads it.
        private readonly SyncVar<int> _gold = new SyncVar<int>(new SyncTypeSettings(ReadPermission.OwnerOnly));

        /// <summary>(previous, next) gold. Owner gets one initial fire (prev == next), then per change.</summary>
        public event Action<int, int> OnGoldChanged;

        private int _notified;
        private bool _hasNotified;

        public int Gold => _gold.Value;

        private void Awake()
        {
            _gold.OnChange += HandleSyncChange;
        }

        private void OnDestroy()
        {
            _gold.OnChange -= HandleSyncChange;
        }

        public override void OnStartServer()
        {
            base.OnStartServer();
            _gold.Value = 0; // fresh per match (component is re-created with the player each scene)
        }

        public override void OnStartClient()
        {
            base.OnStartClient();
            // Only the owner receives an OwnerOnly SyncVar, so only the owner needs the initial fire.
            if (IsOwner)
                Notify(_gold.Value);
        }

        /// <summary>Add gold (ignored if &lt;= 0). Server-only. Callers: kill split, survival bonus.</summary>
        [Server]
        public void Add(int amount)
        {
            if (amount <= 0) return;
            _gold.Value += amount;
        }

        /// <summary>
        /// Spend gold if affordable. Returns false (and changes nothing) if the amount is &lt;= 0 or
        /// exceeds the balance. Server-only. For the shop (task 12).
        /// </summary>
        [Server]
        public bool TrySpend(int amount)
        {
            if (amount <= 0) return false;
            if (_gold.Value < amount) return false;
            _gold.Value -= amount;
            return true;
        }

        // --- SyncVar → event plumbing (mirrors PlayerHealth) ---
        private void HandleSyncChange(int prev, int next, bool asServer) => Notify(next);

        private void Notify(int next)
        {
            if (_hasNotified && _notified == next) return;

            int prev = _hasNotified ? _notified : next; // initial fire => prev == next
            _notified = next;
            _hasNotified = true;
            OnGoldChanged?.Invoke(prev, next);
        }
    }
}
