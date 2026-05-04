using UnityEngine;
using Sisus.Init;

namespace Init.Demo
{
	/// <summary>
	/// The player can collect <see cref="ICollectable">ICollectables</see>
	/// by entering the radius of their trigger and its position can be tracked
	/// via the <see cref="Trackable"/> property.
	/// </summary>
	[Service(typeof(IPlayer), FindFromScene = true, LazyInit = true)]
	public sealed class Player : MonoBehaviour<ITrackable>, IPlayer
	{
		/// <summary>
		/// Returns an object that allows keeping track of the <see cref="ITrackable.Position">position</see> of the <see cref="Player"/>.
		/// </summary>
		public ITrackable Trackable { get; private set; }

		/// <inheritdoc/>
		protected override void Init(ITrackable trackable) => Trackable = trackable;

		private void OnTriggerEnter(Collider other)
		{
			if(other.TryGetComponent(out ICollectable collectable))
			{
				collectable.Collect();
			}
		}
	}
}