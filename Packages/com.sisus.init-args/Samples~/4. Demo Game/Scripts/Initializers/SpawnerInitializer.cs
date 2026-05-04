using Sisus.Init;
using UnityEngine;

namespace Init.Demo
{
	/// <summary>
	/// Initializer for <see cref="Spawner"/>.
	/// </summary>
	[AddComponentMenu("Initialization/Demo/Initializers/Spawner Initializer")]
	internal sealed class SpawnerInitializer : WrapperInitializer<SpawnerComponent, Spawner, SpawnerSettings, IPlayer, ILevel, ITimeProvider, IGameObjectBuilder>
	{
		/// <inheritdoc/>
		protected override Spawner CreateWrappedObject(SpawnerSettings spawnerSettings, IPlayer player, ILevel level, ITimeProvider timeProvider, IGameObjectBuilder gameObjectBuilder)
			=> new (spawnerSettings, player.Trackable, level, timeProvider, gameObjectBuilder);
	}
}