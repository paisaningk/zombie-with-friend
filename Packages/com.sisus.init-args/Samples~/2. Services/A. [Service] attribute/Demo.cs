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
		[SerializeField] Movable client = default;

		void Start()
		{
			// Clone the Movable component using the built-in Instantiate method.
			// The instance will automatically receive all the objects that it depends on during its initialization
			// because all of them have been registered as global services using the [Service] attribute.
			Movable movable = Instantiate(client);

			Debug.Log($"{movable.GetType().Name} was initialized using global services.", movable);
		}
	}

	// The defining type of the TimeProvider service is set to ITimeProvider, meaning that any components that depend on ITimeProvider will receive the TimeProvider service.
	// LazyInit is set to true so that the TimeProvider service will only be created when it is first needed by some client.
	[Service(typeof(ITimeProvider), LazyInit = true)]
	public class TimeProvider : ITimeProvider
	{
		public virtual float DeltaTime => Time.deltaTime;
	}

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