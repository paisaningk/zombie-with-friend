using System;
using System.Collections.Generic;
using System.Linq;
using FishNet.Object;
using FishNet.Object.Synchronizing;
using Player;
using UnityEngine;

namespace Game
{
    /// <summary>
    /// Minimal server-side match coordinator + synced match state (<see cref="GameState"/>).
    ///
    /// Lives as a scene NetworkObject (NOT DontDestroyOnLoad, unlike LobbyManager) — it exists
    /// for the duration of a match and is torn down with the game scene. <see cref="Instance"/>
    /// is set on BOTH server and clients so client-read state (GameState) works; the player
    /// registry is server-only.
    ///
    /// Lose is event-driven (no timer): whenever a registered player's state changes — or one
    /// disconnects — while Playing, if players exist but NONE are Alive the match is Lost.
    /// </summary>
    public class GameManager : NetworkBehaviour
    {
        public static GameManager Instance { get; private set; }

        // Server-only. On clients this stays empty; nothing reads it there. Holds the player facade
        // (PlayerController) so consumers reach State/Health/Wallet through one handle (decision 0010).
        private readonly List<PlayerController> _players = new List<PlayerController>();
        public IReadOnlyList<PlayerController> Players => _players;

        // Synced match state. Consumers (result screen, task 16) subscribe to OnGameStateChanged;
        // one event covers both Lost (task 8) and Won (task 10).
        private readonly SyncVar<GameState> _gameState = new SyncVar<GameState>();

        /// <summary>(previous, next). Fires once on start (prev == next), then on every change.</summary>
        public event Action<GameState, GameState> OnGameStateChanged;

        private GameState _notified;
        private bool _hasNotified;

        public GameState State => _gameState.Value;

        /// <summary>
        /// Server-only: true while the between-wave shop window is open (set by <see cref="WaveManager"/>).
        /// The purchase RPC gates on this so upgrades can only be bought between waves (decision 0011).
        /// Plain bool (not synced) — only the server's purchase validation reads it; a future shop UI
        /// that needs it client-side can promote it to a SyncVar (task 16).
        /// </summary>
        public bool ShopOpen { get; private set; }

        /// <summary>Open/close the shop window. Server-only. Called by WaveManager around its ShopWindow.</summary>
        [Server]
        public void SetShopOpen(bool open) => ShopOpen = open;

        private void Awake()
        {
            _gameState.OnChange += HandleGameStateSync;
        }

        private void OnDestroy()
        {
            _gameState.OnChange -= HandleGameStateSync;
        }

        // Runs once per peer (server and client), so Instance is available on both.
        public override void OnStartNetwork()
        {
            base.OnStartNetwork();
            if (Instance != null && Instance != this)
            {
                Debug.LogWarning("[GameManager] A second GameManager initialized — there should " +
                                 "be exactly one per scene. Keeping the first, ignoring this one.");
                return;
            }
            Instance = this;
            Notify(_gameState.Value); // initial fire (all peers)
        }

        public override void OnStartServer()
        {
            base.OnStartServer();
            // The game scene is up → we're playing. task 15 will drive Lobby → Playing properly.
            SetGameState(GameState.Playing);
        }

        public override void OnStopNetwork()
        {
            base.OnStopNetwork();
            if (Instance == this)
                Instance = null;
        }

        /// <summary>The single mutation point for match state. Server-only. No-op if unchanged.</summary>
        [Server]
        public void SetGameState(GameState next)
        {
            if (_gameState.Value == next) return;
            _gameState.Value = next;
        }

        [Server]
        public void RegisterPlayer(PlayerController player)
        {
            if (player == null) return;
            if (!_players.Contains(player))
            {
                _players.Add(player);
                if (player.State != null)
                    player.State.OnStateChanged += HandlePlayerStateChanged;
            }
        }

        [Server]
        public void UnregisterPlayer(PlayerController player)
        {
            if (player != null && player.State != null)
                player.State.OnStateChanged -= HandlePlayerStateChanged;
            if (_players.Remove(player))
                CheckLose(); // the last Alive player disconnecting can trigger a loss
        }

        private void HandlePlayerStateChanged(PlayerLifeState prev, PlayerLifeState next) => CheckLose();

        /// <summary>
        /// Lose when Playing AND players exist but NONE are Alive. The Count &gt; 0 guard excludes
        /// the empty case (no players yet / everyone left) — that is not a team wipe.
        /// </summary>
        [Server]
        private void CheckLose()
        {
            if (_gameState.Value != GameState.Playing) return;
            if (_players.Count > 0 && !_players.Any(p => p != null && p.State != null && p.State.IsAlive))
                SetGameState(GameState.Lost);
        }

        /// <summary>True while at least one registered player is Alive (and any exist).</summary>
        [Server]
        public bool AnyPlayerAlive()
        {
            return _players.Count > 0 && _players.Any(p => p != null && p.State != null && p.State.IsAlive);
        }

        // --- economy (task 11) ---

        /// <summary>
        /// Split <paramref name="reward"/> equally among ALL connected players (regardless of
        /// Alive/Downed/Dead — no kill credit). Integer floor; the remainder is dropped so everyone
        /// gets exactly the same. Server-only. Called on every enemy kill via WaveManager.
        /// </summary>
        [Server]
        public void AwardGoldForKill(int reward)
        {
            if (reward <= 0) return;
            int n = _players.Count;
            if (n <= 0) return;

            int share = reward / n; // floor — equal split, remainder dropped
            if (share <= 0) return;

            for (int i = 0; i < n; i++)
            {
                PlayerController p = _players[i];
                if (p != null && p.Wallet != null)
                    p.Wallet.Add(share);
            }
        }

        /// <summary>
        /// Grant <paramref name="amount"/> to every player who is still Alive — a reward for
        /// surviving the wave. MUST be called BEFORE wave-clear auto-revive, or everyone would be
        /// Alive and the bonus would be meaningless. Server-only.
        /// </summary>
        [Server]
        public void AwardSurvivalBonus(int amount)
        {
            if (amount <= 0) return;
            for (int i = 0; i < _players.Count; i++)
            {
                PlayerController p = _players[i];
                if (p != null && p.State != null && p.State.IsAlive && p.Wallet != null)
                    p.Wallet.Add(amount);
            }
        }

        // --- GameState SyncVar → event plumbing (mirrors PlayerState) ---
        private void HandleGameStateSync(GameState prev, GameState next, bool asServer) => Notify(next);

        private void Notify(GameState next)
        {
            if (_hasNotified && _notified == next) return;

            var prev = _hasNotified ? _notified : next; // initial fire => prev == next
            _notified = next;
            _hasNotified = true;
            OnGameStateChanged?.Invoke(prev, next);
        }
    }
}
