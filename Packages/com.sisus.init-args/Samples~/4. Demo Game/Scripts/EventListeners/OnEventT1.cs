using Sisus.Init;
using UnityEngine;
using UnityEngine.Events;

namespace Init.Demo
{
	/// <summary>
	/// Base class for components that invoke an <see cref="UnityEvent{TArgument}"/> in reaction
	/// to an <typeparamref name="TEvent"/> occurring.
	/// </summary>
	public abstract class OnEvent<TEvent, TArgument> : MonoBehaviour<TEvent> where TEvent : IEvent<TArgument>
	{
		[SerializeField]
		private UnityEvent<TArgument> reaction = new UnityEvent<TArgument>();
		private TEvent @event = default;

		protected override void Init(TEvent @event) => this.@event = @event;
		private void OnEnable() => @event.AddListener(reaction.Invoke);
		private void OnDisable() => @event.RemoveListener(reaction.Invoke);
	}
}