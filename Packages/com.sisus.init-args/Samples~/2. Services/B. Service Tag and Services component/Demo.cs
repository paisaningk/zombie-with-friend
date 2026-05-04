using UnityEngine;

namespace Sisus.Init.Demos.Services
{
	/// <summary>
	/// Demo showcasing how components that derive from <see cref="MonoBehaviour{T}"/> can
	/// automatically receive global services configured using the [Service] attribute.
	/// </summary>
	/// <seealso cref="Movable"/>
	/// <seealso cref="MoveInputProvider"/>
	/// <seealso cref="TimeProvider"/>
	/// <seealso cref="Level"/>
	/// <seealso cref="MoveSettings"/>
	public class Demo : MonoBehaviour
	{
		void Start()
		{
			var movable = FindAnyObjectByType<Movable>();
			Debug.Log($"{movable.GetType().Name} was initialized using Inspector-configured services.", movable);
		}
	}
}