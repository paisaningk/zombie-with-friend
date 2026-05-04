using UnityEngine;

namespace Sisus.Init.Demos.Initializers
{
	[AddComponentMenu("Init(args)/Demos/Initializers/Movable")]
	public class Movable : MonoBehaviour<TimeProvider, MoveInputProvider, Level, float> // <- List objects that the component depends on here
	{
		MoveInputProvider inputProvider;
		TimeProvider timeProvider;
		float moveSpeed;
		Level level;

		Vector2 moveDirection;

		protected override void Init(TimeProvider timeProvider, MoveInputProvider inputProvider, Level level, float moveSpeed) // <- Receive dependencies here
		{
			this.inputProvider = inputProvider;
			this.timeProvider = timeProvider;
			this.level = level;
			this.moveSpeed = moveSpeed;
		}

		// Services can be safely used during the OnAwake, OnEnable events
		void OnEnable() => inputProvider.MoveInputChanged += OnMoveInputChanged;

		void Update()
		{
			if(moveDirection == Vector2.zero)
			{
				return;
			}

			float time = timeProvider.DeltaTime;
			float distance = time * moveSpeed;

			Vector3 currentPosition = transform.position;
			Vector3 translation = moveDirection * distance;
			Vector3 setPosition = currentPosition + translation;

			setPosition.x = Mathf.Clamp(setPosition.x, level.Bounds.x, level.Bounds.xMax);
			setPosition.y = Mathf.Clamp(setPosition.y, level.Bounds.y, level.Bounds.yMax);
			if(setPosition != currentPosition)
			{
				transform.position = setPosition;
			}
		}

		void OnDisable()
		{
			moveDirection = Vector2.zero;
			inputProvider.MoveInputChanged -= OnMoveInputChanged;
		}

		void OnMoveInputChanged(Vector2 moveInput) => moveDirection = moveInput;
	}
}