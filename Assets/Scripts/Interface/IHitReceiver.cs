using UnityEngine;

public interface IHitReceiver
{
    void ReceiveHit(in HitInfo hit);
}

public readonly struct HitInfo
{
    public readonly Vector3 Point;
    public readonly Vector3 Direction;   // ทิศที่ผลัก (normalized)
    public readonly float Damage;
    public readonly float KnockbackImpulse;

    public HitInfo(Vector3 point, Vector3 direction, float damage, float knockbackImpulse)
    {
        Point = point;
        Direction = direction;
        Damage = damage;
        KnockbackImpulse = knockbackImpulse;
    }
}