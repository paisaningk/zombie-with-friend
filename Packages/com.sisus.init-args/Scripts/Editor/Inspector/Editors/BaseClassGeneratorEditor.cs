using UnityEditor;
using UnityEngine;

namespace Sisus.Init
{
	[CustomEditor(typeof(BaseClassGenerator))]
	public sealed class BaseClassGeneratorEditor : Editor
	{
		public override void OnInspectorGUI()
		{
			serializedObject.Update();
			DrawPropertiesExcluding(serializedObject, "m_Script");
			serializedObject.ApplyModifiedProperties();

			EditorGUILayout.Space();

			var generator = (BaseClassGenerator)target;
			using (new EditorGUI.DisabledScope(string.IsNullOrWhiteSpace(generator.ClassName) || string.IsNullOrWhiteSpace(generator.DerivesFrom)))
			{
				if (GUILayout.Button("Generate"))
				{
					generator.Generate();
				}
			}
		}
	}
}
