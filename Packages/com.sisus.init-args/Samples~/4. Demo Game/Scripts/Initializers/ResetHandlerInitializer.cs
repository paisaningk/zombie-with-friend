using Sisus.Init;
using UnityEngine;
using UnityEngine.Scripting;

namespace Init.Demo
{
	/// <summary>
	/// <see cref="Initializer{,}"/> for the <see cref="ResetHandler"/> component.
	/// </summary>
	[AddComponentMenu("Initialization/Demo/Initializers/Reset Handler Initializer"), Preserve]
	internal sealed class ResetHandlerInitializer : Initializer<ResetHandler, IInputManager> { }
}