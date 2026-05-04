using System;
using Sisus.Init;
using UnityEngine;
using UnityEngine.Events;

namespace Init.Demo
{
	/// <summary>
	/// Manager responsible for detecting changes in user input and broadcasting events in reponse.
	/// </summary>
	[Service(typeof(IInputManager))]
	public sealed class InputManager : IInputManager, IUpdate
	{
		/// <inheritdoc/>
		public event Action ResetInputGiven;

		/// <inheritdoc/>
		public event UnityAction<Vector2> MoveInputChanged
		{
			add => moveInputChangedEvent.AddListener(value);
			remove => moveInputChangedEvent.RemoveListener(value);
		}

		private readonly KeyCode resetKey;
		private readonly KeyCode moveLeftKey;
		private readonly KeyCode moveRightKey;
		private readonly KeyCode moveDownKey;
		private readonly KeyCode moveUpKey;
		private readonly MoveInputChangedEvent moveInputChangedEvent;

		private Vector2 previousMoveInput = Vector2.zero;

		public InputManager(IInputSettings inputSettings, MoveInputChangedEvent moveInputChangedEvent)
		{
			resetKey = inputSettings.ResetKey;
			moveLeftKey = inputSettings.MoveLeftKey;
			moveRightKey = inputSettings.MoveRightKey;
			moveDownKey = inputSettings.MoveDownKey;
			moveUpKey = inputSettings.MoveUpKey;
			this.moveInputChangedEvent = moveInputChangedEvent;
		}

		bool updateCalled;

		/// <inheritdoc/>
		public void Update(float deltaTime)
		{
			if(!updateCalled)
			{
				updateCalled = true;
			}

			if(Input.GetKeyDown(resetKey))
			{
				ResetInputGiven?.Invoke();
			}

			Vector2 moveInput = GetMoveInput();
			if(previousMoveInput == moveInput)
			{
				return; 
			}

			previousMoveInput = moveInput;
			moveInputChangedEvent.Trigger(moveInput);
		}

		private Vector2 GetMoveInput()
		{
			Vector2 input = Vector2.zero;

			if(Input.GetKey(moveRightKey))
			{
				input.x += 1f;
			}

			if(Input.GetKey(moveLeftKey))
			{
				input.x -= 1f;
			}

			if(Input.GetKey(moveUpKey))
			{
				input.y += 1f;
			}

			if(Input.GetKey(moveDownKey))
			{
				input.y -= 1f;
			}

			return input;
		}
	}
}