#if UNITY_EDITOR
using Sirenix.OdinInspector.Editor;
using Sirenix.Utilities.Editor; // SirenixEditorFields
using UnityEditor;
using UnityEngine;

// Global Odin drawer for every ScriptableObject reference: shows Browse / + New
// buttons when the field is null, and defers to the rest of the drawer chain
// ([InlineEditor]/default) when a value is assigned.
[DrawerPriority(DrawerPriorityLevel.WrapperPriority)] // run before InlineEditor so we can wrap it
public class CreateNewSODrawer<T> : OdinValueDrawer<T> where T : ScriptableObject
{
    private const string DefaultFolder = "Assets/ScriptableObject";

    protected override void DrawPropertyLayout(GUIContent label)
    {
        // Assigned: let the rest of the chain ([InlineEditor]/default) draw it.
        if (ValueEntry.SmartValue != null)
        {
            CallNextDrawer(label);
            return;
        }

        // Null: object field + Browse + New on one row.
        EditorGUILayout.BeginHorizontal();

        ValueEntry.SmartValue = (T)SirenixEditorFields.UnityObjectField(
            label, ValueEntry.SmartValue, typeof(T), false);

        if (GUILayout.Button("Browse", GUILayout.Width(58)))
            ShowBrowse();

        if (GUILayout.Button("+ New", GUILayout.Width(50)))
            CreateNew();

        EditorGUILayout.EndHorizontal();
    }

    private void ShowBrowse()
    {
        var entry = ValueEntry; // capture for the menu callback
        var menu = new GenericMenu();

        foreach (var guid in AssetDatabase.FindAssets($"t:{typeof(T).Name}"))
        {
            var asset = AssetDatabase.LoadAssetAtPath<T>(AssetDatabase.GUIDToAssetPath(guid));
            if (asset == null) continue;

            menu.AddItem(new GUIContent(asset.name), false,
                () => { entry.SmartValue = asset; entry.ApplyChanges(); });
        }

        if (menu.GetItemCount() == 0)
            menu.AddDisabledItem(new GUIContent("No assets found"));

        menu.ShowAsContext();
    }

    private void CreateNew()
    {
        var path = EditorUtility.SaveFilePanelInProject(
            "Create " + typeof(T).Name, typeof(T).Name, "asset", "Choose where to save", DefaultFolder);
        if (string.IsNullOrEmpty(path)) return;

        var asset = ScriptableObject.CreateInstance<T>();
        AssetDatabase.CreateAsset(asset, path);
        AssetDatabase.SaveAssets();

        ValueEntry.SmartValue = asset;
        GUIUtility.ExitGUI(); // bail layout cleanly after the modal dialog
    }
}
#endif
