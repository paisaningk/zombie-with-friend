using Sisus.Init.Internal;
using UnityEditor;
using UnityEngine;

namespace Sisus.Init.EditorOnly.Internal
{
	[CustomEditor(typeof(Services), true, isFallback = true), CanEditMultipleObjects]
	public class ServicesEditor : Editor
	{
		private SerializedProperty providesServices;
		private SerializedProperty toClients;

		private static readonly GUIContent clientsLabel = new("For Clients");

		private void OnEnable() => Setup();

		private void Setup()
		{
			providesServices = serializedObject.FindProperty(nameof(providesServices));
			toClients = serializedObject.FindProperty(nameof(toClients));
		}

		public override void OnInspectorGUI()
		{
			if(providesServices is null)
			{
				Setup();
			}

			var services = (Services)target;
			int hashCodeBefore = services.GetStateBasedHashCode();
			EditorGUI.BeginChangeCheck();

			EditorGUILayout.PropertyField(providesServices);
			DrawPocoState(services, hashCodeBefore);
			EditorGUILayout.PropertyField(toClients, clientsLabel);

			if(!EditorGUI.EndChangeCheck())
			{
				return;
			}

			serializedObject.ApplyModifiedProperties();

			int hashCodeAfter = ((Services)target).GetStateBasedHashCode();
			// double check that contents has actually changed, before moving on doing FindObjectsByType
			// and re-registering services
			if(hashCodeBefore == hashCodeAfter)
			{
				return;
			}

			try
			{
				// Avoid raising the Service.AnyChangedEditorOnly more than once
				Service.BatchEditingServices = true;

				var servicesComponent = (Services)target;
				ServiceUtility.RemoveAllServicesProvidedBy(servicesComponent);

				foreach(var definition in servicesComponent.providesServices)
				{
					if(definition.definingType?.Value is { } definingType && definition.service is { } service && service)
					{
						ServiceUtility.AddFor(service, definingType, servicesComponent.toClients, servicesComponent);
					}
				}
			}
			finally
			{
				Service.BatchEditingServices = false;
			}
		}

		private void DrawPocoState(Services services, int hashCodeBefore)
		{
			var anyDrawn = false;

			foreach(var serviceDefinition in services.providesServices)
			{
				if(serviceDefinition.service is ServiceWrapper serviceWrapper && serviceWrapper)
				{
					if(!anyDrawn)
					{
						// If the 'Provides Services' list GUI is unfolded, push the service state GUIs upwards.
						// Otherwise it looks weird with a lot of whitespace to make room for the + and - buttons of the list GUI.
						if(providesServices.isExpanded)
						{
							GUILayout.Space(-EditorGUIUtility.singleLineHeight);
						}

						anyDrawn = true;
					}

					serviceWrapper.DrawStateGUI();
				}
			}

			if(anyDrawn)
			{
				// Add some space between the Service States GUI and the 'For Clients' element.
				GUILayout.Space(5f);
			}
		}
	}
}