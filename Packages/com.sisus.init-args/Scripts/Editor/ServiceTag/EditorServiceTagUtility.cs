//#define DEBUG_CLEAR_CACHE
//#define DEBUG_GET_SERVICE_DEFINING_TYPES

using System;
using System.Buffers;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Runtime.CompilerServices;
using Sisus.Init.Internal;
using Sisus.Shared.EditorOnly;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using Object = UnityEngine.Object;
using static Sisus.Init.Internal.ServiceTagUtility;

#if UNITY_ADDRESSABLES_1_17_4_OR_NEWER
using UnityEditor.AddressableAssets;
using UnityEditor.AddressableAssets.Settings;
#endif

#if DEV_MODE && DEBUG && !INIT_ARGS_DISABLE_PROFILING
using Unity.Profiling;
#endif

namespace Sisus.Init.EditorOnly.Internal
{
	[InitializeOnLoad]
	internal static class EditorServiceTagUtility
	{
		internal static Component openSelectTagsMenuFor;
		private static readonly GUIContent serviceLabel = new("Service", "A service of this type is available.\n\nIt can be acquired automatically during initialization.");
		private static readonly GUIContent blankLabel = new(" ");
		private static readonly HashSet<Type> definingTypesBuilder = new();
		private static Dictionary<object, Type[]> objectDefiningTypesCache = new();
		private static Dictionary<object, Type[]> objectDefiningTypesCache2 = new();

		static EditorServiceTagUtility()
		{
			Selection.selectionChanged -= OnSelectionChanged;
			Selection.selectionChanged += OnSelectionChanged;
			ObjectChangeEvents.changesPublished -= OnObjectChangesPublished;
			ObjectChangeEvents.changesPublished += OnObjectChangesPublished;
			Service.AnyChangedEditorOnly -= OnAnyServiceChanged;
			Service.AnyChangedEditorOnly += OnAnyServiceChanged;
			EditorApplication.playModeStateChanged -= OnPlayModeStateChanged;
			EditorApplication.playModeStateChanged += OnPlayModeStateChanged;
			Undo.undoRedoPerformed -= OnUndoRedoPerformed;
			Undo.undoRedoPerformed += OnUndoRedoPerformed;
			SceneManager.sceneUnloaded -= OnSceneUnloaded;
			SceneManager.sceneUnloaded += OnSceneUnloaded;
			EditorSceneManager.sceneClosed -= OnSceneClosed;
			EditorSceneManager.sceneClosed += OnSceneClosed;

			static void OnObjectChangesPublished(ref ObjectChangeEventStream stream) => RebuildDefiningTypesCache();
			static void OnUndoRedoPerformed() => RebuildDefiningTypesCache();

			static void OnSelectionChanged()
			{
				// Need to repaint editor header to update service tag position.
				RepaintAllServiceEditors();
			}

			static void OnAnyServiceChanged() => RebuildDefiningTypesCache();
			static void OnPlayModeStateChanged(PlayModeStateChange mode) => RebuildDefiningTypesCache();
			static void OnSceneUnloaded(Scene scene) => RebuildDefiningTypesCache();
			static void OnSceneClosed(Scene scene) => RebuildDefiningTypesCache();

			static void RebuildDefiningTypesCache()
			{
				#if DEV_MODE && DEBUG_CLEAR_CACHE
				Debug.Log(nameof(RebuildDefiningTypesCache));
				#endif
				EditorApplication.delayCall -= RebuildDefiningTypesCacheImmediate;
				EditorApplication.delayCall += RebuildDefiningTypesCacheImmediate;
			}

			#if DEV_MODE
			[MenuItem("DevMode/Rebuild Service Defining Types Cache")]
			#endif
			static void RebuildDefiningTypesCacheImmediate()
			{
				#if DEV_MODE && DEBUG_CLEAR_CACHE
				Debug.Log($"RebuildDefiningTypesCacheImmediate called with {objectDefiningTypesCache.Count} entries...");
				#endif

				int count = objectDefiningTypesCache.Count;
				if(count is 0)
				{
					return;
				}

				var oldCache = objectDefiningTypesCache;
				(objectDefiningTypesCache, objectDefiningTypesCache2) = (objectDefiningTypesCache2, objectDefiningTypesCache);

				#if DEV_MODE && UNITY_ASSERTIONS
				Debug.Assert(objectDefiningTypesCache.Count is 0);
				#endif

				var hashSet = new HashSet<Type>();
				var definingTypesChanged = false;
				foreach(var oldEntry in oldCache)
				{
					var target = oldEntry.Key;
					if(target is Object unityObject && !unityObject)
					{
						definingTypesChanged = true;
						continue;
					}

					var oldDefiningTypes = oldEntry.Value;
					var newDefiningTypes = GetServiceDefiningTypes(target);
					if(!AreEqual(oldDefiningTypes, newDefiningTypes, hashSet))
					{
						#if DEV_MODE && DEBUG_CLEAR_CACHE
						Debug.Log($"Defining types changed for {target}:\nOld: {TypeUtility.ToString(oldDefiningTypes)}\nNew: {TypeUtility.ToString(newDefiningTypes)}");
						#endif
						definingTypesChanged = true;
					}
					#if DEV_MODE && DEBUG_CLEAR_CACHE
					else if(target.GetType().Name is "Player")
					{
						Debug.Log($"Defining types for {target.GetType().FullName} did not change: {TypeUtility.ToString(newDefiningTypes)}");
					}
					#endif
				}

				foreach(var oldDefiningTypes in oldCache.Values)
				{
					if(oldDefiningTypes.Length > 0)
					{
						ArrayPool<Type>.Shared.Return(oldDefiningTypes, true);
					}
				}

				oldCache.Clear();

				if(!definingTypesChanged)
				{
					return;
				}

				InspectorContents.Repaint();
			}

			static void RepaintAllServiceEditors()
			{
				var gameObject = Selection.activeGameObject;
				if(!gameObject)
				{
					return;
				}

				foreach(var component in Selection.activeGameObject.GetComponentsNonAlloc<Component>())
				{
					// Skip missing components
					if(!component)
					{
						continue;
					}

					if(GetServiceDefiningTypes(component).Length > 0)
					{
						// Need to repaint editor header to update service tag position.
						InspectorContents.RepaintEditorsWithTarget(component);
					}
				}
			}
		}

