using UnityEngine;
using static Sisus.Init.ValueProviders.ValueProviderUtility;

namespace Sisus.Init.ValueProviders
{
	/// <summary>
	/// Returns the current <see cref="Camera.main">main camera</see> with the tag "MainCamera".
	/// <para>
	/// Can be used to retrieve an Init argument at runtime.
	/// </para>
	/// </summary>
	#if !INIT_ARGS_DISABLE_VALUE_PROVIDER_MENU_ITEMS
	[ValueProviderMenu(MENU_NAME, typeof(Camera), Order = 10)]
	#endif
	#if DEV_MODE
	[CreateAssetMenu(fileName = MENU_NAME, menuName = CREATE_ASSET_MENU_GROUP + MENU_NAME)]
	#endif
	internal sealed class MainCamera : ScriptableObject, IValueProvider<Camera>
	#if UNITY_EDITOR
	, INullGuard
	#endif
	{
		private const string MENU_NAME = "Main Camera";

		/// <summary>
		/// Returns the current <see cref="Camera.main">main camera</see>.
		/// <para>
		/// This is the first enabled Camera component found in the currently
		/// loaded scenes that is tagged "MainCamera".
		/// </para>
		/// </summary>
		public Camera Value => Camera.main;

		#if UNITY_EDITOR
		NullGuardResult INullGuard.EvaluateNullGuard(Component client)
		{
			if(!Camera.main)
			{
				if(Application.isPlaying)
				{
					return NullGuardResult.Error("No camera with the tag 'MainCamera' is currently loaded and active.");
				}

				return NullGuardResult.Warning("No camera with the tag 'MainCamera' is currently loaded and active (but one could become available at runtime).");
			}

			return NullGuardResult.Passed;
		}
		#endif
	}
}