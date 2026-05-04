using System.Diagnostics.CodeAnalysis;
using Sisus.Init.Internal;
using Sisus.Shared.EditorOnly;
using UnityEditor;
using UnityEngine;

#if DEV_MODE && DEBUG && !INIT_ARGS_DISABLE_PROFILING
using Unity.Profiling;
#endif

namespace Sisus.Init.EditorOnly
{
	[InitializeOnLoad]
	internal static class IdTagDrawer
	{
		static IdTagDrawer()
		{
			ComponentHeader.BeforeHeaderGUI -= OnBeforeComponentHeaderGUI;
			ComponentHeader.BeforeHeaderGUI += OnBeforeComponentHeaderGUI;
			ComponentHeader.AfterHeaderGUI -= OnAfterComponentHeaderGUI;
			ComponentHeader.AfterHeaderGUI += OnAfterComponentHeaderGUI;
			Editor.finishedDefaultHeaderGUI -= OnAfterInspectorRootEditorHeaderGUI;
			Editor.finishedDefaultHeaderGUI += OnAfterInspectorRootEditorHeaderGUI;
		}

		private static void OnAfterInspectorRootEditorHeaderGUI(Editor editor)
		{
			if(editor.target is GameObject)
			{
				// Handle InspectorWindow
				OnAfterGameObjectHeaderGUI(editor);
			}
		}

		private static void OnAfterGameObjectHeaderGUI([DisallowNull] Editor gameObjectEditor)
		{
			#if DEV_MODE && DEBUG && !INIT_ARGS_DISABLE_PROFILING
			using var x = beforeHeaderGUIMarker.Auto();
			#endif

			var gameObject = gameObjectEditor.target as GameObject;
			if(!TryGetRefTag(gameObject, gameObject, out var referenceable))
			{
				return;
			}

			var refLabel = GetRefLabel(referenceable);
			bool isAddressableAssetOrPrefabInstance = PrefabUtility.IsPartOfPrefabInstance(gameObject);
			#if UNITY_ADDRESSABLES_1_17_4_OR_NEWER
			isAddressableAssetOrPrefabInstance |= PrefabUtility.IsPartOfPrefabAsset(gameObject);
			#endif
			var refRect = GetRefRectForGameObject(refLabel, Styles.IdTag, isAddressableAssetOrPrefabInstance);
			DrawRefLabel(refLabel, refRect);
			HandleContextMenu(referenceable, refRect);
		}

		private static void OnBeforeComponentHeaderGUI(Component[] targets, Rect headerRect, bool HeaderIsSelected, bool supportsRichText)
		{
			#if DEV_MODE && DEBUG && !INIT_ARGS_DISABLE_PROFILING
			using var x = afterHeaderGUIMarker.Auto();
			#endif

			var component = targets[0];
			if(!TryGetRefTag(component.gameObject, component, out var referenceable))
			{
				return;
			}

			var refLabel = GetRefLabel(referenceable);
			var refRect = GetRefRectForComponent(component, headerRect, refLabel, Styles.IdTag);
			HandleContextMenu(referenceable, refRect);
		}

		private static void OnAfterComponentHeaderGUI(Component[] targets, Rect headerRect, bool HeaderIsSelected, bool supportsRichText)
		{
			var component = targets[0];
			if(!TryGetRefTag(component.gameObject, component, out var referenceable))
			{
				return;
			}

			var refLabel = GetRefLabel(referenceable);
			var refRect = GetRefRectForComponent(component, headerRect, refLabel, Styles.IdTag);
			DrawRefLabel(refLabel, refRect);
		}

		private static void DrawRefLabel(GUIContent refLabel, Rect refRect)
		{
			GUI.BeginClip(refRect);
			refRect.x = 0f;
			refRect.y = 0f;
			GUI.Label(refRect, refLabel, Styles.IdTag);
			GUI.EndClip();
		}