		internal static Span<Type> GetServiceDefiningTypes([DisallowNull] object serviceOrServiceProvider)
		{
			#if DEV_MODE && DEBUG && !INIT_ARGS_DISABLE_PROFILING
			using var x = getServiceDefiningTypesMarker.Auto();
			#endif

			if(objectDefiningTypesCache.TryGetValue(serviceOrServiceProvider, out var cachedResults))
			{
				if(cachedResults.Length <= 1)
				{
					return cachedResults;
				}

				int nullIndex = Array.IndexOf(cachedResults, null, 1);
				return nullIndex is -1 ? cachedResults.AsSpan() : cachedResults.AsSpan(0, nullIndex);
			}

			AddServiceDefiningTypes(serviceOrServiceProvider, definingTypesBuilder);

			int count = definingTypesBuilder.Count;
			if(count == 0)
			{
				objectDefiningTypesCache.Add(serviceOrServiceProvider, Array.Empty<Type>());
				return Span<Type>.Empty;
			}

			var definingTypes = ArrayPool<Type>.Shared.Rent(definingTypesBuilder.Count);
			definingTypesBuilder.CopyTo(definingTypes);
			definingTypesBuilder.Clear();
			objectDefiningTypesCache.Add(serviceOrServiceProvider, definingTypes);
			return definingTypes.AsSpan(0, count);

			[MethodImpl(MethodImplOptions.AggressiveInlining)]
			static void AddServiceDefiningTypes([DisallowNull] object serviceOrServiceProvider, [DisallowNull] HashSet<Type> definingTypes)
			{
				var concreteType = serviceOrServiceProvider.GetType();
				foreach(var definingType in ServiceAttributeUtility.definingTypes)
				{
					var serviceInfo = definingType.Value;
					if(serviceInfo.classWithAttribute != concreteType && serviceInfo.concreteType != concreteType)
					{
						continue;
					}

					if(serviceInfo.loadMethod is LoadMethod.FindFromScene)
					{
						if(Find.GameObjectOf(serviceOrServiceProvider, out var gameObject) && IsSceneObjectOrPrefabEditedInSceneContext(gameObject))
						{
							definingTypes.Add(definingType.Key);
						}
						
						static bool IsSceneObjectOrPrefabEditedInSceneContext(GameObject gameObject)
						{
							if(!gameObject.scene.IsValid())
							{
								return false;
							}

							var prefabStage = PrefabStageUtility.GetPrefabStage(gameObject);
							if(!prefabStage)
							{
								return true;
							}

							if(prefabStage.mode is PrefabStage.Mode.InIsolation)
							{
								return false;
							}
							
							var root = prefabStage.openedFromInstanceRoot;
							return root && root.scene.IsValid();
						}
					}
					else if(serviceInfo.ResourcePath is { Length: > 0 } resourcePath)
					{
						if(Find.In(serviceOrServiceProvider, out Object unityObject) && string.Equals(unityObject.name, Path.GetFileNameWithoutExtension(resourcePath)) && AssetDatabase.Contains(unityObject))
						{
							definingTypes.Add(definingType.Key);
						}
					}
					#if UNITY_ADDRESSABLES_1_17_4_OR_NEWER
					else if(serviceInfo.referenceType is ReferenceType.AddressableKey)
					{
						if(Find.In(serviceOrServiceProvider, out Object unityObject) && AssetDatabase.Contains(unityObject))
						{
							definingTypes.Add(definingType.Key);
						}
					}
					#endif
					else
					{
						definingTypes.Add(definingType.Key);
					}
				}

				foreach(var activeInstance in Service.ActiveInstancesEditorOnly)
				{
					if(ReferenceEquals(activeInstance.ServiceOrProvider, serviceOrServiceProvider))
					{
						definingTypes.Add(activeInstance.definingType);
					}
				}

				foreach(var service in ServiceInjector.services)
				{
					if(ReferenceEquals(service.Value, serviceOrServiceProvider))
					{
						definingTypes.Add(service.Key);
					}
				}

				if(!Application.isPlaying)
				{
					if(serviceOrServiceProvider is Component component && component)
					{
						foreach(var serviceTag in GetServiceTagsTargeting(component))
						{
							if(serviceTag?.DefiningType is { } serviceTagDefiningType)
							{
								definingTypes.Add(serviceTagDefiningType);
							}
						}
					}

					if(serviceOrServiceProvider is Object unityObject && unityObject)
					{
						foreach(var servicesComponent in Services.GetAllEditorOnly())
						{
							foreach(var definition in servicesComponent.providesServices)
							{
								if(ReferenceEquals(definition.service, serviceOrServiceProvider)
									&& definition.definingType.Value is { } definingType)
								{
									definingTypes.Add(definingType);
								}
							}
						}
					}
				}
			}
		}

		internal static Clients GetClients([DisallowNull] Component serviceOrServiceProvider)
		{
			#if DEV_MODE && DEBUG && !INIT_ARGS_DISABLE_PROFILING
			using var x = getServiceDefiningTypesMarker.Auto();
			#endif

			Clients? result = null;

			foreach(var serviceTag in serviceOrServiceProvider.gameObject.GetComponentsNonAlloc<ServiceTag>())
			{
				if(ReferenceEquals(serviceTag.Service, serviceOrServiceProvider))
				{
					SetToHigherVisibility(ref result, serviceTag.ToClients);
				}
			}

			foreach(var services in Services.GetAllEditorOnly())
			{
				foreach(var providedService in services.providesServices)
				{
					if(ReferenceEquals(providedService.service, serviceOrServiceProvider))
					{
						SetToHigherVisibility(ref result, services.toClients);
						break;
					}
				}
			}

			foreach(var activeInstance in Service.ActiveInstancesEditorOnly)
			{
				if(ReferenceEquals(activeInstance.ServiceOrProvider, serviceOrServiceProvider))
				{
					SetToHigherVisibility(ref result, activeInstance.ToClients);
				}
			}

			return result ?? Clients.Everywhere;

			static void SetToHigherVisibility(ref Clients? result, Clients toClients)
			{
				if(!result.HasValue || toClients.CompareTo(result.Value) > 0)
				{
					result = toClients;
				}
			}
		}

