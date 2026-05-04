using UnityEngine;
using Sisus.Init;

namespace Init.Demo
{
	/// <summary>
	/// Continuously moves towards a target.
	/// </summary>
	[AddComponentMenu("Initialization/Demo/Chase")]
	public sealed class Chase : MonoBehaviour<ITrackable, Movable>
	{
		private ITrackable target;
		private Movable movable;

		/// <inheritdoc/>
		protected override void Init(ITrackable target, Movable movable)
		{
			this.target = target;
			this.movable = movable;
		}

		private void Update()
		{
			if(target == NullOrInactive)
			{
				movable.MoveDirection = Vector2.zero;
				return;
			}

			movable.MoveDirection = (target.Position - (Vector2)transform.position).normalized;
		}
	}
}