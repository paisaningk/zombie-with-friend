using UnityEngine;
using Sisus.Init;

namespace Init.Demo
{
	/// <summary>
	/// Represents a level in the game, defining the bounds inside which
	/// game objects can be positioned.
	/// </summary>
	[Service(typeof(ILevel), FindFromScene = true, LazyInit = true)]
	public sealed class Level : MonoBehaviour<RectInt>, ILevel
	{
		/// <inheritdoc/>
		public RectInt Bounds { get; private set; }

		/// <inheritdoc/>
		/// <param name="bounds"> The position and size of the level. </param>
		protected override void Init(RectInt bounds) => Bounds = bounds;
	}
}