		public static bool IsService([MaybeNull] Object client, Type dependencyType)
		{
			if(ServiceAttributeUtility.ContainsDefiningType(dependencyType)
				|| ServiceInjector.services.ContainsKey(dependencyType)
				|| ServiceInjector.TryGetUninitializedServiceInfo(dependencyType, out _))
			{
				return true;
			}

			var clientComponent = Find.In<Transform>(client);
			var clientIsComponent = clientComponent is not null;

			foreach(var activeInstance in Service.ActiveInstancesEditorOnly)
			{
				if(activeInstance.definingType != dependencyType)
				{
					continue;
				}

				var clients = activeInstance.ToClients;
				if(clients is Clients.Everywhere || (clientIsComponent && activeInstance.Registerer && Service.IsAccessibleTo(clientComponent, activeInstance.Registerer, clients)))
				{
					return true;
				}
			}

			// In Edit Mode it's possible that the dependency is registered via a value provider in a Service Tag
			// or Services component, but it will only become available at runtime.
			if(!Application.isPlaying)
			{
				foreach(var serviceTag in ServiceTag.GetAllEditorOnly())
				{
					if(serviceTag.DefiningType != dependencyType)
					{
						continue;
					}

					var clients = serviceTag.ToClients;
					if(clients is Clients.Everywhere || (clientIsComponent && Service.IsAccessibleTo(clientComponent, serviceTag, clients)))
					{
						return true;
					}
				}

				foreach(var servicesComponent in Services.GetAllEditorOnly())
				{
					foreach(var definition in servicesComponent.providesServices)
					{
						if(definition.definingType.Value != dependencyType)
						{
							continue;
						}

						var clients = servicesComponent.toClients;
						if(clients is Clients.Everywhere || (clientIsComponent && Service.IsAccessibleTo(clientComponent, servicesComponent, clients)))
						{
							return true;
						}
					}
				}
			}

			return false;
		}

		internal static Rect GetTagRect(Component component, Rect headerRect, GUIContent label, GUIStyle style)
		{
			var componentTitle = new GUIContent(ObjectNames.GetInspectorTitle(component));
			float componentTitleEndX = 54f + EditorStyles.largeLabel.CalcSize(componentTitle).x + 10f;
			float availableSpace = EditorGUIUtility.currentViewWidth - componentTitleEndX - 69f;
			float labelWidth = style.CalcSize(label).x;
			if(labelWidth > availableSpace)
			{
				labelWidth = availableSpace;
			}
			const float MinWidth = 18f;
			if(labelWidth < MinWidth)
			{
				labelWidth = MinWidth;
			}

			var labelRect = headerRect;
			labelRect.x = EditorGUIUtility.currentViewWidth - 69f - labelWidth;
			labelRect.y += 3f;

			// Fixes Transform header label rect position.
			// For some reason the Transform header rect starts
			// lower and is shorter than all other headers.
			if(labelRect.height < 22f)
			{
				labelRect.y -= 22f - 15f;
			}

			labelRect.height = 20f;
			labelRect.width = labelWidth;
			return labelRect;
		}

		/// <param name="anyProperty"> SerializedProperty of <see cref="Any{T}"/> or some other type field. </param>
		internal static bool Draw(Rect position, GUIContent prefixLabel, SerializedProperty anyProperty, GUIContent serviceLabel = null, bool serviceExists = true)
		{
			var controlRect = EditorGUI.PrefixLabel(position, blankLabel);
			bool clicked = Draw(controlRect, anyProperty, serviceLabel, serviceExists);
			position.width -= controlRect.x - position.x;
			int indentLevelWas = EditorGUI.indentLevel;
			EditorGUI.indentLevel = 0;
			GUI.Label(position, prefixLabel);
			EditorGUI.indentLevel = indentLevelWas;
			return clicked;
		}

		/// <param name="anyProperty"> SerializedProperty of <see cref="Any{T}"/> or some other type field. </param>
		internal static bool Draw(Rect controlRect, SerializedProperty anyProperty = null, GUIContent label = null, bool serviceExists = true)
		{
			label ??= serviceLabel;
			float maxWidth = Styles.ServiceTag.CalcSize(label).x;
			if(controlRect.width > maxWidth)
			{
				controlRect.width = maxWidth;
			}

			controlRect.y += 2f;
			controlRect.height -= 2f;
			int indentLevelWas = EditorGUI.indentLevel;
			EditorGUI.indentLevel = 0;

			var backgroundColorWas = GUI.backgroundColor;
			if(serviceExists)
			{
				GUI.backgroundColor = new Color(1f, 1f, 0f);
			}

			bool clicked = GUI.Button(controlRect, label, Styles.ServiceTag);

			GUI.backgroundColor = backgroundColorWas;

			GUILayout.Space(2f);

			EditorGUI.indentLevel = indentLevelWas;

			if(!clicked)
			{
				return false;
			}

			GUI.changed = true;

			if(anyProperty is not null)
			{
				OnServiceTagClicked(controlRect, anyProperty);
			}

			return true;
		}

		/// <param name="anyProperty"> SerializedProperty of <see cref="Any{T}"/> or some other type field. </param>
		internal static void OnServiceTagClicked(Rect controlRect, SerializedProperty anyProperty)
		{
			if(anyProperty == null)
			{
				#if DEV_MODE
				Debug.LogWarning($"OnServiceTagClicked called but {nameof(anyProperty)} was null.");
				#endif
				return;
			}

			var propertyValue = anyProperty.GetValue();
			if(propertyValue == null)
			{
				#if DEV_MODE
				Debug.LogWarning($"OnServiceTagClicked called but GetValue returned null for {nameof(anyProperty)} '{anyProperty.name}' ('{anyProperty.propertyPath}').");
				#endif
				return;
			}

			Type propertyType = propertyValue.GetType();

			Type serviceType;
			if(typeof(IAny).IsAssignableFrom(propertyType) && propertyType.IsGenericType)
			{
				serviceType = propertyType.GetGenericArguments()[0];
			}
			else
			{
				serviceType = propertyType;
			}

			switch(Event.current.button)
			{
				case 0:
				case 2:
					var targetObject = anyProperty.serializedObject.targetObject;
					PingServiceOfClient(targetObject, serviceType);
					return;
				case 1:
					AnyPropertyDrawer.OpenDropdown(controlRect, anyProperty);
					return;
			}
		}

