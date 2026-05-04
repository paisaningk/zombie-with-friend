using UnityEngine;

namespace Sisus.Init.Demos.Initializers
{
	/// <summary>
	/// Represents a level in the game, defining the bounds inside which
	/// game objects can be positioned.
	/// </summary>
	public class Level : MonoBehaviour
	{
		[SerializeField] RectInt bounds = new(0, 0, 19, 19);
		public RectInt Bounds => bounds;
	}
}