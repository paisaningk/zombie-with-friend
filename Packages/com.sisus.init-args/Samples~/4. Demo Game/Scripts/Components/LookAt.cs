using UnityEngine;
using Sisus.Init;

namespace Init.Demo
{
	/// <summary>
	/// Continuously turns to face a target.
	/// </summary>
	[AddComponentMenu("Initialization/Demo/Look At")]
	public sealed class LookAt : MonoBehaviour<ITrackable>
	{
		private ITrackable target;

		/// <inheritdoc/>
		protected override void Init(ITrackable target) => this.target = target;

		private void OnEnable()
		{
			target.PositionChanged += LookAtTarget;
		}

		private void OnDisable()
		{
			if(target != Null)
			{
				target.PositionChanged -= LookAtTarget;
			}
		}

		private void LookAtTarget()
		{
			if(target != NullOrInactive)
			{
				transform.LookAt(target.Position);
			}
		}
	}
}