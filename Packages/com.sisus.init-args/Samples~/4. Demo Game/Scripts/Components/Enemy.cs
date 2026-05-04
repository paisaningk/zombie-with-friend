using UnityEngine;

namespace Init.Demo
{
	/// <summary>
	/// An enemy that kills the player object on collision.
	/// </summary>
	[AddComponentMenu("Initialization/Demo/Enemy")]
	public sealed class Enemy : MonoBehaviour, IDeadly { }
}
