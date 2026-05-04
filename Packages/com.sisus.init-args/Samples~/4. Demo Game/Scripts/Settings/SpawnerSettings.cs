using System;
using UnityEngine;

namespace Init.Demo
{
    /// <summary>
    /// Settings for a <see cref="Spawner"/> object.
    /// </summary>
    [Serializable]
    public sealed class SpawnerSettings
    {
        [SerializeField]
        private float minSpawnInterval = 1f;

        [SerializeField]
        private float maxSpawnInterval = 6f;

        [SerializeField]
        private float lifeTime = 5f;

        [SerializeField]
        private float minSpawnDistanceToOtherEntities = 3f;

        /// <summary>
        /// The minimum number of seconds between two objects being spawned.
        /// </summary>
        public float MinSpawnInterval => minSpawnInterval;
        
        /// <summary>
        /// The maximum number of seconds between two enemies being spawned.
        /// </summary>
        public float MaxSpawnInterval => maxSpawnInterval;

        /// <summary>
        /// The number of seconds that an enemy will exist before despawning.
        /// </summary>
        public float LifeTime => lifeTime;

        /// <summary>
        /// The minimum distance that a spawned enemy should have to the player as well as other
        /// enemies that exist in the scene.
        /// </summary>
        public float MinSpawnDistanceToOtherEntities => minSpawnDistanceToOtherEntities;

        /// <summary>
        /// Initializes a new instance of the <see cref="SpawnerSettings"/> class with default settings.
        /// </summary>
        public SpawnerSettings() { }

        /// <summary>
        /// Initializes a new instance of the <see cref="SpawnerSettings"/> class.
        /// </summary>
        /// <param name="minSpawnInterval"> The minimum number of seconds between two objects being spawned. </param>
        /// <param name="maxSpawnInterval"> The maximum number of seconds between two enemies being spawned. </param>
        /// <param name="lifeTime"> The number of seconds that an enemy will exist before despawning. </param>
        /// <param name="minSpawnDistanceToOtherEntities">
        /// The minimum distance that a spawned enemy should have to the player as well as other enemies that exist in the scene.
        /// </param>
        public SpawnerSettings(float minSpawnInterval, float maxSpawnInterval, float lifeTime, float minSpawnDistanceToOtherEntities)
        {
            this.minSpawnInterval = minSpawnInterval;
            this.maxSpawnInterval = maxSpawnInterval;
            this.lifeTime = lifeTime;
            this.minSpawnDistanceToOtherEntities = minSpawnDistanceToOtherEntities;
        }
    }
}