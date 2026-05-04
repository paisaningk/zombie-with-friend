using Sisus.Init;
using UnityEngine;
using UnityEngine.Events;

namespace Init.Demo
{
	/// <summary>
	/// Component that invokes an <see cref="UnityEvent"/> whenever the
	/// <see cref="IPlayerKilledEvent"/> event is triggered.
	/// </summary>
	[AddComponentMenu("Initialization/Demo/Events/On Player Killed")]
	// The Init section can be hidden for a component using the [Init] attribute.
	// This can be desirable when a component only depends on global services
	// to minimize Inspector clutter.
	[Init(HideInInspector = true)]
	public sealed class OnPlayerKilled : OnEvent<IPlayerKilledEvent> { }
}