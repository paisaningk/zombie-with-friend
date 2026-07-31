using FishNet.Object;
using UnityEngine;

/// <summary>
/// A server-authoritative travelling projectile. The server owns hit resolution
/// (<see cref="IsServerInitialized"/> guards <see cref="OnTriggerEnter"/>); clients just watch it
/// move via NetworkTransform.
///
/// Filtering is layer-mask based so ONE prefab kind serves both directions:
///   • damageMask — layers it damages (must carry an <see cref="IHitReceiver"/>). Enemy ranger
///     projectile → Player; a player projectile weapon → Enemy (no friendly fire either way).
///   • blockMask  — solid layers that simply stop it (walls / ground).
///   • anything else is passed through (e.g. other enemies for an enemy projectile).
///
/// Requires a TRIGGER collider (OnTriggerEnter) and a kinematic Rigidbody so trigger events also
/// fire against static geometry (ground/walls have no Rigidbody of their own).
/// </summary>
[RequireComponent(typeof(NetworkObject))]
public sealed class NetworkProjectile : NetworkBehaviour
{
    [SerializeField] private float speed = 30f;
    [SerializeField] private float lifeTime = 2.5f;
    [SerializeField] private float damage = 10f;
    [SerializeField] private float knockbackImpulse = 6f;

    [Header("Filtering")]
    [Tooltip("Layers this projectile damages (target must have an IHitReceiver).")]
    [SerializeField] private LayerMask damageMask = ~0;
    [Tooltip("Solid layers that stop the projectile without damage (walls / ground).")]
    [SerializeField] private LayerMask blockMask = 0;

    private Vector3 _vel;
    private float _dieAt;

    // Server sets direction + stats (speed/damage come from the firing weapon/enemy data).
    [Server]
    public void ServerInit(Vector3 dir, float newSpeed, float newDamage)
    {
        speed = newSpeed;
        damage = newDamage;

        if (dir.sqrMagnitude < 0.0001f) dir = transform.forward;
        dir.Normalize(); // full 3D direction (aim includes pitch)

        _vel = dir * speed;
        _dieAt = Time.time + lifeTime;
    }

    private void Update()
    {
        transform.position += _vel * Time.deltaTime;

        if (Time.time >= _dieAt)
            Despawn();
    }

    private void OnTriggerEnter(Collider other)
    {
        if (!IsServerInitialized) return; // hit resolution is server-only

        int bit = 1 << other.gameObject.layer;

        if ((damageMask.value & bit) != 0)
        {
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
            return;
        }

        if ((blockMask.value & bit) != 0)
            Despawn();
        // else: not a target and not a wall → pass through
    }

    [Server]
    private void Despawn()
    {
        if (IsSpawned)
            base.Despawn();
    }
}
