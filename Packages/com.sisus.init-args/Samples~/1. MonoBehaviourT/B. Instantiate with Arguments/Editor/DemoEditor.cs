using UnityEditor;
using UnityEditor.UIElements;
using UnityEngine;
using UnityEngine.UIElements;

namespace Sisus.Init.Demos.Instantiate
{
	[CustomEditor(typeof(Demo))]
	class DemoEditor : Editor
	{
		const string helpText = "Showcases how components that derive from MonoBehaviour<T...> can be initialized with all the objects that they depend on using the Instantiate extension methods.\n\n" +
		                        "Enter Play Mode and try moving around using the arrow keys.";
		
		public override VisualElement CreateInspectorGUI()
		{
			var root = new VisualElement();
			root.Add(new HelpBox(helpText, HelpBoxMessageType.Info));
			root.Add(new Button( () => Application.OpenURL("https://docs.sisus.co/init-args/features/monobehaviour-t/")){ text = "MonoBehaviour<T> Documentation" });
			InspectorElement.FillDefaultInspector(root , serializedObject , this);
			return root;
		}
	}
}