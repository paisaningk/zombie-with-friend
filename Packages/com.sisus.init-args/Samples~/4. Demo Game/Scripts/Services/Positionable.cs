using UnityEngine;

namespace Init.Demo
{
	public sealed class Positionable : MonoBehaviour
	{
		public void SetPosition(Vector3 value) => transform.localPosition = value;
		
		public void SetPositionX(float value)
		{
			var position = transform.localPosition;
			position.x = value;
			transform.localPosition = position;
		}

		public void SetPositionY(float value)
		{
			var position = transform.localPosition;
			position.y = value;
			transform.localPosition = position;
		}

		public void SetPositionZ(float value)
		{
			var position = transform.localPosition;
			position.z = value;
			transform.localPosition = position;
		}
	}
}