		internal static bool PingServiceOfClient([DisallowNull] Object client, [DisallowNull] Type serviceType)
		{
			if(TryGetServicePingInfo(client, serviceType, out var info))
			{
				info.Ping();
				return true;
			}

			return false;
		}

		private static bool TryGetServicePingInfo([DisallowNull] Object client, [DisallowNull] Type serviceType, out PingableServiceInfo result)
		{
			var clientTransform = Find.In<Transform>(client);
			var clientIsComponent = clientTransform is not null;
			var clientScene = clientIsComponent ? clientTransform.gameObject.scene : default;

			// In Edit Mode it's possible that the dependency is registered via a value provider in a Service Tag
			// or Services component, but it will only become available at runtime.
			if(!Application.isPlaying)
			{
				PingableServiceInfo? nearestInstance = null;

				foreach(var serviceTag in ServiceTag.GetAllEditorOnly())
				{
					if(serviceTag.DefiningType != serviceType || !serviceTag.Service)
					{
						continue;
					}

					if(!Service.IsAccessibleTo(clientTransform, serviceTag, serviceTag.ToClients))
					{
						continue;
					}

					if(nearestInstance is not { } nearest)
					{
						nearestInstance = serviceTag;
						continue;
					}

					if(serviceTag.gameObject.scene != clientScene)
					{
						continue;
					}

					if(nearest.Scene != clientScene)
					{
						nearestInstance = serviceTag;
						continue;
					}

					var instanceTransform = serviceTag.transform;
					var nearestTransform = nearest.Context;

					for(var clientParent = clientTransform; clientParent; clientParent = clientParent.parent)
					{
						if(clientParent == instanceTransform)
						{
							if(clientParent == nearestTransform)
							{
								break;
							}

							nearestInstance = serviceTag;
							break;
						}

						if(clientParent == nearestTransform)
						{
							break;
						}
					}
				}

				foreach(var servicesComponent in Services.GetAllEditorOnly())
				{
					if(!Service.IsAccessibleTo(clientTransform, servicesComponent, servicesComponent.toClients))
					{
						continue;
					}

					foreach(var definition in servicesComponent.providesServices)
					{
						if(definition.definingType.Value != serviceType || !definition.service)
						{
							continue;
						}

						if(nearestInstance is not { } nearest)
						{
							nearestInstance = new(servicesComponent, definition.service);
							continue;
						}

						if(servicesComponent.gameObject.scene != clientScene)
						{
							continue;
						}

						if(nearest.Scene != clientScene)
						{
							nearestInstance = new(servicesComponent, definition.service);
							continue;
						}

						var instanceTransform = servicesComponent.transform;
						var nearestTransform = nearest.Context;

						for(var clientParent = clientTransform; clientParent; clientParent = clientParent.parent)
						{
							if(clientParent == instanceTransform)
							{
								if(clientParent == nearestTransform)
								{
									break;
								}

								nearestInstance = new PingableServiceInfo(servicesComponent, definition.service);
								break;
							}

							if(clientParent == nearestTransform)
							{
								break;
							}
						}
					}
				}

				foreach(var instance in Service.ActiveInstancesEditorOnly)
				{
					if(instance.definingType != serviceType)
					{
						continue;
					}

					var pingable = instance.ServiceOrProvider as Object;
					if(!pingable)
					{
						continue;
					}

					var pingableComponent = pingable as Component;
					if(pingableComponent)
					{
						if(!Service.IsAccessibleTo(clientTransform, pingableComponent, instance.ToClients))
						{
							continue;
						}
					}
					else if(instance.ToClients is not Clients.Everywhere)
					{
						continue;
					}

					if(nearestInstance is not { } nearest)
					{
						nearestInstance = instance;
						continue;
					}

					if(instance.Scene != clientScene)
					{
						continue;
					}

					if(nearest.Scene != clientScene)
					{
						nearestInstance = instance;
						continue;
					}

					var instanceTransform = instance.Transform;
					var nearestTransform = nearest.Context;

					for(var clientParent = clientTransform; clientParent; clientParent = clientParent.parent)
					{
						if(clientParent == instanceTransform)
						{
							if(clientParent == nearestTransform)
							{
								break;
							}

							nearestInstance = instance;
							break;
						}

						if(clientParent == nearestTransform)
						{
							break;
						}
					}
				}

				if(nearestInstance.HasValue)
				{
					result = nearestInstance.Value;
					return true;
				}
			}

			if(ServiceAttributeUtility.TryGetInfoForDefiningType(serviceType, out var globalServiceInfo))
			{
				var scriptWithAttribute = Find.Script(globalServiceInfo.classWithAttribute);

				if(Application.isPlaying && Find.typesToFindableTypes.ContainsKey(serviceType) && !globalServiceInfo.IsTransient && (!globalServiceInfo.LazyInit || ServiceUtility.ExistsFor(client, serviceType)))
				{
					if(ServiceUtility.TryGetFor(client, serviceType, out var service) && Find.UnityObjectOf(service, out var unityObject))
					{
						result = new(serviceOrProvider:unityObject, registerer:scriptWithAttribute, context: clientTransform, scene: clientScene);
					}
				}

				if(globalServiceInfo.FindFromScene)
				{
					if(Find.Any(serviceType, out var service) && Find.UnityObjectOf(service, out var unityObject))
					{
						var transform = Find.In<Transform>(unityObject);
						result = new(serviceOrProvider:unityObject, registerer: scriptWithAttribute, context: transform, scene: transform ? transform.gameObject.scene : default);
						return true;
					}
				}

				if(globalServiceInfo.SceneBuildIndex >= 0)
				{
					var sceneAsset = Find.SceneAssetByBuildIndex(globalServiceInfo.SceneBuildIndex);
					if(sceneAsset)
					{
						var scene = SceneManager.GetSceneByBuildIndex(globalServiceInfo.SceneBuildIndex);
						var context = scene.isLoaded && Find.Any(serviceType, out var service) && Find.In(service, out Transform transform) ? transform : null;
						result = new(serviceOrProvider:sceneAsset, registerer: scriptWithAttribute, context: context, scene);
						return true;
					}
				}
				else if(globalServiceInfo.ScenePath is { Length: > 0 } scenePath)
				{
					var sceneAsset = Find.SceneAssetByName(scenePath);
					if(sceneAsset)
					{
						var scene = SceneManager.GetSceneByPath(scenePath);
						var context = scene.isLoaded && Find.Any(serviceType, out var service) && Find.In(service, out Transform transform) ? transform : null;
						result = new(serviceOrProvider:sceneAsset, registerer: scriptWithAttribute, context: context, scene);
						return true;
					}
				}
				else if(globalServiceInfo.SceneName is { Length: > 0 } sceneName)
				{
					var sceneAsset = Find.SceneAssetByName(sceneName);
					if(sceneAsset)
					{
						var scene = SceneManager.GetSceneByName(sceneName);
						var context = scene.isLoaded && Find.Any(serviceType, out var service) && Find.In(service, out Transform transform) ? transform : null;
						result = new(serviceOrProvider:sceneAsset, registerer: scriptWithAttribute, context: context, scene);
						return true;
					}
				}
				else if(globalServiceInfo.ResourcePath is { Length: > 0 } resourcePath)
				{
					var resource = Resources.Load<Object>(resourcePath);
					if(resource)
					{
						result = new(serviceOrProvider:resource, registerer: scriptWithAttribute, context: null, default);
						return true;
					}
				}
				#if UNITY_ADDRESSABLES_1_17_4_OR_NEWER
				else if(globalServiceInfo.AddressableKey is { Length: > 0 } addressableKey)
				{
					var addressableSettings = AddressableAssetSettingsDefaultObject.Settings;
					foreach(var group in addressableSettings.groups)
					{
						foreach(var entry in group.entries)
						{
							var allEntries = new List<AddressableAssetEntry>();
							entry.GatherAllAssets(allEntries, true, true, false);
							foreach(var assetEntry in allEntries)
							{
								if(!string.Equals(assetEntry.address, addressableKey))
								{
									continue;
								}

								var asset = AssetDatabase.LoadAssetAtPath<Object>(assetEntry.AssetPath);
								if(!asset)
								{
									continue;
								}

								result = new(serviceOrProvider:asset, registerer: scriptWithAttribute, context: null, default);
								return true;
							}
						}
					}
				}
				#endif
				else if(scriptWithAttribute)
				{
					result = new(serviceOrProvider:null, registerer: scriptWithAttribute, context: null, default);
					return true;
				}
			}

			result = default;
			return false;
		}

