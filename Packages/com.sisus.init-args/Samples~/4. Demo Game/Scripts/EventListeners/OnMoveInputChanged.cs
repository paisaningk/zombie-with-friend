using UnityEngine;
using UnityEngine.Events;

namespace Init.Demo
{
	/// <summary>
	/// Component that invokes an <see cref="UnityEvent"/> whenever the
	/// <see cref="IMoveInputChangedEvent"/> event is triggered.
	/// </summary>
	[AddComponentMenu("Initialization/Demo/Events/On Move Input Changed")]
	public sealed class OnMoveInputChanged : OnEvent<IMoveInputChangedEvent, Vector2> { }
}