using UnityEditor;
using UnityEngine;

namespace Init.Demo.EditorOnly
{
	[CustomEditor(typeof(Event), true), CanEditMultipleObjects]
	public sealed class EventDrawer : Editor
	{
		private static readonly string[] scriptField = { "m_Script" };

		public override void OnInspectorGUI()
		{
			DrawPropertiesExcluding(serializedObject, scriptField);
			DrawTriggerEventGUI();
		}

		private void DrawTriggerEventGUI()
		{
			if(target is not IEventTrigger)
			{
				return;
			}

			GUILayout.Space(5f);

			if(!GUILayout.Button("Trigger Event"))
			{
				return;
			}

			foreach(var target in targets)
			{
				if(target is IEventTrigger eventTrigger)
				{
					eventTrigger.Trigger();
				}
			}
		}
	}
}