		internal static void SelectAllGlobalServiceClientsInScene(object serviceOrServiceProvider)
			=> Selection.objects = GetServiceDefiningTypes(serviceOrServiceProvider).ToArray().SelectMany(FindAllReferences).Distinct().ToArray();

		internal static void SelectAllReferencesInScene(object serviceOrServiceProvider, Type[] definingTypes, Clients clients, [DisallowNull] Component registerer)
			=> Selection.objects = definingTypes.SelectMany(FindAllReferences).Distinct().Where(go => Service.IsAccessibleTo(go.transform, registerer, clients)).ToArray();

		/// <summary>
		/// Ping MonoScript or GameObject containing the configuration that causes the object, or the value provided by the object
		/// (in the case of a wrapper component etc.), to be a service.
		/// </summary>
		/// <param name="serviceOrServiceProvider">
		/// An object which is a service, or an object which provides the services, such as an <see cref="IWrapper"/>.
		/// </param>
		internal static void PingServiceDefiningObject(Object serviceOrServiceProvider)
		{
			bool serviceOrServiceProviderIsInactive = serviceOrServiceProvider is Component component && !component.gameObject.activeInHierarchy;
			foreach(var serviceTag in ServiceTag.GetAllEditorOnly(serviceOrServiceProviderIsInactive))
			{
				if(AreEqual(serviceTag.Service, serviceOrServiceProvider))
				{
					EditorGUIUtility.PingObject(serviceTag);
					return;
				}
			}
			
			var services = Find.All<Services>().FirstOrDefault(s => s.providesServices.Any(i => AreEqual(i.service, serviceOrServiceProvider)));
			if(services)
			{
				EditorGUIUtility.PingObject(services);
				return;
			}

			// Ping MonoScript that contains the ServiceAttribute, if found...
			var serviceOrServiceProviderType = serviceOrServiceProvider.GetType();
			if(HasServiceAttribute(serviceOrServiceProviderType))
			{
				var scriptWithServiceAttribute = Find.Script(serviceOrServiceProviderType);
				if(scriptWithServiceAttribute)
				{
					EditorGUIUtility.PingObject(scriptWithServiceAttribute);
					return;
				}
			}

			// Ping MonoScript of ServiceInitializer
			foreach(Type serviceInitializerType in TypeUtility.GetImplementingTypes<IServiceInitializer>())
			{
				if(serviceInitializerType.IsGenericType && !serviceInitializerType.IsGenericTypeDefinition && !serviceInitializerType.IsAbstract
				&& serviceInitializerType.GetGenericArguments()[0] == serviceOrServiceProviderType && HasServiceAttribute(serviceInitializerType)
				&& Find.Script(serviceInitializerType, out var serviceInitializerScript))
				{
					EditorGUIUtility.PingObject(serviceInitializerScript);
					return;
				}
			}

			if(serviceOrServiceProvider is IWrapper)
			{
				foreach(Type interfaceType in serviceOrServiceProviderType.GetInterfaces())
				{
					if(interfaceType.IsGenericType && interfaceType.GetGenericTypeDefinition() == typeof(IWrapper<>)
					&& Find.Script(interfaceType.GetGenericArguments()[0], out var serviceInitializerScript))
					{
						EditorGUIUtility.PingObject(serviceInitializerScript);
						return;
					}
				}
			}
		}

