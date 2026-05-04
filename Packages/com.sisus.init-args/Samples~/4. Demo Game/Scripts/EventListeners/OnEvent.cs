using Sisus.Init;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.Scripting;

namespace Init.Demo
{
	/// <summary>
	/// Base class for components that invoke an <see cref="UnityEvent"/> in reaction
	/// to an <typeparamref name="TEvent"/> occurring.
	/// </summary>
	[RequireDerived]
	public abstract class OnEvent<TEvent> : MonoBehaviour<TEvent> where TEvent : IEvent
	{
		[SerializeField]
		private UnityEvent reaction = new UnityEvent();
		private TEvent @event = default;

		protected override void Init(TEvent @event) => this.@event = @event;
		private void OnEnable() => @event.AddListener(reaction.Invoke);
		private void OnDisable() => @event.RemoveListener(reaction.Invoke);
	}
}