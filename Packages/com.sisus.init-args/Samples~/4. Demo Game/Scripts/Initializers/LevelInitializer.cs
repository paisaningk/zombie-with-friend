using Sisus.Init;
using UnityEngine;

namespace Init.Demo
{
	/// <summary>
	/// <see cref="Initializer{,}"/> for the <see cref="Level"/> component.
	/// </summary>
	[AddComponentMenu("Initialization/Demo/Initializers/Level Initializer")]
	internal sealed class LevelInitializer : Initializer<Level, RectInt>
	{
		#if UNITY_EDITOR
		protected override void OnReset(ref RectInt bounds) => bounds = new RectInt(0, 0, 19, 19);
		#endif
	}
}
