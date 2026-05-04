using UnityEditor;
using UnityEditor.UIElements;
using UnityEngine;
using UnityEngine.UIElements;

namespace Sisus.Init.Demos.Initializers
{
	[CustomEditor(typeof(Demo))]
	class DemoEditor : Editor
	{
		const string helpText = "Showcases how Initializers can be used to configure the Init arguments that are provided to instances of components that derive from MonoBehaviour<T...>.\n\n" +
		                        "Enter Play Mode and try moving around using the WASD keys.";
		
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