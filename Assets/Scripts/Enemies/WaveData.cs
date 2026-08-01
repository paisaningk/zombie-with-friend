using System;
using FishNet.Object;
using UnityEngine;

namespace Enemies
{
    /// <summary>One entry in a wave's spawn pool: a type, its guaranteed minimum, and its
    /// relative weight for filling the wave's remaining budget.</summary>
    [Serializable]
    public class WeightedEnemy
    {
        [Tooltip("Enemy prefab (NetworkObject) to spawn. Must be a registered enemy prefab.")]
        public NetworkObject prefab;

        [Tooltip("Relative weight when randomly filling the wave budget beyond the guaranteed minimums. " +
                 "0 = only ever spawns its minCount (guaranteed-only type).")]
        public float weight = 1f;

        [Tooltip("Guaranteed count of this type per wave (spawned regardless of weight roll).")]
        public int minCount = 0;
    }

    /// <summary>
    /// One wave. Composition is resolved once at wave start:
    ///   1. every type's <see cref="WeightedEnemy.minCount"/> is placed (guaranteed),
    ///   2. the rest of <see cref="totalCount"/> is filled by weighted-random draw from the pool,
    ///   3. the resulting multiset is shuffled into a spawn order.
    /// If the minimums already meet/exceed <see cref="totalCount"/>, the minimums win.
    /// Enemies then trickle in on <see cref="spawnInterval"/>, never exceeding <see cref="maxAlive"/>
    /// concurrently — a slot freed by a death is refilled from the remaining budget.
    /// </summary>
    [Serializable]
    public class Wave
    {
        public WeightedEnemy[] pool = Array.Empty<WeightedEnemy>();

        [Tooltip("Total enemies this wave (guaranteed minimums count toward it; minimums win if they exceed it).")]
        public int totalCount = 5;

        [Tooltip("Max enemies alive at once. A death frees a slot that gets refilled from the remaining budget.")]
        public int maxAlive = 6;

        [Tooltip("Seconds between spawns while refilling toward maxAlive.")]
        public float spawnInterval = 1f;
    }

    /// <summary>
    /// The whole campaign — an ordered list of waves — as one asset. Level-agnostic: the same
    /// asset can run on any map (spawn POINTS are per-scene Transforms on the WaveManager).
    /// Referenced by <c>Game.WaveManager</c> (see decision 0009).
    /// </summary>
    [CreateAssetMenu(menuName = "Enemies/Wave Data", fileName = "WaveData")]
    public class WaveData : ScriptableObject
    {
        public Wave[] waves = Array.Empty<Wave>();

        public int WaveCount => waves != null ? waves.Length : 0;
    }
}
