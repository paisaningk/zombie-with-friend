using UnityEngine;

namespace Init.Demo
{
	/// <summary>
	/// Represents a level in the game.
	/// </summary>
	public interface ILevel
	{
		/// <summary>
		/// The position and size of the level.
		/// </summary>
		RectInt Bounds { get; }
	}
}