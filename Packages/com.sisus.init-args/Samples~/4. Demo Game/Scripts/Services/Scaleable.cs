using UnityEngine;

namespace Init.Demo
{
	public sealed class Scaleable : MonoBehaviour
	{
		public void SetScale(Vector3 value) => transform.localPosition = value;
		
		public void SetScaleX(float value)
		{
			var scale = transform.localScale;
			scale.x = value;
			transform.localScale = scale;
		}

		public void SetScaleY(float value)
		{
			var scale = transform.localScale;
			scale.y = value;
			transform.localScale = scale;
		}

		public void SetScaleZ(float value)
		{
			var scale = transform.localScale;
			scale.z = value;
			transform.localScale = scale;
		}
	}
}
