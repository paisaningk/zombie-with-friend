using FishNet.Connection;
using FishNet.Object;
using FishNet.Transporting;
using UnityEngine;
namespace Networking
{
    public class PlayerSpawner : NetworkBehaviour
    {
        [SerializeField] private NetworkObject PlayerPrefab;

        public override void OnStartServer()
        {
            base.OnStartServer();
            // ฟัง event ตอน client เข้ามา
            ServerManager.OnRemoteConnectionState += OnClientConnected;
        }

        public override void OnStopServer()
        {
            base.OnStopServer();
            ServerManager.OnRemoteConnectionState -= OnClientConnected;
        }

        private void OnClientConnected(NetworkConnection conn, RemoteConnectionStateArgs args)
        {
            // เช็คว่า connected (ไม่ใช่ disconnected)
            if (args.ConnectionState != RemoteConnectionState.Started) return;

            // Spawn เฉพาะฝั่ง Server เท่านั้น
            SpawnPlayer(conn);
        }

        [Server]
        private void SpawnPlayer(NetworkConnection conn)
        {
            // เลือก spawn point
            var spawnPos = GetSpawnPoint();

            // Instantiate แล้ว give ownership ให้ client นั้น
            var player = Instantiate(PlayerPrefab, spawnPos, Quaternion.identity);
            ServerManager.Spawn(player, conn); // conn = owner

            Debug.Log($"[PlayerSpawner] Spawned player for connection {conn.ClientId}");
        }

        private Vector3 GetSpawnPoint()
        {
            // ง่ายๆ ก่อน random ใน area
            return new Vector3(Random.Range(-5f, 5f), 0f, Random.Range(-5f, 5f));
        }
    }
}
