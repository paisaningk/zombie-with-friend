using UnityEngine;
using Sisus.Init;

namespace Init.Demo
{
	/// <summary>
	/// <see cref="Initializer{,}"/> for the <see cref="LookAt"/> component.
	/// </summary>
	[AddComponentMenu("Initialization/Demo/Initializers/Look At Initializer")]
    internal sealed class LookAtInitializer : Initializer<LookAt, ITrackable> { }
}