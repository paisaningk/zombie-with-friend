using System;
using UnityEngine;

namespace Sisus.Init.Demos.GameObjectT
{
	/// <summary>
	/// Demo showcasing how components that derive from <see cref="MonoBehaviour{T}"/> can be
	/// initialized with all the objects that they depend on using the AddComponent extension methods.
	/// </summary>
	/// <seealso cref="Movable"/>
	public class Demo : MonoBehaviour
	{
		[SerializeField] Mesh mesh = default;
		[SerializeField] Material material = default;
		[SerializeField] BoundsInt levelBounds = new(0, 0, 0, 19, 19, 0);
		[SerializeField] float moveSpeed = 12f;
		[SerializeField] MoveInputProvider inputProvider = default;
		
		readonly TimeProvider timeProvider = new();

		private void Start()
		{
			// Enable the input provider to make it start receiving callbacks during Update events.
			inputProvider.SetEnabled(true);
			
			Movable movable = new GameObject<Movable, MeshFilter, MeshRenderer>("Movable")
					// Pass all the objects that the Movable component depends on to its Init method
					.Init1(inputProvider, timeProvider, levelBounds, moveSpeed)
					// Use an anonymous expression to initialize the MeshFilter component
					.Init2(meshFilter => meshFilter.mesh = mesh)
					// Use an anonymous expression to initialize the MeshRenderer component
					.Init3(meshRenderer => meshRenderer.sharedMaterial = material);
			
			Debug.Log($"{movable.GetType().Name} was initialized using new GameObject.", movable);
		}
	}
	
	/// <summary>
	/// Provides information about how much time in seconds has passed since the last frame.
	/// <para>
	/// A simple wrapper around <see cref="Time.deltaTime"/> to make it easier to test code that depends on time.
	/// </para>
	/// </summary>
	public class TimeProvider
	{
		public virtual float DeltaTime => Time.deltaTime;
	}
	
	/// <summary>
	/// Raises the <see cref="MoveInputChanged"/> event whenever move input changes.
	/// </summary>
	[Serializable]
	public class MoveInputProvider : IUpdate // <- IUpdate can be implemented to receive a callback every frame
	{
		public event Action<Vector2> MoveInputChanged;

		[SerializeField] KeyCode moveLeftKey = KeyCode.LeftArrow;
		[SerializeField] KeyCode moveRightKey = KeyCode.RightArrow;
		[SerializeField] KeyCode moveDownKey = KeyCode.DownArrow;
		[SerializeField] KeyCode moveUpKey = KeyCode.UpArrow;

		Vector2 previousMoveInput = Vector2.zero;

		public MoveInputProvider(KeyCode moveLeftKey, KeyCode moveRightKey, KeyCode moveDownKey, KeyCode moveUpKey)
		{
			this.moveLeftKey = moveLeftKey;
			this.moveRightKey = moveRightKey;
			this.moveDownKey = moveDownKey;
			this.moveUpKey = moveUpKey;
		}

		public void Update(float deltaTime)
		{
			Vector2 moveInput = GetMoveInput();
			if(previousMoveInput == moveInput)
			{
				return; 
			}

			previousMoveInput = moveInput;
			MoveInputChanged?.Invoke(moveInput);
			
			Vector2 GetMoveInput()
			{
				var input = Vector2.zero;
				input.x += Input.GetKey(moveLeftKey) ? -1f : Input.GetKey(moveRightKey) ? 1f : 0f;
				input.y += Input.GetKey(moveDownKey) ? -1f : Input.GetKey(moveUpKey) ? 1f : 0f;
				return input;
			}
		}
	}
}