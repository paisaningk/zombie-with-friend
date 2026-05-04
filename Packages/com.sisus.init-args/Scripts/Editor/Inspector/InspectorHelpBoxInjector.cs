using System;
using System.Collections.Generic;
using Sisus.Init.Internal;
using Sisus.Init.ValueProviders;
using Sisus.Shared.EditorOnly;
using UnityEditor;
using UnityEngine;

namespace Sisus.Init.EditorOnly.Internal
{
	/// <summary>
	/// Class that can be used to insert boxes containing texts into the Inspector
	/// below the headers of components of particular types.
	/// </summary>
	[InitializeOnLoad]
	internal static class InspectorHelpBoxInjector
	{
		const float topPadding = 3f;
		const float leftPadding = 15f;
		const float rightPadding = 8f;
		const float IconWidth = 20f;
		static GUIStyle helpBoxTextStyle;
		static readonly GUIContent content = new("");

		static InspectorHelpBoxInjector()
		{
			ComponentHeader.AfterHeaderGUI -= OnAfterHeaderGUI;
			ComponentHeader.AfterHeaderGUI += OnAfterHeaderGUI;
		}

		private static readonly HashSet<Type> registeredDefiningTypes = new();
		private static readonly HashSet<Type> warnedAboutServices = new();

		static void OnAfterHeaderGUI(Component[] targets, Rect headerRect, bool HeaderIsSelected, bool supportsRichText)
		{
			registeredDefiningTypes.Clear();
			warnedAboutServices.Clear();

			var component = targets[0];
			if(component is Services servicesComponent)
			{
				foreach(var serviceDefinition in servicesComponent.providesServices)
				{
					var service = serviceDefinition.service;
					if(!service)
					{
						if(warnedAboutServices.Add(null))
						{
							content.text = "A service is missing.";
							DrawHelpBox(MessageType.Warning, content);
						}

						continue;
					}

					var definingType = serviceDefinition.definingType.Value;
					if(definingType is not null)
					{
						if(!definingType.IsInstanceOfType(service)
							&& !ValueProviderUtility.IsValueTypeSupported(service, definingType))
						{
							var concreteType = service.GetType();
							var concreteTypeToString = TypeUtility.ToString(concreteType);
							var definingTypeToString = TypeUtility.ToString(definingType);
							content.text = definingType.IsInterface
								? $"{concreteTypeToString} does not implement {definingTypeToString} or IValueProvider<{definingTypeToString}>."
								: $"{concreteTypeToString} does not derive from {definingTypeToString} or implement IValueProvider<{definingTypeToString}>";

							DrawHelpBox(MessageType.Warning, content);
						}

						if(!registeredDefiningTypes.Add(definingType) && warnedAboutServices.Add(definingType))
						{
							var definingTypeToString = TypeUtility.ToString(definingType);
							content.text = $"Multiple services have the same defining type: {definingTypeToString}.";
							DrawHelpBox(MessageType.Warning, content);
						}
					}
					else if(warnedAboutServices.Add(service.GetType()))
					{
						content.text = $"The service {service.GetType().Name} is missing its defining type.";
						DrawHelpBox(MessageType.Warning, content);
					}

					definingType ??= service.GetType();
					if(ServiceAttributeUtility.ContainsDefiningType(definingType))
					{
						content.text = GetReplacesDefaultServiceText(definingType, servicesComponent.toClients);
						DrawHelpBox(MessageType.Info, content);
					}
					else if(service is Component serviceComponent && TryGetServiceTag(serviceComponent, serviceDefinition.definingType, out _))
					{
						content.text = GetReplacesDefaultServiceText(definingType, servicesComponent.toClients);
						DrawHelpBox(MessageType.Info, content);
					}
				}
			}
			else
			{
				foreach(var serviceTag in ServiceTagUtility.GetServiceTagsTargeting(component))
				{
					if(serviceTag.DefiningType is { } definingType && ServiceAttributeUtility.ContainsDefiningType(definingType))
					{
						content.text = GetReplacesDefaultServiceText(definingType, serviceTag.ToClients);
						DrawHelpBox(MessageType.Info, content);
					}
				}
			}
		}

		static bool TryGetServiceTag(Component component, Type matchingDefiningType, out ServiceTag result)
		{
			foreach(var serviceTag in ServiceTagUtility.GetServiceTagsTargeting(component))
			{
				if(serviceTag.DefiningType == matchingDefiningType)
				{
					result = serviceTag;
					return true;
				}
			}

			result = null;
			return false;
		}

		static string GetReplacesDefaultServiceText(Type serviceType, Clients toClients)
		{
			return toClients switch
			{
				Clients.Everywhere => $"Replaces the default {TypeUtility.ToString(serviceType)} service for all clients.",
				Clients.InGameObject => $"This Replaces the default {TypeUtility.ToString(serviceType)} service for clients in this game object.",
				Clients.InChildren => $"Replaces the default {TypeUtility.ToString(serviceType)} service for clients in this game object and all of its children.",
				Clients.InParents => $"Replaces the default {TypeUtility.ToString(serviceType)} service for clients in this game object and all of its parents.",
				Clients.InHierarchyRootChildren => $"Replaces the default {TypeUtility.ToString(serviceType)} service for clients in the root game object and all of its children.",
				Clients.InScene => $"Replaces the default {TypeUtility.ToString(serviceType)} service for clients in this scene.",
				Clients.InAllScenes => $"Replaces the default {TypeUtility.ToString(serviceType)} service for clients in all scenes.",
				_ => $"Replaces the default {TypeUtility.ToString(serviceType)} service for clients {toClients}."
			};
		}

		static void DrawHelpBox(MessageType type, GUIContent content, string url = "")
		{
			GUILayout.Space(topPadding);

			helpBoxTextStyle ??= new(EditorStyles.label)
			{
				richText = true,
				wordWrap = true,
				alignment = TextAnchor.MiddleLeft
			};

			var boxWidth = EditorGUIUtility.currentViewWidth - leftPadding - rightPadding;
			var textWidth = boxWidth - 30f;
			var textHeight = helpBoxTextStyle.CalcHeight(content, textWidth);
			var boxHeight = Mathf.Max(textHeight + 14f, 30f);
			var helpBoxRect = GUILayoutUtility.GetRect(boxWidth, boxHeight, EditorStyles.helpBox);
			helpBoxRect.x += leftPadding;
			helpBoxRect.width = boxWidth;
			helpBoxRect.height = boxHeight;

			GUI.Label(helpBoxRect, "", EditorStyles.helpBox);

			var iconRect = helpBoxRect;
			iconRect.x += 5f;
			iconRect.width = IconWidth;
			GUI.Label(iconRect, type switch
			{
				MessageType.Error => Styles.ErrorIcon,
				MessageType.Warning => Styles.WarningIcon,
				_ => Styles.InfoIcon,
			});

			var textRect = helpBoxRect; 
			textRect.x += 25f;
			textRect.y += 7f;
			textRect.width -= 25f;
			textRect.height = EditorGUIUtility.singleLineHeight;
			textRect.width = textWidth;
			textRect.height = textHeight;

			GUI.Label(textRect, content, helpBoxTextStyle);

			if(url.Length > 0)
			{
				EditorGUIUtility.AddCursorRect(textRect, MouseCursor.Link);
				if(GUI.Button(textRect, GUIContent.none, EditorStyles.label))
				{
					Application.OpenURL(url);
				}
			}
		}
	}
}