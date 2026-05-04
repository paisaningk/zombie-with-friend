using System.Linq;
using UnityEditor;
using UnityEngine;

namespace Sisus.Init.EditorOnly.Internal
{
	public sealed class Icons : ScriptableObject
	{
		private static Texture2D nullGuardPassedIcon;

		public static Texture2D NullGuardPassedIcon => nullGuardPassedIcon ??= LoadNullGuardPassedIcon();

		private static Texture2D LoadNullGuardPassedIcon()
		{
			var nullGuardPassedIconName = "NullGuardPassed@2x";
			if(EditorGUIUtility.isProSkin)
			{
				nullGuardPassedIconName = "d_" + nullGuardPassedIconName;
			}

			if (nullGuardPassedIcon && nullGuardPassedIcon.name == nullGuardPassedIconName)
			{
				return nullGuardPassedIcon;
			}

			var expectedAssetPath =  "Packages/com.sisus.init-args/Icons/" + nullGuardPassedIconName + ".png";
			nullGuardPassedIcon = AssetDatabase.LoadAssetAtPath<Texture2D>(expectedAssetPath);
			if (nullGuardPassedIcon)
			{
				return nullGuardPassedIcon;
			}

			var guid = AssetDatabase.FindAssets("t:Texture2D " + nullGuardPassedIconName).FirstOrDefault();
			if(guid is null)
			{
				const string FallbackIconName = "TestPassed";
				nullGuardPassedIcon = (Texture2D)EditorGUIUtility.IconContent(FallbackIconName).image;

				#if DEV_MODE
				Debug.LogWarning($"Icon '{nullGuardPassedIconName}' not found anywhere in the project. Using fallback '{FallbackIconName}'.");
				Debug.Assert(nullGuardPassedIcon);
				#endif

				return nullGuardPassedIcon;
			}

			var foundAtPath = AssetDatabase.GUIDToAssetPath(guid);
			nullGuardPassedIcon = AssetDatabase.LoadAssetAtPath<Texture2D>(foundAtPath);

			#if DEV_MODE
			Debug.LogWarning($"Icon not found at expected path '{expectedAssetPath}', but was found at '{foundAtPath}'.");
			Debug.Assert(nullGuardPassedIcon);
			#endif

			return nullGuardPassedIcon;
		}
	}
}