using Sisus.Init;
using UnityEngine;

namespace Init.Demo
{
	/// <summary>
	/// <see cref="Initializer{,}"/> for the <see cref="Killable"/> component.
	/// </summary>
	[AddComponentMenu("Initialization/Demo/Initializers/Killable Initializer")]
    public sealed class KillableInitializer : Initializer<Killable, IEventTrigger>
	{
		#if UNITY_EDITOR
		#pragma warning disable CS0649
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
			public IEventTrigger onKilled;
		}
		#pragma warning restore CS0649
		#endif
	}
}
