using FishNet.Object;
using UnityEngine;

[RequireComponent(typeof(NetworkObject))]
public sealed class NetworkProjectile : NetworkBehaviour
{
    [SerializeField] private float speed = 30f;
    [SerializeField] private float lifeTime = 2.5f;
    [SerializeField] private float damage = 10f;
    [SerializeField] private float knockbackImpulse = 6f;

    private Vector3 _vel;
    private float _dieAt;

    // ให้ server ตั้งค่าทิศทาง/ความเร็ว
    [Server]
    public void ServerInit(Vector3 dir)
    {
        dir.y = 0f;
        if (dir.sqrMagnitude < 0.0001f) dir = transform.forward;
        dir.Normalize();

        _vel = dir * speed;
        _dieAt = Time.time + lifeTime;
    }

    private void Update()
    {
        transform.position += _vel * Time.deltaTime;

        if (Time.time >= _dieAt)
        {
            Despawn();
        }
    }

    private void OnTriggerEnter(Collider other)
    {
        if (!IsServerInitialized) return; // ตัดสิน hit บน server เท่านั้น

        if (other.CompareTag("Player"))
        {
            return;
        }

        if (other.TryGetComponent<IHitReceiver>(out var receiver))
        {
            Vector3 dir = _vel.sqrMagnitude > 0f ? _vel.normalized : transform.forward;
            dir.y = 0f;
            if (dir.sqrMagnitude > 0.0001f) dir.Normalize();

            receiver.ReceiveHit(new HitInfo(
                point: transform.position,
                direction: dir,
                damage: damage,
                knockbackImpulse: knockbackImpulse
            ));
        }

        Despawn();
        
        Debug.Log(other.gameObject.name);
    }

    [Server]
    private void Despawn()
    {
        if (IsSpawned)
            base.Despawn();
    }
}
