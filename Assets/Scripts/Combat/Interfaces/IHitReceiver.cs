using UnityEngine;

public interface IHitReceiver
{
    /// <summary>
    /// Apply a hit. Returns TRUE when this hit killed/downed the receiver — local attribution for
    /// on-kill weapon effects (decision 0016, W5): the shooter learns "this shot got the kill" without
    /// threading an attacker through the whole damage pipeline. Receivers with no death concept
    /// (knockback-only) return false.
    /// </summary>
    bool ReceiveHit(in HitInfo hit);
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