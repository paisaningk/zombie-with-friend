using Sisus.Init;
using UnityEngine;

namespace Init.Demo
{
	/// <summary>
	/// An event that is invoked when the player character collects a collectable.
	/// <para>
	/// Whenever the event is <see cref="Trigger">triggered</see> all methods
	/// that are listening for the event are invoked.
	/// </para>
	/// </summary>
	[Service(typeof(ICollectedEvent), ResourcePath = "On Collected")]
	[CreateAssetMenu(fileName = "On Collected", menuName = "Init(args) Demo/Events/On Collected")]
	public sealed class CollectedEvent : Event, ICollectedEvent { }
}