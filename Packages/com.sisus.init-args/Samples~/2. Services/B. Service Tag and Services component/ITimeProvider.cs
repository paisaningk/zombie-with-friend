using UnityEngine;

namespace Sisus.Init.Demos.Services
{
	/// <summary>
	/// Provides information about how much time in seconds has passed since the last frame.
	/// <para>
	/// A simple wrapper around <see cref="Time.deltaTime"/> to make it easier to test code that depends on time.
	/// </para>
	/// </summary>
	public interface ITimeProvider
	{
		public float DeltaTime { get; }
	}
}