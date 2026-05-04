using UnityEditor;
using UnityEditor.UIElements;
using UnityEngine;
using UnityEngine.UIElements;

namespace Sisus.Init.Demos.Services
{
	[CustomEditor(typeof(Level))]
	public class LevelEditor : Editor
	{
		private const string helpText = "Level has been registered as a global service acquired from the scene via [Service(FindFromScene = true)].\n\n" +
		                                "Null Argument Guard was disabled via the icon in the Init section, because providing a custom bounds value during initialization is optional. The component also supports configuring the bounds via the Inspector.\n\n" +
		                                "The Init section could also be hidden in the Inspector by disabling 'Show Init Section' in the component's context menu, if desired.";

		public override VisualElement CreateInspectorGUI()
		{
			var root = new VisualElement();
			root.Add(new HelpBox(helpText, HelpBoxMessageType.Info));
			root.Add(new Button(() => Application.OpenURL("https://docs.sisus.co/init-args/features/service-tag/")) { text = "Service Tag Documentation" });
			InspectorElement.FillDefaultInspector(root, serializedObject, this);
			return root;
		}
	}
}