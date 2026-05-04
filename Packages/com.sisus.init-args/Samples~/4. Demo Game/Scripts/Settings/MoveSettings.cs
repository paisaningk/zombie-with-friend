using UnityEngine;

namespace Init.Demo
{
	/// <summary>
	/// ScriptableObject asset that holds settings data related to movement.
	/// </summary>
	public sealed class MoveSettings : ScriptableObject, IMoveSettings
	{
		[SerializeField]
		private float moveSpeed = 12f;

		/// <inheritdoc/>
		public float MoveSpeed => moveSpeed;
	}
}