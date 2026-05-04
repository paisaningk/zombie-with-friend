using UnityEngine;
using Sisus.Init;

namespace Init.Demo
{
	/// <summary>
	/// A component that can set the color of on <see cref="Renderer"/>
	/// attached to the same <see cref="GameObject"/>.
	/// </summary>
	[AddComponentMenu("Initialization/Demo/Colored")]
	[RequireComponent(typeof(Renderer))]
	public sealed class Colored : MonoBehaviour<Color>
	{
		/// <inheritdoc/>
		protected override void Init(Color color)
		{
			GetComponent<Renderer>().material.color = color;
		}
	}
}