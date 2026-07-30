using System.Collections.Generic;
using FishNet;
using FishNet.Connection;
using FishNet.Managing;
using FishNet.Managing.Scened;
using FishNet.Object;
using UnityEngine;

namespace Networking
{
    /// <summary>
    /// Spawns one player object per connection that is present in THIS spawner's scene.
    ///
    /// Built for a lobby-first flow: players connect while still in the menu, then the
    /// game scene is loaded later via <c>SceneManager.LoadGlobalScenes</c>. FishNet's
    /// built-in PlayerSpawner keys off <c>OnClientLoadedStartScenes</c> (fires once at
    /// connect-time) and therefore never fires for anyone in that flow. This spawner keys
    /// off scene PRESENCE instead:
    ///   - subscribes to <see cref="SceneManager.OnClientPresenceChangeEnd"/> for
    ///     connections that become present after we initialize, AND
    ///   - drains connections already present when we initialize (e.g. the host, who was
    ///     present in this scene before this object's Awake ran).
    /// Subscribe-first-then-drain avoids missing a connection that arrives between the two
    /// steps; <see cref="_spawned"/> makes it idempotent so nobody is spawned twice.
    /// Server-only; the list stays untouched on clients.
    /// </summary>
    public class GamePlayerSpawner : MonoBehaviour
    {
        [Tooltip("Networked player prefab to spawn, owned by the joining connection.")]
        [SerializeField] private NetworkObject playerPrefab;

        [Tooltip("Spawn points, used round-robin. Falls back to the prefab's transform if empty.")]
        [SerializeField] private Transform[] spawns = new Transform[0];

        private NetworkManager _networkManager;
        private int _nextSpawn;
        private readonly HashSet<NetworkConnection> _spawned = new HashSet<NetworkConnection>();

        private void Awake()
        {
            _networkManager = InstanceFinder.NetworkManager;
            if (_networkManager == null)
            {
                Debug.LogWarning($"[GamePlayerSpawner] No NetworkManager found; {gameObject.name} disabled.");
                enabled = false;
                return;
            }

            // Subscribe BEFORE the Start() drain so a presence change that lands between
            // the two is caught by the event rather than slipping through.
            _networkManager.SceneManager.OnClientPresenceChangeEnd += OnClientPresenceChangeEnd;
        }

        private void Start()
        {
            if (!_networkManager.IsServerStarted)
                return;

            // Anyone already present in our scene (the host, and any client that finished
            // loading this scene before we initialized) won't get an event — drain them now.
            if (_networkManager.SceneManager.SceneConnections.TryGetValue(gameObject.scene, out var conns))
            {
                foreach (var conn in conns)
                    TrySpawn(conn);
            }
        }

        private void OnDestroy()
        {
            if (_networkManager != null)
                _networkManager.SceneManager.OnClientPresenceChangeEnd -= OnClientPresenceChangeEnd;
        }

        private void OnClientPresenceChangeEnd(ClientPresenceChangeEventArgs args)
        {
            if (!_networkManager.IsServerStarted)
                return;
            if (args.Scene != gameObject.scene)
                return;

            if (args.Added)
                TrySpawn(args.Connection);
            else
                _spawned.Remove(args.Connection); // allow a re-join to spawn again
        }

        private void TrySpawn(NetworkConnection conn)
        {
            if (conn == null)
                return;
            if (!_spawned.Add(conn)) // already spawned for this connection
                return;
            if (playerPrefab == null)
            {
                Debug.LogWarning($"[GamePlayerSpawner] playerPrefab not assigned; cannot spawn for connection {conn.ClientId}.");
                return;
            }

            GetSpawn(out var position, out var rotation);
            NetworkObject nob = _networkManager.GetPooledInstantiated(playerPrefab, position, rotation, true);
            _networkManager.ServerManager.Spawn(nob, conn, gameObject.scene);
        }

        private void GetSpawn(out Vector3 position, out Quaternion rotation)
        {
            if (spawns.Length == 0)
            {
                position = playerPrefab.transform.position;
                rotation = playerPrefab.transform.rotation;
                return;
            }

            Transform point = spawns[_nextSpawn % spawns.Length];
            _nextSpawn++;
            if (point != null)
            {
                position = point.position;
                rotation = point.rotation;
            }
            else
            {
                position = playerPrefab.transform.position;
                rotation = playerPrefab.transform.rotation;
            }
        }
    }
}
