using UnityEngine;
using Sisus.Init;
using UnityEngine.Events;

namespace Init.Demo
{
	/// <summary>
	/// A <see cref="GameObject"/> with the <see cref="Movable"/> component can move along
	/// the x and y axes within the <see cref="ILevel.Bounds">bounds</see> of the <see cref="ILevel"/>
	/// when it is given a non-zero <see cref="MoveDirection"/> value.
	/// </summary>
	[AddComponentMenu("Initialization/Demo/Movable")]
	public sealed class Movable : MonoBehaviour<IMoveSettings, ILevel, ITimeProvider>, IResettable, ITrackable
	{
		/// <inheritdoc/>
		public event UnityAction PositionChanged;

		private IMoveSettings settings;
		private ILevel level;
		private ITimeProvider timeProvider;

		/// <inheritdoc/>
		public Vector2 MoveDirection { get; set; }

		/// <inheritdoc/>
		public Vector2 Position => transform.position;

		/// <inheritdoc/>
		protected override void Init(IMoveSettings settings, ILevel level, ITimeProvider timeProvider)
		{
			this.settings = settings;
			this.level = level;
			this.timeProvider = timeProvider;
		}

		/// <inheritdoc/>
		void IResettable.ResetState()
		{
			transform.position = (Vector2)level.Bounds.min;
		}

		private void Update()
		{
			if(MoveDirection == Vector2.zero)
			{
				return;
			}

			float time = timeProvider.DeltaTime;
			float speed = settings.MoveSpeed;
			float distance = time * speed;

			Vector3 currentPosition = transform.position;
			Vector3 translation = MoveDirection * distance;
			Vector3 updatedPosition = currentPosition + translation;

			if(updatedPosition.x < level.Bounds.x)
			{
				updatedPosition.x = level.Bounds.x;
			}
			if(updatedPosition.x > level.Bounds.xMax)
			{
				updatedPosition.x = level.Bounds.xMax;
			}
			if(updatedPosition.y < level.Bounds.y)
			{
				updatedPosition.y = level.Bounds.y;
			}
			if(updatedPosition.y > level.Bounds.yMax)
			{
				updatedPosition.y = level.Bounds.yMax;
			}

			if(updatedPosition == currentPosition)
			{
				return;
			}

			transform.position = updatedPosition;

			PositionChanged?.Invoke();
		}

		private void OnDisable() => MoveDirection = Vector2.zero;
	}
}