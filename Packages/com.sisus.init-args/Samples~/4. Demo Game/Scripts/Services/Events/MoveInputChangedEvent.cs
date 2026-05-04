using Sisus.Init;
using UnityEngine;

namespace Init.Demo
{
	/// <summary>
	/// An event that is invoked when move input given for the player character has changed.
	/// <para>
	/// Whenever the event is <see cref="Event.Trigger">triggered</see> all methods
	/// that are listening for the event are invoked.
	/// </para>
	/// </summary>
	[Service(typeof(IMoveInputChangedEvent), typeof(MoveInputChangedEvent), ResourcePath = Name)]
	[CreateAssetMenu(fileName = Name, menuName = CreateAssetMenuDirectory + Name)]
	public sealed class MoveInputChangedEvent : Event<Vector2>, IMoveInputChangedEvent
	{
		private const string Name = "On Move Input Changed";
	}
}