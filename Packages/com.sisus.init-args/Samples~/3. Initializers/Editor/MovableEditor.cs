using UnityEditor;
using UnityEditor.UIElements;
using UnityEngine;
using UnityEngine.UIElements;

namespace Sisus.Init.Demos.Initializers
{
	[CustomEditor(typeof(Movable))]
	class MovableEditor : Editor
	{
		const string helpText = "\nAn Initializer has been attached to this component.\n\n" +
		                        "The Initializer will automatically acquire the 'Time Provider' object, because it has been configured as a global service using the [Service] attribute.\n\n" +
		                        "The Initializer has been used to override the default 'Arrow Keys Input Provider' service with 'WASD Input Provider' object for this instance, by simply dragging-and-dropping it over the default one in the Inspector.\n\n" +
		                        "A reference to the Level object has been dragged from a different scene, resulting in a cross-scene reference being created automatically.\n\n" +
		                        "A value provider that returns a random float was selected from the float argument's dropdown menu. The provider appears as a selection in the dropdown menus of all float arguments, because the RandomFloat type has [ValueProviderMenu] attribute targeting float type arguments.\n\n" +
		                        "When Play Mode is entered, the Initializer will pass all the configured arguments to the Movable component's Init method, before it's OnAwake method is executed.\n";
		
		public override VisualElement CreateInspectorGUI()
		{
			var root = new VisualElement();
			root.Add(new HelpBox(helpText, HelpBoxMessageType.Info));
			root.Add(new Button(() => Application.OpenURL("https://docs.sisus.co/init-args/features/initializer/")) { text = "Initializer Documentation" });
			InspectorElement.FillDefaultInspector(root , serializedObject , this);
			return root;
		}
	}
}