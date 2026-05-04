using UnityEngine;

namespace Sisus.Init.Demos.Services
{
	/// <summary>
	/// Holds movement-related configuration.
	/// </summary>
	[Service(ResourcePath = "InitArgs.Demos.Services/MoveSettings", LazyInit = true)]
	public sealed class MoveSettings : ScriptableObject<float>
	{
		[SerializeField]
		private float moveSpeed = 12f;

		public float MoveSpeed => moveSpeed;

		// Allow passing in a custom move speed during initialization to facilitate testing.
		protected override void Init(float moveSpeed) => this.moveSpeed = moveSpeed;
	}
}