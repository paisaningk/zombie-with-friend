using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using Sisus.Init;
using UnityEngine;
using static Sisus.NullExtensions;
using Random = UnityEngine.Random;

namespace Init.Demo
{
    /// <summary>
    /// Class responsible for continuously spawning objects into the scene.
    /// </summary>
    public sealed class Spawner : IUpdate, IResettable, ICoroutines
    {
        private readonly Dictionary<GameObject, Vector2Int> aliveInstances = new();

        private readonly SpawnerSettings settings;
        private readonly ITrackable playerTrackable;
        private readonly ILevel level;
        private readonly ITimeProvider timeProvider;
        private readonly IBuilder<GameObject> gameObjectBuilder;

        private float nextEnemySpawnTime;

		public ICoroutineRunner CoroutineRunner { get; set; }

        /// <summary>
        /// Initializes a new instance of the <see cref="Spawner"/> class.
        /// </summary>
        public Spawner([DisallowNull] SpawnerSettings settings, [DisallowNull] ITrackable playerTrackable, [DisallowNull] ILevel level, [DisallowNull] ITimeProvider timeProvider, [DisallowNull] IBuilder<GameObject> gameObjectBuilder)
        {
            if(settings is null) throw new ArgumentNullException(nameof(settings));
            if(playerTrackable == Null) throw new ArgumentNullException(nameof(playerTrackable));
            if(level == Null) throw new ArgumentNullException(nameof(level));
            if(timeProvider == Null) throw new ArgumentNullException(nameof(timeProvider));
            if(gameObjectBuilder == Null) throw new ArgumentNullException(nameof(gameObjectBuilder));

            this.playerTrackable = playerTrackable;
            this.settings = settings;
            this.level = level;
            this.timeProvider = timeProvider;
            this.gameObjectBuilder = gameObjectBuilder;
        }

        /// <inheritdoc/>
        void IResettable.ResetState()
        {
            CoroutineRunner.StopAllCoroutines();

            foreach(var instance in aliveInstances.Keys)
            {
                gameObjectBuilder.Dispose(instance);
            }

            aliveInstances.Clear();
        }

        /// <inheritdoc/>
        void IUpdate.Update(float deltaTime)
        {
			if(timeProvider.Time < nextEnemySpawnTime || playerTrackable == NullOrInactive)
            {
                return;
            }

            nextEnemySpawnTime = GetNextSpawnTime();
			CoroutineRunner.StartCoroutine(Spawn());
        }

        private float GetNextSpawnTime()
			=> timeProvider.Time + Random.Range(settings.MinSpawnInterval, settings.MaxSpawnInterval);

        private IEnumerator Spawn()
        {
            Vector2Int position = GetRandomPosition();
            GameObject instance = gameObjectBuilder.Create((Vector2)position);
            aliveInstances.Add(instance, position);

            yield return timeProvider.WaitForSeconds(settings.LifeTime);

            aliveInstances.Remove(instance);
            gameObjectBuilder.Dispose(instance);
        }

        private Vector2Int GetRandomPosition()
        {
            RectInt bounds = level.Bounds;

            if(bounds.width == 0 && bounds.height == 0)
			{
                return bounds.position;
			}

            Vector2Int position;
            int tries = 0;
            const int MaxTries = 100;
            do
            {
                position = new Vector2Int(Random.Range(bounds.x, bounds.xMax),
                                            Random.Range(bounds.y, bounds.yMax));
            }
            while(!IsValidSpawnPosition(position) && ++tries < MaxTries);

            return position;
        }

        private Vector2Int GetPlayerPosition()
        {
            var position = playerTrackable.Position;
            return new Vector2Int(Mathf.RoundToInt(position.x), Mathf.RoundToInt(position.y));
        }

        private bool IsValidSpawnPosition(Vector2Int position)
        {
            float minDistance = settings.MinSpawnDistanceToOtherEntities;

            foreach(var instancePosition in aliveInstances.Values)
            {
                if(Vector2Int.Distance(instancePosition, position) < minDistance)
                {
                    return false;
                }
            }

            return Vector2Int.Distance(GetPlayerPosition(), position) >= minDistance;
        }
    }
}