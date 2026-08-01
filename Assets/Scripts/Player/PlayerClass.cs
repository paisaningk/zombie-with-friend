using System;
using FishNet.Object;
using FishNet.Object.Synchronizing;

namespace Player
{
    /// <summary>
    /// Server-authoritative holder for a single player's <see cref="PlayerClassType"/> (decision 0012).
    ///
    /// Pure data holder — NO class rules live here. It only stores the class, syncs it, and announces
    /// changes. Ability/stat systems read <see cref="Current"/> (or the convenience flags) to decide
    /// what a player can do; e.g. <see cref="HealPulseAbility"/> gates on <see cref="IsSupport"/>.
    ///
    /// The SyncVar is all-read (like <see cref="PlayerState"/>/<see cref="PlayerReady"/>): class is team
    /// information a future UI shows for every teammate. Defaults to <see cref="PlayerClassType.Support"/>
    /// on the server so the Heal Pulse is usable immediately; the lobby class-select (task 14) overrides
    /// it via <see cref="SetClass"/>.
    ///
    /// Consumer contract mirrors PlayerState: subscribe to <see cref="OnClassChanged"/> in Awake — one
    /// initial fire (prev == next) then one per change.
    /// </summary>
    public class PlayerClass : NetworkBehaviour
    {
        // All-read: teammates need to see each other's class (class HUD, task 16).
        private readonly SyncVar<PlayerClassType> _class = new SyncVar<PlayerClassType>();

        /// <summary>(previous, next). Fired once on start (prev == next), then on every change.</summary>
        public event Action<PlayerClassType, PlayerClassType> OnClassChanged;

        private PlayerClassType _notified;
        private bool _hasNotified;

        public PlayerClassType Current => _class.Value;
        public bool IsSupport => _class.Value == PlayerClassType.Support;
        public bool IsGunner => _class.Value == PlayerClassType.Gunner;

        private void Awake()
        {
            _class.OnChange += HandleSyncChange;
        }

        private void OnDestroy()
        {
            _class.OnChange -= HandleSyncChange;
        }

        public override void OnStartServer()
        {
            base.OnStartServer();
            // Default class until the lobby (task 14) chooses one. Support so Heal Pulse works now.
            _class.Value = PlayerClassType.Support;
        }

        // Runs once per peer (server and client). Awake has already run, so consumers that subscribed
        // in their own Awake catch this initial fire.
        public override void OnStartNetwork()
        {
            base.OnStartNetwork();
            Notify(_class.Value);
        }

        /// <summary>The single mutation point for class. Server-only. No-op if unchanged.
        /// Caller (task 14): the lobby applies the player's chosen class before the match.</summary>
        [Server]
        public void SetClass(PlayerClassType next)
        {
            if (_class.Value == next) return;
            _class.Value = next;
        }

        // --- SyncVar → event plumbing (mirrors PlayerState) ---
        private void HandleSyncChange(PlayerClassType prev, PlayerClassType next, bool asServer) => Notify(next);

        private void Notify(PlayerClassType next)
        {
            if (_hasNotified && _notified == next) return;

            var prev = _hasNotified ? _notified : next; // initial fire => prev == next
            _notified = next;
            _hasNotified = true;
            OnClassChanged?.Invoke(prev, next);
        }
    }
}
