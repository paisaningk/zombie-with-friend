using UnityEngine;

namespace Sisus.Init.Demos.Initializers
{
	/// <summary>
	/// Provides a random float value between the specified min and max values.
	/// </summary>
	[ValueProviderMenu("Random", typeof(float))]
	public sealed class RandomFloat : ScriptableObject, IValueProvider<float>
	{
		[SerializeField] float min = 0f;
		[SerializeField] float max = 1f;

		public float Value => Random.Range(min, max);
	}
}