using UnityEngine;
using UnityEditor;
using System;

namespace Sisus.Init.EditorOnly.Internal
{
	[InitializeOnLoad]
	internal static class DragAndDropUtility
	{
		public static UnityEngine.Object DraggedObject => DragAndDrop.objectReferences.Length > 0 ? DragAndDrop.objectReferences[0] : null;

		public static Type DragAndDroppedComponentType { get; private set; }
		public static Component DragAndDroppedComponent { get; private set; }
		public static GameObject DragSourceGameObject { get; private set; }
		public static GameObject DropTargetGameObject { get; private set; }

		static DragAndDropUtility()
		{
			#if UNITY_6000_4_OR_NEWER
			EditorApplication.hierarchyWindowItemByEntityIdOnGUI -= DetectDragAndDrop;
			EditorApplication.hierarchyWindowItemByEntityIdOnGUI += DetectDragAndDrop;
			#else
			EditorApplication.hierarchyWindowItemOnGUI -= DetectDragAndDrop;
			EditorApplication.hierarchyWindowItemOnGUI += DetectDragAndDrop;
			#endif
		}
 
		private static void DetectDragAndDrop
		(
#if UNITY_6000_4_OR_NEWER
			EntityId entityId,
#else
			int entityId,
#endif
			Rect itemRect)
		{
			if(Event.current.rawType != EventType.DragPerform || !itemRect.Contains(Event.current.mousePosition) || DragAndDrop.objectReferences.Length <= 0 || !(DragAndDrop.objectReferences[0] is Component component))
			{
				return;
			}

			DragSourceGameObject = !component ? null : component.gameObject;
			DragAndDroppedComponent = component;
			DragAndDroppedComponentType = component.GetType();
			DropTargetGameObject = EditorUtility.
				#if UNITY_6000_3_OR_NEWER
				EntityIdToObject
				#else
				InstanceIDToObject
				#endif
				(entityId) as GameObject;

			EditorApplication.delayCall += ClearState;
		}

		private static void ClearState()
		{
			DragAndDroppedComponentType = null;
			DragAndDroppedComponent = null; 
			DragSourceGameObject = null;
			DropTargetGameObject = null;
		}
	}
}