		private static bool HasServiceAttribute(Type type) => type.GetCustomAttributes<ServiceAttribute>().Any();

		internal static bool CanAddServiceTag(Component service) => GetServiceDefiningTypes(service).Length is 0;

		internal static void OpenToClientsMenu(Component serviceOrServiceProvider, Rect tagRect)
		{
			var tags = GetServiceTagsTargeting(serviceOrServiceProvider).ToArray();
			
			// If has no ServiceTag component then ping defining object.
			if(tags.Length == 0)
			{
				PingServiceDefiningObject(serviceOrServiceProvider);
				return;
			}

			GUI.changed = true;

			var selectedValue = tags[0].ToClients;
			if(selectedValue is < 0 or > Clients.Everywhere)
			{
				#if DEV_MODE
				Debug.LogWarning(selectedValue);
				#endif
				selectedValue = Clients.Everywhere;
			}

			ClientsDropdownWindow.Show(tagRect, new ClientsDataSource(new() { selectedValue }), OnItemSelected);

			void OnItemSelected(Clients value)
			{
				Undo.RecordObjects(tags, "Set Availability");
				foreach(var serviceTag in tags)
				{
					if(value == serviceTag.ToClients)
					{
						Undo.DestroyObjectImmediate(serviceTag);
					}
					else
					{
						serviceTag.ToClients = value;
					}
				}
			}
		}

		internal static void OpenContextMenuForService(Component serviceOrServiceProvider, Type[] definingTypes, Clients clients, [DisallowNull] Component registerer, Rect tagRect)
		{
			var menu = new GenericMenu();

			menu.AddItem(new("Find Clients In Scenes"), false, () => SelectAllReferencesInScene(serviceOrServiceProvider, definingTypes, clients, registerer));

			if(HasServiceTag(serviceOrServiceProvider))
			{
				menu.AddItem(new("Set Defining Types..."), false, () => openSelectTagsMenuFor = serviceOrServiceProvider);

				var tagRectScreenSpace = tagRect;
				tagRectScreenSpace.y += GUIUtility.GUIToScreenPoint(Vector2.zero).y;
				if(EditorWindow.mouseOverWindow)
				{
					tagRectScreenSpace.y -= EditorWindow.mouseOverWindow.position.y;
				}

				menu.AddItem(new("Set Availability..."), false, () => OpenToClientsMenu(serviceOrServiceProvider, tagRectScreenSpace));
			}
			else if(!(serviceOrServiceProvider is Services))
			{
				menu.AddItem(new("Find Defining Object"), false, () => PingServiceDefiningObject(serviceOrServiceProvider));
			}

			menu.DropDown(tagRect);
		}
		
		internal static void OpenContextMenuForServiceOfClient(Object client, Type serviceDefiningType, Rect tagRect)
		{
			if(!TryGetServicePingInfo(client, serviceDefiningType, out var info))
			{
				return;
			}

			var menu = new GenericMenu();

			if(info.ServiceOrProvider)
			{
				menu.AddItem(new("Find Service"), false, () => Ping(info.ServiceOrProvider));
			}

			if(info.Registerer && !ReferenceEquals(info.Registerer, info.ServiceOrProvider))
			{
				menu.AddItem(new("Find Definition"), false, () => Ping(info.Registerer));
			}

			if(info.Script is { } script && !ReferenceEquals(script, info.Registerer))
			{
				menu.AddItem(new("Find Script"), false, () => Ping(script));
			}

			menu.DropDown(tagRect);

			static void Ping(Object target) => EditorGUIUtility.PingObject(target is Component component ? component.gameObject : target);
		}

		internal static void OpenSelectDefiningTypesMenu(Component service, Rect tagRect)
		{
			if(!CanAddServiceTag(service) && !HasServiceTag(service))
			{
				return;
			}

			GUI.changed = true;

			var selectedTypes = GetServiceTagsTargeting(service).Select(tag => tag.DefiningType).ToHashSet(); // was "Service Types"
			TypeDropdownWindow.Show(tagRect, new DefiningTypeDataSource(service, selectedTypes), OnTypeSelected);

			void OnTypeSelected(Type selectedType)
			{
				Undo.RecordObject(service, "Set Service Type");

				if(ServiceTag.Remove(service, selectedType))
				{
					return;
				}

				ServiceTag.Add(service, selectedType);
				InspectorContents.RepaintEditorsWithTarget(service);
			}
		}

		/// <summary>
		/// NOTE: Can contain duplicate types. Use Distinct() if needed.
		/// </summary>
		internal static IEnumerable<Type> GetDefiningTypeOptions(object serviceOrServiceProvider)
		{
			if(serviceOrServiceProvider is null)
			{
				yield break;
			}

			var serviceOrProviderType = serviceOrServiceProvider.GetType();
			yield return serviceOrProviderType;

			if (serviceOrServiceProvider is IValueProvider or IValueProviderAsync)
			{
				foreach(var interfaceType in serviceOrProviderType.GetInterfaces())
				{
					if(!interfaceType.IsGenericType
						|| interfaceType.GetGenericTypeDefinition() is not { } typeDefinition
						|| (typeDefinition != typeof(IValueProvider<>) && typeDefinition != typeof(IValueProviderAsync<>)))
					{
						continue;
					}

					var valueType = interfaceType.GetGenericArguments()[0];
					if(TypeUtility.IsNullOrBaseType(valueType))
					{
						continue;
					}

					yield return valueType;

					if(valueType.IsValueType)
					{
						continue;
					}

					for(var baseType = valueType.BaseType; !TypeUtility.IsNullOrBaseType(baseType); baseType = baseType.BaseType)
					{
						yield return baseType;
					}

					if(!valueType.IsInterface)
					{
						foreach(var derivedType in TypeCache.GetTypesDerivedFrom(valueType))
						{
							yield return derivedType;
						}

						foreach(var valueTypeInterface in valueType.GetInterfaces().Where(IncludeInterface))
						{
							yield return valueTypeInterface;
						}
					}
				}
			}
			else if (serviceOrServiceProvider is IValueByTypeProvider valueByTypeProvider)
			{
				foreach(var type in valueByTypeProvider.GetSupportedValueTypes())
				{
					yield return type;
				}
			}
			else if (serviceOrServiceProvider is IValueByTypeProviderAsync valueByTypeProviderAsync)
			{
				foreach(var type in valueByTypeProviderAsync.GetSupportedValueTypes())
				{
					yield return type;
				}
			}

			for(var baseType = serviceOrProviderType.BaseType; !TypeUtility.IsNullOrBaseType(baseType); baseType = baseType.BaseType)
			{
				yield return baseType;
			}

			foreach(var derivedType in TypeCache.GetTypesDerivedFrom(serviceOrProviderType))
			{
				yield return derivedType;
			}
			
			foreach(var serviceOrProviderInterface in serviceOrProviderType.GetInterfaces().Where(IncludeInterface))
			{
				yield return serviceOrProviderInterface;
			}

			static bool IncludeInterface(Type type) => type.IsGenericType ? !ignoredGenericTypes.Contains(type.IsGenericTypeDefinition ? type : type.GetGenericTypeDefinition()) : !ignoredNonGenericTypes.Contains(type);
		}

