using FishNet.Object;
using UnityEngine;

namespace Enemies
{
    /// <summary>Melee walks up and hits; Ranged stops at range and fires a projectile.</summary>
    public enum EnemyAttackType { Melee, Ranged }

    /// <summary>
    /// Data-driven stats for one enemy kind (ScriptableObject). One asset per type
    /// (Runner / Tank / Ranger); an <see cref="Enemy"/> prefab references its asset. Kept as a
    /// single SO with an <see cref="EnemyAttackType"/> switch — MVP variety is small (2 melee +
    /// 1 ranged), so a polymorphic hierarchy would be over-engineering (see decision 0008).
    ///
    /// goldReward is intentionally absent — that arrives with currency (task 12).
    /// </summary>
    [CreateAssetMenu(menuName = "Enemies/Enemy Data", fileName = "EnemyData")]
    public class EnemyData : ScriptableObject
    {
        [Header("Identity")]
        public string displayName = "Enemy";

        [Header("Health / Movement")]
        public float maxHp = 50f;
        [Tooltip("Chase speed (fed to FollowerEntity.maxSpeed on the server).")]
        public float moveSpeed = 5f;

        [Header("Attack")]
        public EnemyAttackType attackType = EnemyAttackType.Melee;
        public float damage = 10f;
        [Tooltip("Max distance to the target at which this enemy can attack (and stops to attack).")]
        public float attackRange = 1.5f;
        [Tooltip("Attacks per second.")]
        public float attackRate = 1f;

        [Header("Ranged only")]
        [Tooltip("Projectile spawned by a Ranged enemy (server-spawned). Needs NetworkObject + NetworkProjectile.")]
        public NetworkObject projectilePrefab;
        public float projectileSpeed = 20f;

        /// <summary>Seconds between attacks.</summary>
        public float AttackCooldown => attackRate > 0f ? 1f / attackRate : 0f;

#if UNITY_EDITOR
        private void OnValidate()
        {
            if (maxHp < 1f) maxHp = 1f;
            if (attackRange < 0f) attackRange = 0f;
        }
#endif
    }
}
