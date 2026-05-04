using System;
using UnityEditor;

namespace Sisus.Shared.EditorOnly
{
	public static class HierarchyWindowUtility
	{
		public static EditorWindow LastInteractedHierarchyWindow => SceneHierarchyWindow.lastInteractedHierarchyWindow;

		public static bool IsExpanded(
		#if UNITY_6000_4_OR_NEWER
		UnityEngine.EntityId
		#else
		int
		#endif
		itemId)
		{
			foreach(var sceneHierarchyWindow in SceneHierarchyWindow.GetAllSceneHierarchyWindows())
			{
#pragma warning disable CS0618
				if(Array.IndexOf(sceneHierarchyWindow.GetExpandedIDs(), itemId) != -1)
#pragma warning restore CS0618
				{
					return true;
				}
			}

			return false;
		}

		public static
		#if UNITY_6000_4_OR_NEWER
		UnityEngine.EntityId
		#else
		int
		#endif
			GetItemBeingRenamedId()
		{
			var window = SceneHierarchyWindow.lastInteractedHierarchyWindow;
			if(!window)
			{
				#if UNITY_6000_5_OR_NEWER
				return UnityEngine.EntityId.None;
				#else
				return -1;
				#endif
			}
			
			var renameOverlay = window.sceneHierarchy.treeView.state.renameOverlay;
			if(!renameOverlay.IsRenaming())
			{
				#if UNITY_6000_5_OR_NEWER
				return UnityEngine.EntityId.None;
				#else
				return -1;
				#endif
			}

#pragma warning disable CS0618
			return renameOverlay.userData;
#pragma warning restore CS0618
		}

		public static int GetDraggedItemId()
		{
			var window = SceneHierarchyWindow.lastInteractedHierarchyWindow;
			if(!window)
			{
				return -1;
			}
		
			var treeView = window.sceneHierarchy.treeView;
			if(!treeView.isDragging)
			{
				return -1;
			}
			
			return treeView.dragging?.GetDropTargetControlID() ?? -1;
		}

		public static bool IsDraggedOrSelected
		(
			#if UNITY_6000_4_OR_NEWER
			UnityEngine.EntityId entityId
			#else
			int entityId
			#endif
		)
		{
			var window = SceneHierarchyWindow.lastInteractedHierarchyWindow;
			if(!window)
			{
				return false;
			}

			var treeView = window.sceneHierarchy.treeView;
			if(treeView.isDragging)
			{
				return treeView.IsItemDragSelectedOrSelected(new(entityId, 0));
			}

			return treeView.HasSelection() && treeView.IsSelected(entityId);
		}

		public static void SetExpandedRecursive
		(
			#if UNITY_6000_4_OR_NEWER
			UnityEngine.EntityId entityId,
			#else
			int entityId,
			#endif
			bool expand
		)
		=> SceneHierarchyWindow.lastInteractedHierarchyWindow.SetExpandedRecursive(entityId, expand);

		public static bool IsHierarchyWindowFocused()
		{
			var window = SceneHierarchyWindow.lastInteractedHierarchyWindow;
			return window && window.sceneHierarchy.treeView.HasFocus();
		}
	}
}