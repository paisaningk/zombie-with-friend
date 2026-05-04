using Sisus.Init;
using UnityEngine;
using UnityEngine.Events;

namespace Init.Demo
{
	/// <summary>
	/// Component that invokes a <see cref="UnityEvent"/> whenever the game is reset.
	/// </summary>
	[AddComponentMenu("Initialization/Demo/Resettable")]
	// Hide the Init section in the inspector and disable Null Argument Guard
	// and Wait For Services behaviour using the [Init] attribute.
	// The ability to inject the Text dependency is only meant to be used when
	// this component is initialized using code (e.g. to accomodate easy testing),
	// not when it is attached to a GameObject using the Inspector in Edit Mode.// this component is initialized using code, not when it is
	// attached to a GameObject using the Inspector in Edit Mode.
	[Init(Enabled = false)]
	public sealed class Resettable : MonoBehaviour<UnityEvent>, IResettable
	{
		[SerializeField, Tooltip("Event invoked whenever the game is reset")]
		private UnityEvent reset = new();

		/// <inheritdoc/>
		protected override void Init(UnityEvent reset) => this.reset = reset;

		/// <inheritdoc/>
		void IResettable.ResetState() => reset.Invoke();
	}
}