		private static bool AreEqual(Type[] pooledArray, in Span<Type> types, HashSet<Type> helperHashSet)
		{
			int pooledArrayLength = pooledArray.Length;
			int typeCount = types.Length;
			if(pooledArrayLength < typeCount)
			{
				return false;
			}

			if(pooledArrayLength > typeCount && pooledArray[typeCount] is not null)
			{
				return false;
			}

			helperHashSet.Clear();
			for (int i = 0; i < typeCount; i++)
			{
				helperHashSet.Add(types[i]);
			}

			for (int i = 0; i < typeCount; i++)
			{
				if (!helperHashSet.Contains(types[i]))
				{
					return false;
				}
			}

			return true;
		}

		private static bool AreEqual(object x, object y)
		{
			if(ReferenceEquals(x, y))
			{
				return true;
			}

			if(x is IWrapper xWrapper)
			{
				if(ReferenceEquals(xWrapper.WrappedObject, y))
				{
					return true;
				}

				if(y is IWrapper yWrapper)
				{
					if(ReferenceEquals(xWrapper.WrappedObject, yWrapper.WrappedObject))
					{
						return true;
					}
				}
			}
			else if(y is IWrapper yWrapper)
			{
				if(ReferenceEquals(x, yWrapper.WrappedObject))
				{
					return true;
				}
			}

			return false;
		}

		private static IEnumerable<GameObject> FindAllReferences(Type serviceType)
		{
			for(int s = SceneManager.sceneCount - 1; s >= 0; s--)
			{
				var scene = SceneManager.GetSceneAt(s);
				var rootGameObjects = scene.GetRootGameObjects();
				for(int r = rootGameObjects.Length - 1; r >= 0; r--)
				{
					foreach(var reference in FindAllReferences(rootGameObjects[r].transform, serviceType))
					{
						yield return reference;
					}
				}
			}
		}

		private static IEnumerable<GameObject> FindAllReferences(Transform transform, Type serviceType)
		{
			var components = transform.gameObject.GetComponentsNonAlloc<Component>();

			// Skip component at index 0 which is most likely a Transform.
			for(int c = components.Count - 1; c >= 1; c--)
			{
				var component = components[c];
				if(!component)
				{
					continue;
				}

				var componentType = component.GetType();

				if(component is IOneArgument)
				{
					if(componentType.GetInterfaces().FirstOrDefault(i => i.IsGenericType && i.GetGenericTypeDefinition() == typeof(IArgs<>)) is Type initializableType)
					{
						var argTypes = initializableType.GetGenericArguments();
						if(argTypes[0] == serviceType)
						{
							yield return component.gameObject;
							continue;
						}
					}
				}
				else if(component is ITwoArguments)
				{
					if(componentType.GetInterfaces().FirstOrDefault(i => i.IsGenericType && i.GetGenericTypeDefinition() == typeof(IArgs<,>)) is Type initializableType)
					{
						var argTypes = initializableType.GetGenericArguments();
						if(argTypes[0] == serviceType || argTypes[1] == serviceType)
						{
							yield return component.gameObject;
							continue;
						}
					}
				}
				else if(component is IThreeArguments)
				{
					if(componentType.GetInterfaces().FirstOrDefault(i => i.IsGenericType && i.GetGenericTypeDefinition() == typeof(IArgs<,,>)) is Type initializableType)
					{
						var argTypes = initializableType.GetGenericArguments();
						if(argTypes[0] == serviceType || argTypes[1] == serviceType || argTypes[2] == serviceType)
						{
							yield return component.gameObject;
							continue;
						}
					}
				}
				else if(component is IFourArguments)
				{
					if(componentType.GetInterfaces().FirstOrDefault(i => i.IsGenericType && i.GetGenericTypeDefinition() == typeof(IArgs<,,,>)) is Type initializableType)
					{
						var argTypes = initializableType.GetGenericArguments();
						if(argTypes[0] == serviceType || argTypes[1] == serviceType || argTypes[2] == serviceType || argTypes[3] == serviceType)
						{
							yield return component.gameObject;
							continue;
						}
					}
				}
				else if(component is IFiveArguments)
				{
					if(componentType.GetInterfaces().FirstOrDefault(i => i.IsGenericType && i.GetGenericTypeDefinition() == typeof(IArgs<,,,,>)) is Type initializableType)
					{
						var argTypes = initializableType.GetGenericArguments();
						if(argTypes[0] == serviceType || argTypes[1] == serviceType || argTypes[2] == serviceType || argTypes[3] == serviceType || argTypes[4] == serviceType)
						{
							yield return component.gameObject;
							continue;
						}
					}
				}

				var serializedObject = new SerializedObject(component);
				var property = serializedObject.GetIterator();
				string serviceTypeName = serviceType.Name;
				string serviceTypeNameAlt = string.Concat("PPtr<", serviceTypeName, ">");
				
				if(property.NextVisible(true))
				{
					do
					{
						if(string.Equals(property.type, "Any`1") && property.GetValue() is object any)
						{
							Type anyValueType = any.GetType().GetGenericArguments()[0];
							if(anyValueType == serviceType)
							{
								yield return component.gameObject;
							}
						}
						else if((string.Equals(property.type, serviceTypeName) || string.Equals(property.type, serviceTypeNameAlt)) && property.GetType() == serviceType)
						{
							yield return component.gameObject;
						}
					}
					// Checking only surface level fields, not nested fields, for performance reasons.
					while(property.NextVisible(false));
				}
			}

			for(int i = transform.childCount - 1; i >= 0; i--)
			{
				foreach(var reference in FindAllReferences(transform.GetChild(i), serviceType))
				{
					yield return reference;
				}
			}
		}

