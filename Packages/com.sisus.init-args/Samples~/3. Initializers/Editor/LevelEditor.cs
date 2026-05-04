using UnityEditor;
using UnityEditor.UIElements;
using UnityEngine;
using UnityEngine.UIElements;

namespace Sisus.Init.Demos.Initializers
{
	[CustomEditor(typeof(Level))]
	public class LevelEditor : Editor
	{
		private const string helpText = "Although Level exist in a different scene from Movable, Movable can still reference it. This is because Initializers support dragging-and-dropping references across scenes.\n\n" +
		                                "A cross-scene reference identifier is automatically generated for any component that is dragged into an Object reference field on an Initializer in a different scene.\n\n" +
		                                "You can see the cross-scene reference identifier of a component by mouseovering the [Ref] tag in the Inspector. You can also copy or delete the id from the context menu that opens if you click the tag.";

		public override VisualElement CreateInspectorGUI()
		{
			var root = new VisualElement();
			root.Add(new HelpBox(helpText, HelpBoxMessageType.Info));
			root.Add(new Button(() => Application.OpenURL("https://docs.sisus.co/init-args/features/initializer/")) { text = "Initializer Documentation" });
			InspectorElement.FillDefaultInspector(root, serializedObject, this);
			return root;
		}
	}
}