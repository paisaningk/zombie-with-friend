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

    // Server sets direction + stats (speed/damage come from the firing weapon's WeaponData).
    [Server]
    public void ServerInit(Vector3 dir, float newSpeed, float newDamage)
    {
        speed = newSpeed;
        damage = newDamage;

        if (dir.sqrMagnitude < 0.0001f) dir = transform.forward;
        dir.Normalize(); // full 3D direction (first-person aim includes pitch)

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

        var receiver = other.GetComponentInParent<IHitReceiver>();
        if (receiver != null)
        {
            Vector3 dir = _vel.sqrMagnitude > 0f ? _vel.normalized : transform.forward;

            receiver.ReceiveHit(new HitInfo(
                point: transform.position,
                direction: dir,
                damage: damage,
                knockbackImpulse: knockbackImpulse
            ));
        }

        Despawn();
    }

    [Server]
    private void Despawn()
    {
        if (IsSpawned)
            base.Despawn();
    }
}
