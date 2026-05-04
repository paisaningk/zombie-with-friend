using UnityEngine;

namespace Sisus.Init.Demos.Services
{
	public class TimeProvider : ScriptableObject, ITimeProvider
	{
		public virtual float DeltaTime => Time.deltaTime;
	}
}