using FishNet.Object;
using UnityEngine;

namespace Combat
{
    /// <summary>
    /// Everything a <see cref="WeaponEffect"/> needs to act, gathered on the SERVER right after a hit is
    /// resolved (decision 0016, Q11). Effects run server-only — there is no client prediction to sync, so
    /// the whole "strategy is fragile in the fire path" problem never arises. Effect damage must go
    /// through <see cref="PlayerWeapon.EffectDamage"/> (direct ApplyDamage, no hooks) so an explosion
    /// can't re-trigger itself (recursion guard).
    /// </summary>
    public readonly struct EffectContext
    {
        public readonly PlayerWeapon Weapon;   // the firing weapon (server) — use for EffectDamage + FX RPCs
        public readonly NetworkObject Victim;  // what the shot hit (may be null on a miss)
        public readonly Vector3 Point;
        public readonly Vector3 Direction;
        public readonly float Damage;          // damage this shot dealt
        public readonly bool Killed;           // did this shot kill the victim (local attribution)

        public EffectContext(PlayerWeapon weapon, NetworkObject victim, Vector3 point, Vector3 direction, float damage, bool killed)
        {
            Weapon = weapon;
            Victim = victim;
            Point = point;
            Direction = direction;
            Damage = damage;
            Killed = killed;
        }
    }

    /// <summary>
    /// A server-side event effect carried by an attachment (decision 0016, Q11 effect layer). This is the
    /// extension point for "weird" behaviour (lifesteal / chain / explosive / on-kill …): subclass, drop
    /// the logic in a hook, make an SO asset — no change to the core fire path. Both hooks default to
    /// no-op so an effect implements only the one it cares about.
    /// </summary>
    public abstract class WeaponEffect : ScriptableObject
    {
        /// <summary>Runs on every hit that dealt damage (server). E.g. Explosive AoE, Chain lightning.</summary>
        public virtual void OnHitDealt(in EffectContext ctx) { }

        /// <summary>Runs only when the shot killed the victim (server). E.g. Heal/Ammo on kill.</summary>
        public virtual void OnKill(in EffectContext ctx) { }
    }
}
