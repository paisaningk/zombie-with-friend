using UnityEditor;
using UnityEngine;

namespace Sisus.Init.EditorOnly.Internal
{
	internal static class ValueProviderMenuItems
	{
		private const string DeleteSubAsset = "CONTEXT/ScriptableObject/Delete";

		[MenuItem(DeleteSubAsset, priority = 1500)]
		private static void DeleteSubAssetMenuItem(MenuCommand command)
			=> ValueProviderEditorUtility.DeleteSubAsset(command.context as ScriptableObject, saveToDisk: true);

		[MenuItem(DeleteSubAsset, priority = 1500, validate = true)]
		private static bool DeleteSubAssetMenuItemClickable(MenuCommand command)
			=> command.context && command.context is ScriptableObject scriptableObject && AssetDatabase.IsSubAsset(scriptableObject);
	}
}