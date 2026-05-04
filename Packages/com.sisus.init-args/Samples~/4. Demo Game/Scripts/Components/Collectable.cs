using Sisus.Init;
using UnityEngine;

namespace Init.Demo
{
	/// <summary>
	/// An object that can be collected by the player object.
	/// </summary>
	[AddComponentMenu("Initialization/Demo/Collectable")]
	public sealed class Collectable : MonoBehaviour<IEventTrigger>, ICollectable
	{
		[Tooltip("Event invoked when the object is collected.")]
		private IEventTrigger onCollected;

		protected override void Init(IEventTrigger onCollected) => this.onCollected = onCollected;

		/// <inheritdoc/>
		public void Collect()
		{
			onCollected.Trigger();
			gameObject.SetActive(false);
		}
	}
}
