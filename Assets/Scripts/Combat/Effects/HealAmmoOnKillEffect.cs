using UnityEngine;

namespace Combat.Effects
{
    /// <summary>
    /// Vampiric / scavenger rounds (decision 0016, W5 example 3): killing a target heals the shooter and
    /// tops up the active magazine. Server-only, and it fires on the <see cref="WeaponEffect.OnKill"/>
    /// hook — which only runs because <c>IHitReceiver.ReceiveHit</c> reports the kill back locally
    /// (no attacker threading through the damage pipeline).
    /// </summary>
    [CreateAssetMenu(menuName = "Weapons/Effects/Heal + Ammo on Kill", fileName = "HealAmmoOnKillEffect")]
    public class HealAmmoOnKillEffect : WeaponEffect
    {
        [SerializeField] private float _heal = 10f;
        [SerializeField] private int _ammo = 3;

        public override void OnKill(in EffectContext ctx)
        {
            if (ctx.Weapon == null) return;
            ctx.Weapon.EffectHealShooter(_heal);
            ctx.Weapon.EffectRefillAmmo(_ammo);
        }
    }
}