		private static void HandleContextMenu(IdTag idTag, Rect refRect)
		{
			if(GUI.Button(refRect, GUIContent.none, EditorStyles.label))
			{
				Event.current.Use();
				var menu = new GenericMenu();
				menu.AddItem(new("Copy"), false, CopyToClipboard);
				menu.AddItem(new("Delete"), false, Delete);
				menu.AddItem(new("Generate New"), false, GenerateNewId);
				menu.DropDown(refRect);
			}

			void CopyToClipboard() => GUIUtility.systemCopyBuffer = idTag.guid.ToString();

			void Delete()
			{
				var targetType = idTag.target is GameObject ? "GameObject" : "component";
				if(!EditorUtility.DisplayDialog("Delete Cross-Scene Id?",
			$"Are you sure you want to delete the cross-scene id of this {targetType}?\n\n" +
					$"Any references to this {targetType} from other scenes and assets will be broken.",
					"Delete", "Cancel"))
				{
					return;
				}

				if(!Application.isPlaying || AssetDatabase.IsMainAsset(idTag.gameObject))
				{
					Undo.DestroyObjectImmediate(idTag);
					return;
				}

				Object.Destroy(idTag);
			}
			
			void GenerateNewId()
			{
				var targetType = idTag.target is GameObject ? "GameObject" : "Component";
				if(!EditorUtility.DisplayDialog("Generate New Id?",
			$"Are you sure you want to generate a new cross-scene id for this {targetType}?\n\n" +
					$"Any references to this {targetType} from other scenes and assets will be broken.",
					"Generate New Id", "Cancel"))
				{
					return;
				}
				
				idTag.GenerateNewId();
			}
			
		}

		private static GUIContent GetRefLabel(IdTag referenceable) => new("Id", "Cross-Scene Id:\n" + referenceable.Guid);

		private static Rect GetRefRectForGameObject(GUIContent label, GUIStyle style, bool isAddressableAssetOrPrefabInstance)
		{
			GUILayout.Label(" ", GUILayout.Height(0f));
			var labelRect = GUILayoutUtility.GetLastRect();
			labelRect.size = style.CalcSize(label);
			labelRect.x += 4f;
			labelRect.y -= labelRect.height;
			return labelRect;
		}

		private static Rect GetRefRectForComponent(Component component, Rect headerRect, GUIContent label, GUIStyle style)
        {
			var componentTitle = new GUIContent(ObjectNames.GetInspectorTitle(component));
			float componentTitleEndX = 54f + EditorStyles.largeLabel.CalcSize(componentTitle).x + 10f;
			float availableSpace = headerRect.width - componentTitleEndX - 69f;
			float labelWidth = style.CalcSize(label).x;
			if(labelWidth > availableSpace)
			{
				labelWidth = availableSpace;
			}
			const float MinWidth = 18f;
			if(labelWidth < MinWidth)
			{
				labelWidth = MinWidth;
			}

			var labelRect = headerRect;
			labelRect.x = headerRect.width - 64f - labelWidth;
			#if POWER_INSPECTOR
			labelRect. x -= EditorGUIUtility.singleLineHeight; // add room for Debug Mode+ button
			#endif
			labelRect.y += 4f;

			// Fixes Transform header label rect position.
			// For some reason the Transform header rect starts
			// lower and is shorter than all other headers.
			if(labelRect.height < 22f)
            {
                labelRect.y -= 22f - 15f;
            }

            labelRect.height = 20f;
			labelRect.width = labelWidth;

			return labelRect;
        }

		/// <param name="gameObject"> target itself, or the GameObject to which target is attached.</param>
		/// <param name="target"> GameObject or component. </param>
		private static bool TryGetRefTag([DisallowNull] GameObject gameObject, Object target, out IdTag result)
		{
			foreach(var tag in gameObject.GetComponentsNonAlloc<IdTag>())
			{
				if(ReferenceEquals(tag.Target, target))
				{
					result = tag;
					return true;
				}
			}

			result = null;
			return false;
		}

		#if DEV_MODE && DEBUG && !INIT_ARGS_DISABLE_PROFILING
		private static readonly ProfilerMarker beforeHeaderGUIMarker = new(ProfilerCategory.Gui, "ServiceTagDrawer.BeforeHeaderGUI");
		private static readonly ProfilerMarker afterHeaderGUIMarker = new(ProfilerCategory.Gui, "ServiceTagDrawer.AfterHeaderGUI");
		#endif
	}
}