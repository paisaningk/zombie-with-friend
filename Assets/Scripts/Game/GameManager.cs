using System.Collections.Generic;
using System.Linq;
using FishNet.Object;
using Player;
using UnityEngine;

namespace Game
{
    /// <summary>
    /// Minimal server-side match coordinator.
    ///
    /// Lives as a scene NetworkObject (NOT DontDestroyOnLoad, unlike LobbyManager) — it
    /// exists for the duration of a match and is torn down with the game scene.
    ///
    /// <see cref="Instance"/> is set on BOTH server and clients so future client-read state
    /// (GameState SyncVar, task 15) works without reworking this. The player registry,
    /// however, is server-only: registration is guarded by [Server] and the list stays
    /// empty on clients.
    /// </summary>
    public class GameManager : NetworkBehaviour
    {
        public static GameManager Instance { get; private set; }

        // Server-only. On clients this stays empty; nothing reads it there.
        private readonly List<PlayerState> _players = new List<PlayerState>();
        public IReadOnlyList<PlayerState> Players => _players;

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
        }

        public override void OnStopNetwork()
        {
            base.OnStopNetwork();
            if (Instance == this)
                Instance = null;
        }

        [Server]
        public void RegisterPlayer(PlayerState player)
        {
            if (player == null) return;
            if (!_players.Contains(player))
                _players.Add(player);
        }

        [Server]
        public void UnregisterPlayer(PlayerState player)
        {
            _players.Remove(player);
        }

        /// <summary>
        /// Stub for the lose-condition system (task 8). True while at least one registered
        /// player is Alive. The Count > 0 guard prevents a lone host with no players yet
        /// from reading as "everyone dead". Real semantics (only meaningful while
        /// GameState == Playing) land in task 8.
        /// </summary>
        [Server]
        public bool AnyPlayerAlive()
        {
            return _players.Count > 0 && _players.Any(p => p != null && p.IsAlive);
        }
    }
}
