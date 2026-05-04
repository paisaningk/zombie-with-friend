using UnityEngine;

namespace Sisus.Init.Demos.Instantiate
{
	[AddComponentMenu("Init(args)/Demos/Instantiate/Movable")]
	class Movable : MonoBehaviour<MoveInputProvider, TimeProvider, BoundsInt, float> // <- List objects that the component depends on here
	{
		MoveInputProvider inputProvider;
		TimeProvider timeProvider;
		float moveSpeed;
		BoundsInt levelBounds;

		Vector2 moveDirection;

		protected override void Init(MoveInputProvider inputProvider, TimeProvider timeProvider, BoundsInt levelBounds, float moveSpeed) // <- Receive dependencies here
		{
			this.inputProvider = inputProvider;
			this.timeProvider = timeProvider;
			this.levelBounds = levelBounds;
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

			setPosition.x = Mathf.Clamp(setPosition.x, levelBounds.x, levelBounds.xMax);
			setPosition.y = Mathf.Clamp(setPosition.y, levelBounds.y, levelBounds.yMax);
			if(setPosition != currentPosition)
			{
				transform.position = setPosition;
			}
		}

		private void OnDisable()
		{
			moveDirection = Vector2.zero;
			inputProvider.MoveInputChanged -= OnMoveInputChanged;
		}

		private void OnMoveInputChanged(Vector2 moveInput) => moveDirection = moveInput;
	}
}