		private readonly struct PingableServiceInfo
		{
			/// <summary>
			/// The service itself if it is an Object that already exists; otherwise, a value provider,
			/// initializer or wrapper, that will create or hold the service at runtime.
			/// </summary>
			[MaybeNull]
			public readonly Object ServiceOrProvider;

			/// <summary>
			/// ServiceTag, Services component or the MonoScript with the ServiceAttribute.
			/// </summary>
			[MaybeNull]
			public readonly Object Registerer;

			/// <summary>
			/// MonoScript of the service or its provider.
			/// </summary>
			[MaybeNull]
			public MonoScript Script => Registerer is MonoScript script ? script : ServiceOrProvider ? Find.Script(ServiceOrProvider.GetType()) : null;

			[MaybeNull]
			public Object PingTarget => ServiceOrProvider ? ServiceOrProvider : Registerer ? Registerer : Script;
			
			public readonly Transform Context;
			public readonly Scene Scene;

			public PingableServiceInfo(Services services, Object serviceOrProvider) : this(serviceOrProvider, services, services.transform, services.gameObject.scene) { }

			public PingableServiceInfo(Object serviceOrProvider, Object registerer, Transform context, Scene scene) //Y, PingTargetType pingTargetType, object serviceProvider, ServiceProviderType providerType)
			{
				ServiceOrProvider = serviceOrProvider;
				Registerer = registerer;
				Context = context;
				Scene = scene;
			}
			public static implicit operator PingableServiceInfo(Service.ActiveServiceInfo activeServiceInfo) => new(activeServiceInfo.ServiceOrProvider as Object, activeServiceInfo.Registerer, activeServiceInfo.Transform, activeServiceInfo.Scene);
			public static implicit operator PingableServiceInfo(ServiceTag serviceTag) => new(serviceTag.Service, serviceTag, serviceTag.transform, serviceTag.gameObject.scene);

			public bool Ping()
			{
				if(PingTarget is not { } pingTarget || !pingTarget)
				{
					return false;
				}

				Ping(pingTarget);
				return true;

				static void Ping(Object target) => EditorGUIUtility.PingObject(target is Component component ? component.gameObject : target);
			}
		}

		private static readonly HashSet<Type> ignoredNonGenericTypes = new()
		{
			typeof(ISerializationCallbackReceiver),
			typeof(IOneArgument),
			typeof(ITwoArguments),
			typeof(IThreeArguments),
			typeof(IFourArguments),
			typeof(IFiveArguments),
			typeof(ISixArguments),
			typeof(ISevenArguments),
			typeof(IEightArguments),
			typeof(INineArguments),
			typeof(ITenArguments),
			typeof(IElevenArguments),
			typeof(ITwelveArguments),
			typeof(IWrapper),
			typeof(IInitializable),
			typeof(IInitializableEditorOnly),
			typeof(IEnableable),
			typeof(IComparable),
			typeof(IFormattable),
			typeof(IConvertible),
			typeof(IDisposable),
			typeof(IValueByTypeProvider),
			typeof(IValueByTypeProviderAsync),
			typeof(INullGuard),
			typeof(INullGuardByType),
			typeof(IEnumerable),
			typeof(ICloneable),
			typeof(IValueProvider),
			typeof(IValueProviderAsync),
			typeof(IValueByTypeProvider),
			typeof(IValueByTypeProviderAsync)
		};
		
		private static readonly HashSet<Type> ignoredGenericTypes = new()
		{
			typeof(IEquatable<>),
			typeof(IComparable<>),
			typeof(IWrapper<>),
			typeof(IFirstArgument<>),
			typeof(ISecondArgument<>),
			typeof(IThirdArgument<>),
			typeof(IFourthArgument<>),
			typeof(IFifthArgument<>),
			typeof(ISixthArgument<>),
			typeof(ISeventhArgument<>),
			typeof(IEighthArgument<>),
			typeof(INinthArgument<>),
			typeof(ITenthArgument<>),
			typeof(IEleventhArgument<>),
			typeof(ITwelfthArgument<>),
			typeof(IArgs<>),
			typeof(IArgs<,>),
			typeof(IArgs<,,>),
			typeof(IArgs<,,,>),
			typeof(IArgs<,,,,>),
			typeof(IArgs<,,,,,>),
			typeof(IArgs<,,,,,,>),
			typeof(IArgs<,,,,,,,>),
			typeof(IArgs<,,,,,,,,>),
			typeof(IArgs<,,,,,,,,,>),
			typeof(IArgs<,,,,,,,,,,>),
			typeof(IArgs<,,,,,,,,,,,>),
			typeof(IInitializable<>),
			typeof(IInitializable<,>),
			typeof(IInitializable<,,>),
			typeof(IInitializable<,,,>),
			typeof(IInitializable<,,,,>),
			typeof(IInitializable<,,,,,>),
			typeof(IInitializable<,,,,,,>),
			typeof(IInitializable<,,,,,,,>),
			typeof(IInitializable<,,,,,,,,>),
			typeof(IInitializable<,,,,,,,,,>),
			typeof(IInitializable<,,,,,,,,,,>),
			typeof(IInitializable<,,,,,,,,,,,>),
			typeof(IValueProvider<>),
			typeof(IValueProviderAsync<>)
		};

		#if DEV_MODE && DEBUG && !INIT_ARGS_DISABLE_PROFILING
		private static readonly ProfilerMarker getServiceDefiningTypesMarker = new(ProfilerCategory.Gui, "EditorServiceTagUtility.GetServiceDefiningTypes");
		#endif
	}
}