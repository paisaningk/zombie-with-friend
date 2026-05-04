#pragma warning disable CS0414

using UnityEngine;
using Object = UnityEngine.Object;

namespace Sisus.Init.Internal
{
	// Base class for all Initializers; targeted by InitializerEditor.
	public abstract class Initializer : MonoBehaviour
	{
		internal abstract Object GetTarget();

		#if !UNITY_2022_2_OR_NEWER
		// Helps code compile in older Unity versions where destroyCancellationToken is not available.
		public System.Threading.CancellationToken destroyCancellationToken => System.Threading.CancellationToken.None;
		#endif

		private protected void DestroySelfIfNotAsset()
		{
			if(!this)
			{
				return;
			}

			#if UNITY_EDITOR
			if(gameObject.IsAsset(resultIfSceneObjectInEditMode: true))
			{
				return;
			}
			#endif

			Destroy(this);
		}
	}
}