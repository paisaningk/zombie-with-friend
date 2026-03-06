using FishNet;
using FishNet.Object;
using UnityEngine;

namespace Bullet
{
    public sealed class NetworkShooter : NetworkBehaviour
    {
        [SerializeField] private Transform muzzle;
        [SerializeField] private NetworkProjectile projectilePrefab;
        [SerializeField] private float fireCooldown = 0.12f;

        private float nextFireTime;

        private void Update()
        {
            if (!IsOwner) return;
            if (!muzzle || !projectilePrefab) return;

            if (Input.GetMouseButton(0) && Time.time >= nextFireTime)
            {
                nextFireTime = Time.time + fireCooldown;

                // ส่งตำแหน่ง + ทิศยิงของ muzzle ไปให้ server
                ServerFire(muzzle.position, muzzle.forward);
            }
        }

        [ServerRpc]
        private void ServerFire(Vector3 pos, Vector3 dir)
        {
            // ชัด ๆ ว่าตอนนี้ server active ไหม
            bool serverStarted = InstanceFinder.ServerManager != null && InstanceFinder.ServerManager.Started;
            Debug.Log($"[ServerFire] IsServer={IsServer} ServerStarted={serverStarted} ObjSpawned={base.IsSpawned}");

            if (!serverStarted) return;

            dir.y = 0f;
            if (dir.sqrMagnitude < 0.0001f) return;
            dir.Normalize();

            var rot = Quaternion.LookRotation(dir, Vector3.up);
            NetworkProjectile p = Instantiate(projectilePrefab, pos, rot);
            p.ServerInit(dir);

            // Spawn แบบชัวร์ ๆ
            InstanceFinder.ServerManager.Spawn(p.NetworkObject);
        }
    }
}
