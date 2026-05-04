using Sisus.Init;

namespace Sisus.Init.Demos.Initializers
{
	/// <summary>
	/// Initializer for the <see cref="Movable"/> component.
	/// </summary>
	internal sealed class MovableInitializer : Initializer<Movable, TimeProvider, MoveInputProvider, Level, float>
	{
		#if UNITY_EDITOR
		/// <summary>
		/// This section can be used to customize how the Init arguments will be drawn in the Inspector.
		/// <para>
		/// The Init argument names shown in the Inspector will match the names of members defined inside this section.
		/// </para>
		/// <para>
		/// Any PropertyAttributes attached to these members will also affect the Init arguments in the Inspector.
		/// </para>
		/// </summary>
		private sealed class Init
		{
			public TimeProvider TimeProvider = default;
			public MoveInputProvider InputProvider = default;
			public Level Level = default;
			public float MoveSpeed = default;
		}
		#endif
	}
}
