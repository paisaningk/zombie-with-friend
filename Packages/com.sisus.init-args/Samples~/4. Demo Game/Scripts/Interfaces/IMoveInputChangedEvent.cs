using UnityEngine;

namespace Init.Demo
{
	/// <summary>
	/// Represents an event that is invoked when move input given for the the player character has changed.
	/// </summary>
	public interface IMoveInputChangedEvent : IEvent<Vector2> { }
}