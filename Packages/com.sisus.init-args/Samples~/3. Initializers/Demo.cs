using UnityEngine;

namespace Sisus.Init.Demos.Initializers
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
			// Clone the Movable component using the built-in Instantiate method.
			// The instance will automatically receive all the objects that it depends on during its initialization
			// because all of them have been registered as global services using the [Service] attribute.
			Movable movable = FindAnyObjectByType<Movable>();

			Debug.Log($"{movable.GetType().Name} was initialized using global services.", movable);
		}
	}

	[Service]
	public class TimeProvider
	{
		public virtual float DeltaTime => Time.deltaTime;
	}
}