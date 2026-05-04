using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Linq;
using System.Reflection;
using System.Runtime.Serialization;
using Sisus.Init.Internal;
using Sisus.Init.Reflection;
using Sisus.Shared.EditorOnly;
using UnityEditor;
using UnityEditorInternal;
using UnityEngine;
using static Sisus.Init.Internal.InitializableUtility;
using static Sisus.Init.Internal.InitializerUtility;
using Object = UnityEngine.Object;

#if ODIN_INSPECTOR
using Sirenix.OdinInspector.Editor;
#endif

#if DEV_MODE && DEBUG && !INIT_ARGS_DISABLE_PROFILING
using Unity.Profiling;
#endif

namespace Sisus.Init.EditorOnly.Internal
{
	internal static class InitializerEditorUtility
	{
		private const BindingFlags AnyDeclaredInstance = BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.DeclaredOnly;
		internal static readonly Color nullGuardFailedColor = new(1f, 0.82f, 0f, 1f);
		internal static readonly Color nullGuardWarningColor = new(0.4f, 0.6f, 1f, 1f);

		private static readonly GUIContent clientNullTooltip = new("", "A new instance will be added to this GameObject during initialization.");
		private static readonly GUIContent clientPrefabTooltip = new("", "A new instance will be created by cloning this prefab during initialization.");
		private static readonly GUIContent clientInstantiateTooltip = new("", "A new instance will be created by cloning this scene object during initialization.");
		private static readonly GUIContent clientNotInitializableTooltip = new("", "Can not inject arguments to client because it does not implement IInitializable.");
		private static readonly GUIContent tempLabel = new();
		private static GUIContent warningIcon;
		private static GUIContent prefabIcon;
		private static GUIContent gameObjectIcon;
		private static GUIContent instantiateOverlayIcon;
		private static GUIContent scriptableObjectIcon;

		private static readonly HashSet<Type> initializableEditors = new(12)
		{
			typeof(InitializableT1EditorDecorator),
			typeof(InitializableT2EditorDecorator),
			typeof(InitializableT3EditorDecorator),
			typeof(InitializableT4EditorDecorator),
			typeof(InitializableT5EditorDecorator),
			typeof(InitializableT6EditorDecorator),
			typeof(InitializableT7EditorDecorator),
			typeof(InitializableT8EditorDecorator),
			typeof(InitializableT9EditorDecorator),
			typeof(InitializableT10EditorDecorator),
			typeof(InitializableT11EditorDecorator),
			typeof(InitializableT12EditorDecorator)
		};

		private static readonly Dictionary<int, Type> initializableEditorDecoratorsByArgumentCount = new(12)
		{
			{ 1,  typeof(InitializableT1EditorDecorator) },
			{ 2,  typeof(InitializableT2EditorDecorator) },
			{ 3,  typeof(InitializableT3EditorDecorator) },
			{ 4,  typeof(InitializableT4EditorDecorator) },
			{ 5,  typeof(InitializableT5EditorDecorator) },
			{ 6,  typeof(InitializableT6EditorDecorator) },
			{ 7,  typeof(InitializableT7EditorDecorator) },
			{ 8,  typeof(InitializableT8EditorDecorator) },
			{ 9,  typeof(InitializableT9EditorDecorator) },
			{ 10, typeof(InitializableT10EditorDecorator) },
			{ 11, typeof(InitializableT11EditorDecorator) },
			{ 12, typeof(InitializableT12EditorDecorator) }
		};

		private static readonly Dictionary<Type, Type> editorDecoratorTypes = new()
		{
			{ typeof(IInitializable<>), typeof(InitializableT1EditorDecorator) },
			{ typeof(IInitializable<,>), typeof(InitializableT2EditorDecorator) },
			{ typeof(IInitializable<,,>), typeof(InitializableT3EditorDecorator) },
			{ typeof(IInitializable<,,,>), typeof(InitializableT4EditorDecorator) },
			{ typeof(IInitializable<,,,,>), typeof(InitializableT5EditorDecorator) },
			{ typeof(IInitializable<,,,,,>), typeof(InitializableT6EditorDecorator) },
			{ typeof(IInitializable<,,,,,,>), typeof(InitializableT7EditorDecorator) },
			{ typeof(IInitializable<,,,,,,,>), typeof(InitializableT8EditorDecorator) },
			{ typeof(IInitializable<,,,,,,,,>), typeof(InitializableT9EditorDecorator) },
			{ typeof(IInitializable<,,,,,,,,,>), typeof(InitializableT10EditorDecorator) },
			{ typeof(IInitializable<,,,,,,,,,,>), typeof(InitializableT11EditorDecorator) },
			{ typeof(IInitializable<,,,,,,,,,,,>), typeof(InitializableT12EditorDecorator) },
			{ typeof(MonoBehaviour<>), typeof(MonoBehaviourT1EditorDecorator) },
			{ typeof(MonoBehaviour<,>), typeof(MonoBehaviourT2EditorDecorator) },
			{ typeof(MonoBehaviour<,,>), typeof(MonoBehaviourT3EditorDecorator) },
			{ typeof(MonoBehaviour<,,,>), typeof(MonoBehaviourT4EditorDecorator) },
			{ typeof(MonoBehaviour<,,,,>), typeof(MonoBehaviourT5EditorDecorator) },
			{ typeof(MonoBehaviour<,,,,,>), typeof(MonoBehaviourT6EditorDecorator) },
			{ typeof(MonoBehaviour<,,,,,,>), typeof(MonoBehaviourT7EditorDecorator) },
			{ typeof(MonoBehaviour<,,,,,,,>), typeof(MonoBehaviourT8EditorDecorator) },
			{ typeof(MonoBehaviour<,,,,,,,,>), typeof(MonoBehaviourT9EditorDecorator) },
			{ typeof(MonoBehaviour<,,,,,,,,,>), typeof(MonoBehaviourT10EditorDecorator) },
			{ typeof(MonoBehaviour<,,,,,,,,,,>), typeof(MonoBehaviourT11EditorDecorator) },
			{ typeof(MonoBehaviour<,,,,,,,,,,,>), typeof(MonoBehaviourT12EditorDecorator) },
			{ typeof(StateMachineBehaviour<>), typeof(StateMachineBehaviourT1EditorDecorator) },
			{ typeof(StateMachineBehaviour<,>), typeof(StateMachineBehaviourT2EditorDecorator) },
			{ typeof(StateMachineBehaviour<,,>), typeof(StateMachineBehaviourT3EditorDecorator) },
			{ typeof(StateMachineBehaviour<,,,>), typeof(StateMachineBehaviourT4EditorDecorator) },
			{ typeof(StateMachineBehaviour<,,,,>), typeof(StateMachineBehaviourT5EditorDecorator) },
			{ typeof(StateMachineBehaviour<,,,,,>), typeof(StateMachineBehaviourT6EditorDecorator) },
			{ typeof(Wrapper<>), typeof(WrapperEditorDecorator) },
			{ typeof(Animator), typeof(AnimatorEditorDecorator) }
		};

		private static readonly Dictionary<int, string[]> propertyNamesByArgumentCount = new(12)
		{
			{ 1, new[] {"argument"} },
			{ 2, new[] {"firstArgument", "secondArgument"} },
			{ 3, new[] {"firstArgument", "secondArgument", "thirdArgument"} },
			{ 4, new[] {"firstArgument", "secondArgument", "thirdArgument", "fourthArgument"} },
			{ 5, new[] {"firstArgument", "secondArgument", "thirdArgument", "fourthArgument", "fifthArgument"} },
			{ 6, new[] {"firstArgument", "secondArgument", "thirdArgument", "fourthArgument", "fifthArgument", "sixthArgument"} },
			{ 7, new[] {"firstArgument", "secondArgument", "thirdArgument", "fourthArgument", "fifthArgument", "sixthArgument", "seventhArgument" } },
			{ 8, new[] {"firstArgument", "secondArgument", "thirdArgument", "fourthArgument", "fifthArgument", "sixthArgument", "seventhArgument", "eighthArgument" } },
			{ 9, new[] {"firstArgument", "secondArgument", "thirdArgument", "fourthArgument", "fifthArgument", "sixthArgument", "seventhArgument", "eighthArgument", "ninthArgument" } },
			{ 10, new[] {"firstArgument", "secondArgument", "thirdArgument", "fourthArgument", "fifthArgument", "sixthArgument", "seventhArgument", "eighthArgument", "ninthArgument", "tenthArgument" } },
			{ 11, new[] {"firstArgument", "secondArgument", "thirdArgument", "fourthArgument", "fifthArgument", "sixthArgument", "seventhArgument", "eighthArgument", "ninthArgument", "tenthArgument", "eleventhArgument" } },
			{ 12, new[] {"firstArgument", "secondArgument", "thirdArgument", "fourthArgument", "fifthArgument", "sixthArgument", "seventhArgument", "eighthArgument", "ninthArgument", "tenthArgument", "eleventhArgument", "twelfthArgument" } }
		};

		/// <summary>
		/// Maps initializer Init argument property names to their zero-based Init argument indexes.
		/// </summary>
		private static readonly Dictionary<string, int> propertyNameToInitParameterIndex = new(13)
		{
			{ "argument", 0 },
			{ "firstArgument", 0 },
			{ "secondArgument", 1 },
			{ "thirdArgument", 2 },
			{ "fourthArgument", 3 },
			{ "fifthArgument",  4 },
			{ "sixthArgument", 5 },
			{ "seventhArgument", 6 },
			{ "eighthArgument", 7 },
			{ "ninthArgument", 8 },
			{ "tenthArgument", 9 },
			{ "eleventhArgument", 10 },
			{ "twelfthArgument", 11 }
		};

		private static Color ObjectFieldBackgroundColor => EditorGUIUtility.isProSkin ? new Color32(42, 42, 42, 255) : new Color32(237, 237, 237, 255);

		static InitializerEditorUtility()
		{
			foreach(var editorDecoratorType in TypeCache.GetTypesWithAttribute<CustomEditorDecoratorAttribute>())
			{
				var attribute = editorDecoratorType.GetCustomAttribute<CustomEditorDecoratorAttribute>();
				editorDecoratorTypes[attribute.TargetType] = editorDecoratorType;
			}
		}

		internal static bool IsInitializableEditorType(Type editorType) => initializableEditors.Contains(editorType);

		internal static bool HasAnyInitializerTypes(Type clientType, bool matchInitializersForDerivedAndInterfaceTypes) => GetInitializerTypes(clientType, matchInitializersForDerivedAndInterfaceTypes).Any();

		internal static IEnumerable<Type> GetInitializerTypes(Type clientType, bool matchInitializersForDerivedAndInterfaceTypes)
		{
			foreach(var initializerType in TypeCache.GetTypesDerivedFrom(typeof(IInitializer)))
			{
				if(IsInitializerFor(initializerType, clientType, matchInitializersForDerivedAndInterfaceTypes))
				{
					yield return initializerType;
				}
			}
		}

		internal static Type[] GetInitParameterTypes([DisallowNull] Object initializable)
		{
			if(TryGetInitializer(initializable, out IInitializer initializer))
			{
				foreach(Type interfaceType in initializer.GetType().GetInterfaces())
				{
					if(interfaceType.IsGenericType && argumentCountsByIInitializerTypeDefinition.ContainsKey(interfaceType.GetGenericTypeDefinition()))
					{
						return interfaceType.GetGenericArguments().Skip(1).ToArray();
					}
				}
			}

			Type[] results = null;
			var initializableType = initializable.GetType();
			foreach(Type interfaceType in initializableType.GetInterfaces())
			{
				if(interfaceType.IsGenericType
				&& argumentCountsByIInitializableTypeDefinition.TryGetValue(interfaceType.GetGenericTypeDefinition(), out int argumentCount)
				&& (results is null || results.Length < argumentCount))
				{
					results = interfaceType.GetGenericArguments();
				}
			}

			if(results is not null)
			{
				return results;
			}

			if(GetInitializerTypes(initializableType, matchInitializersForDerivedAndInterfaceTypes: true)
				.Select(GetInitParameters)
				.OrderByDescending(x => x.Length).FirstOrDefault() is { Length: > 0 } initializerInitParameters)
			{
				return initializerInitParameters;
			}

			return Array.Empty<Type>();

			Type[] GetInitParameters(Type initializerType)
			{
				foreach(Type interfaceType in initializerType.GetInterfaces())
				{
					if(interfaceType.IsGenericType && argumentCountsByIInitializerTypeDefinition.ContainsKey(interfaceType.GetGenericTypeDefinition()))
					{
						return interfaceType.GetGenericArguments().Skip(1).ToArray();
					}
				}

				return Array.Empty<Type>();
			}
		}
		internal static Type GetClientType([DisallowNull] Type initializerType)
		{
			foreach(Type interfaceType in initializerType.GetInterfaces())
			{
				if(interfaceType.IsGenericType && argumentCountsByIInitializerTypeDefinition.ContainsKey(interfaceType.GetGenericTypeDefinition()))
				{
					return interfaceType.GetGenericArguments()[0];
				}
			}

			return null;
		}

		internal static bool IsGenericIInitializableType(Type interfaceType) => interfaceType.IsGenericType && argumentCountsByIInitializableTypeDefinition.ContainsKey(interfaceType.GetGenericTypeDefinition());

		internal static bool TryGetEditorDecoratorType(Type inspectedType, out Type editorDecoratorType)
		{
			if(editorDecoratorTypes.TryGetValue(inspectedType, out editorDecoratorType))
			{
				return editorDecoratorType is not null;
			}

			const int OtherType = 0;
			const int ComponentType = 1;
			const int ScriptableObjectType = 2;
			const int StateMachineBehaviourType = 3;
			var type = typeof(MonoBehaviour).IsAssignableFrom(inspectedType) ? ComponentType
						: typeof(ScriptableObject).IsAssignableFrom(inspectedType) ? ScriptableObjectType
						: typeof(StateMachineBehaviour).IsAssignableFrom(inspectedType) ? StateMachineBehaviourType
						: OtherType;

			if(type is not OtherType)
			{
				// Don't use an EditorDecorator for value providers that are drawn inlined
				// within the AnyPropertyDrawer - we don't want the Init section to appear for those.
				if(type is ScriptableObjectType && inspectedType.IsDefined(typeof(ValueProviderMenuAttribute)))
				{
					editorDecoratorTypes.Add(inspectedType, null);
					return false;
				}

				for(var typeOrBaseType = inspectedType; typeOrBaseType != null; typeOrBaseType = typeOrBaseType.BaseType)
				{
					if(typeOrBaseType.IsGenericType
					? editorDecoratorTypes.TryGetValue(typeOrBaseType.GetGenericTypeDefinition(), out editorDecoratorType)
					: editorDecoratorTypes.TryGetValue(typeOrBaseType, out editorDecoratorType))
					{
						editorDecoratorTypes.Add(inspectedType, editorDecoratorType);
						return editorDecoratorType is not null;
					}
				}

				int initArgumentCount = GetClientInitArgumentCount(inspectedType);
				if(initializableEditorDecoratorsByArgumentCount.TryGetValue(initArgumentCount, out editorDecoratorType))
				{
					editorDecoratorTypes.Add(inspectedType, editorDecoratorType);
					return true;
				}
			}

			bool hasAnyInitializer = false;
			foreach(var initializerType in type is ComponentType ? Find.typesToComponentTypes[typeof(IInitializer)] : Find.typesToFindableTypes[typeof(IInitializer)])
			{
				if(initializerType.IsAbstract)
				{
					continue;
				}

				foreach(var interfaceType in initializerType.GetInterfaces())
				{
					if(!interfaceType.IsGenericType)
					{
						continue;
					}

					if(!argumentCountsByIInitializerTypeDefinition.TryGetValue(interfaceType.GetGenericTypeDefinition(), out int argumentCount))
					{
						continue;
					}

					var initializerClientType = interfaceType.GetGenericArguments()[0];
					if(initializerClientType.IsAssignableFrom(inspectedType)
						&& (initializerClientType != typeof(object) || inspectedType == typeof(object)))
					{
						if(initializableEditorDecoratorsByArgumentCount.TryGetValue(argumentCount, out editorDecoratorType))
						{
							editorDecoratorTypes.Add(inspectedType, editorDecoratorType);
							return true;
						}

						hasAnyInitializer = true;
					}

					if(Find.typesToWrapperTypes.TryGetValue(initializerClientType, out Type[] wrapperTypes)
						&& Array.IndexOf(wrapperTypes, inspectedType) != -1)
					{
						if(initializableEditorDecoratorsByArgumentCount.TryGetValue(argumentCount, out editorDecoratorType))
						{
							editorDecoratorTypes.Add(inspectedType, editorDecoratorType);
							return true;
						}

						hasAnyInitializer = true;
					}
				}
			}

			if(hasAnyInitializer)
			{
				editorDecoratorType = typeof(InitializableEditorDecorator);
				editorDecoratorTypes.Add(inspectedType, editorDecoratorType);
				return true;
			}

			editorDecoratorTypes.Add(inspectedType, null);
			return false;
		}

		internal static void AddInitializer(Object[] clients, Type initializerType)
		{
			GUI.changed = true;
			int count = clients.Length;

			if(typeof(Component).IsAssignableFrom(initializerType))
			{
				for(int i = 0; i < count; i++)
				{
					var component = clients[i] as Component;
					if(component)
					{
						IInitializer initializer = (IInitializer)Undo.AddComponent(component.gameObject, initializerType);
						initializer.Target = component;
					}
				}

				return;
			}

			if(typeof(ScriptableObject).IsAssignableFrom(initializerType))
			{
				var selectionWas = Selection.objects;

				for(int i = 0; i < count; i++)
				{
					var scriptableObjectClient = clients[i] as ScriptableObject;
					if(scriptableObjectClient)
					{
						const string UNDO_NAME = "Add Initializer";
						Undo.RecordObject(scriptableObjectClient, UNDO_NAME);

						var initializerInstance = ScriptableObject.CreateInstance(initializerType);
						initializerInstance.name = "Initializer";
						Undo.RegisterCreatedObjectUndo(initializerInstance, UNDO_NAME);

						((IInitializer)initializerInstance).Target = scriptableObjectClient;
						AssetDatabase.StartAssetEditing();
						string path = AssetDatabase.GetAssetPath(scriptableObjectClient);
						EditorUtility.SetDirty(scriptableObjectClient);
						AssetDatabase.AddObjectToAsset(initializerInstance, path);
						AssetDatabase.StopAssetEditing();
						AssetDatabase.ImportAsset(path);

						foreach(var asset in AssetDatabase.LoadAllAssetsAtPath(path))
						{
							if(asset.GetType() == initializerType)
							{
								initializerInstance = asset as ScriptableObject;
								break;
							}
						}

						if(scriptableObjectClient is IInitializableEditorOnly initializableEditorOnly)
						{
							Undo.RecordObject(scriptableObjectClient, UNDO_NAME);
							initializableEditorOnly.Initializer = initializerInstance as IInitializer;
						}
					}

					Selection.objects = selectionWas;
				}
			}
		}

		/// <param name="targets"> The top-level components or scriptable objects being inspected. </param>
		/// <param name="client"> The object for which the initializer should be generated. </param>
		public static void GenerateAndAttachInitializer(Object[] targets, object client)
		{
			GUI.changed = true;

			string initializerPath = ScriptGenerator.CreateInitializer(client);
			var initializerScript = AssetDatabase.LoadAssetAtPath<MonoScript>(initializerPath);

			Debug.Log($"Initializer class created at \"{initializerPath}\".", initializerScript);

			var initializerGuid = AssetDatabase.AssetPathToGUID(initializerPath);
			EditorPrefs.SetString(InitializerGUI.SetInitializerTargetOnScriptsReloadedEditorPrefsKey, initializerGuid + ":" + string.Join(";", targets.Select(t =>
			{
				#if UNITY_6000_4_OR_NEWER
				return t.GetEntityId();
				#else
				return t.GetInstanceID();
				#endif
			})));

			if(initializerScript is null)
			{
				#if DEV_MODE
				Debug.LogWarning($"AssetDatabase.LoadAssetAtPath<MonoScript>({initializerPath}) returned null");
				#endif
				return;
			}

			var addScriptMethod = typeof(InternalEditorUtility).GetMethod("AddScriptComponentUncheckedUndoable", BindingFlags.Static | BindingFlags.NonPublic | BindingFlags.Public);
			if(addScriptMethod is null)
			{
				#if DEV_MODE
				Debug.LogWarning("Method InternalEditorUtility.AddScriptComponentUncheckedUndoable not found.");
				#endif
				return;
			}

			foreach(var target in targets)
			{
				if(target is Component component)
				{
					addScriptMethod.Invoke(null, new Object[] { component.gameObject, initializerScript });
				}
			}
		}

		private static bool IsInitializerFor(Type initializerType, Type clientType, bool matchInitializersForDerivedAndInterfaceTypes)
		{
			if(initializerType.IsAbstract)
			{
				return false;
			}

			foreach(Type interfaceType in initializerType.GetInterfaces())
			{
				if(!interfaceType.IsGenericType || interfaceType.GetGenericTypeDefinition() != typeof(IInitializer<>))
				{
					continue;
				}

				var initializerClientType = interfaceType.GetGenericArguments()[0];
				if(initializerClientType == clientType)
				{
					return true;
				}

				if(initializerClientType.IsAssignableFrom(clientType) && !TypeUtility.IsBaseType(initializerClientType) && matchInitializersForDerivedAndInterfaceTypes)
				{
					return true;
				}

				if(Find.typesToWrapperTypes.TryGetValue(initializerClientType, out Type[] wrapperTypes)
					&& Array.IndexOf(wrapperTypes, clientType) != -1)
				{
					return true;
				}
			}

			return false;
		}

		internal static void DrawClientField(Rect rect, SerializedProperty client, GUIContent clientLabel, bool isInitializable)
		{
			var reference = client.objectReferenceValue;
			var tooltipRect = rect;
			tooltipRect.x += 2f;
			tooltipRect.y += 2f;
			tooltipRect.width -= 21f;
			tooltipRect.height -= 3f;

			var mouseovered = rect.Contains(Event.current.mousePosition) && DragAndDrop.visualMode != DragAndDropVisualMode.None;
			if(!isInitializable && !mouseovered && TryGetTintForNullGuardResult(NullGuardResultType.Error, out Color errorColor))
			{
				GUI.color = errorColor;
			}

			if(!reference)
			{
				EditorGUI.ObjectField(rect, client, GUIContent.none);
				
				if(mouseovered)
				{
					return;
				}

				tempLabel.text = clientLabel.text;
				tempLabel.tooltip = isInitializable ? clientNullTooltip.text : clientNotInitializableTooltip.text;

				EditorGUI.DrawRect(tooltipRect, ObjectFieldBackgroundColor);
				GUI.Label(tooltipRect, tempLabel);
			}
			else
			{
				Component component = reference as Component;
				var gameObject = component ? component.gameObject : null;
				bool isPrefab = gameObject && !gameObject.scene.IsValid();
				bool isSceneObject = gameObject && gameObject.scene.IsValid();
				bool isScriptableObject = reference is ScriptableObject;

				GUIContent icon;
				if(!isInitializable)
				{
					if(warningIcon == null)
					{
						warningIcon = EditorGUIUtility.IconContent("Warning");
					}

					icon = warningIcon;
				}
				else if(isPrefab)
				{
					if(prefabIcon == null)
					{
						prefabIcon = EditorGUIUtility.IconContent("Prefab Icon");
					}

					if(instantiateOverlayIcon == null)
					{
						instantiateOverlayIcon = EditorGUIUtility.IconContent("PrefabOverlayAdded Icon");
					}

					icon = prefabIcon;
				}
				else if(isSceneObject)
				{
					if(gameObjectIcon == null)
					{
						gameObjectIcon = EditorGUIUtility.IconContent("GameObject Icon");
					}

					if(instantiateOverlayIcon == null)
					{
						instantiateOverlayIcon = EditorGUIUtility.IconContent("PrefabOverlayAdded Icon");
					}

					icon = gameObjectIcon;
				}
				else if(isScriptableObject)
				{
					if(scriptableObjectIcon == null)
					{
						scriptableObjectIcon = EditorGUIUtility.IconContent("ScriptableObject Icon");
					}

					icon = scriptableObjectIcon;
				}
				else
				{
					icon = GUIContent.none;
				}

				var objectFieldRect = rect;
				if(icon.image)
				{
					objectFieldRect.x += 22f;
					objectFieldRect.width -= 22f;
				}

				EditorGUI.ObjectField(objectFieldRect, client, GUIContent.none);

				var tooltip = GetReferenceTooltip(client.serializedObject.targetObject, reference, isInitializable).tooltip;
				tempLabel.text = "";
				tempLabel.tooltip = tooltip;
				icon.tooltip = tooltip;

				GUI.Label(tooltipRect, tempLabel);

				var iconSize = EditorGUIUtility.GetIconSize();
				EditorGUIUtility.SetIconSize(new(15f, 15f));

				var iconRect = tooltipRect;
				iconRect.y -= 4f;
				iconRect.width = 20f;
				iconRect.height = 20f;
				if(GUI.Button(iconRect, icon, EditorStyles.label))
				{
					EditorGUIUtility.PingObject(gameObject ? gameObject : reference);
				}

				GUI.Label(iconRect, instantiateOverlayIcon);

				EditorGUIUtility.SetIconSize(iconSize);
			}

			GUI.color = Color.white;
		}

		internal static GUIContent GetReferenceTooltip(Object objectWithField, Object reference, bool isInitializable)
		{
			if(!isInitializable)
			{
				return clientNotInitializableTooltip;
			}

			if(!reference)
			{
				return clientNullTooltip;
			}

			var component = reference as Component;
			if(!component)
			{
				return GUIContent.none;
			}

			var gameObject = component.gameObject;
			if(!gameObject)
			{
				return GUIContent.none;
			}

			var gameObjectWithField = objectWithField is Component componentWithField ? componentWithField.gameObject : null;
			if(!gameObjectWithField || gameObjectWithField == gameObject)
			{
				return GUIContent.none;
			}

			bool isPrefab = !gameObject.scene.IsValid();
			return isPrefab ? clientPrefabTooltip : clientInstantiateTooltip;
		}

		private static string GetLabel([DisallowNull] Type type)
		{
			if(type.GetCustomAttribute<AddComponentMenu>() is not AddComponentMenu addComponentMenu)
			{
				return GetLabel(TypeUtility.ToString(type));
			}

			string menuPath = addComponentMenu.componentMenu;
			if(string.IsNullOrEmpty(menuPath))
			{
				return GetLabel(TypeUtility.ToString(type));
			}

			int nameStart = menuPath.LastIndexOf('/') + 1;
			return nameStart <= 0 ? menuPath : menuPath.Substring(nameStart);
		}

		internal static string GetLabel(string unnicifiedTypeOrFieldName)
		{
			unnicifiedTypeOrFieldName = ObjectNames.NicifyVariableName(unnicifiedTypeOrFieldName);

			if(unnicifiedTypeOrFieldName.StartsWith("I "))
			{
				unnicifiedTypeOrFieldName = unnicifiedTypeOrFieldName.Substring(2);
			}

			return unnicifiedTypeOrFieldName;
		}

		public static bool TryGetInitParameterAttributesFromMetadata(string initializerPropertyName, Type parameterType, Type metadataClass, [NotNullWhen(true)][MaybeNullWhen(false)] out Attribute[] results)
		{
			// "firstArgument" => 0, "secondArgument" => 1 etc.
			if(!propertyNameToInitParameterIndex.TryGetValue(initializerPropertyName, out int parameterIndex))
			{
				FieldInfo[] fields = metadataClass.GetFields(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
				if(parameterIndex < fields.Length)
				{
					var fieldAtIndex = fields[parameterIndex];
					if(fieldAtIndex.FieldType == parameterType)
					{
						results = Attribute.GetCustomAttributes(fieldAtIndex, typeof(Attribute));
						if(results.Length == 0)
						{
							results = null;
							return false;
						}

						return true;
					}
				}
			}

			if(!TryGetArgumentTargetFieldName(metadataClass, parameterType, parameter: null, parameterIndex, out string fieldName))
			{
				results = null;
				return false;
			}

			var field = metadataClass.GetField(fieldName, BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
			results = Attribute.GetCustomAttributes(field, typeof(Attribute));
			if(results.Length == 0)
			{
				results = null;
				return false;
			}

			return true;
		}

		public static InitParameterGUI[] CreateParameterGUIs(SerializedObject serializedObject, Type clientType, Type[] argumentTypes)
			=> GetPropertyDrawerData(serializedObject, clientType, argumentTypes, GetAnyFieldSerializedProperties(serializedObject, argumentTypes));

		private static SerializedProperty[] GetAnyFieldSerializedProperties(SerializedObject serializedObject, Type[] argumentTypes)
		{
			int count = argumentTypes.Length;
			var results = new SerializedProperty[count];
			string[] usualPropertyNames;
			if(!propertyNamesByArgumentCount.TryGetValue(count, out usualPropertyNames))
			{
				#if DEV_MODE
				if(count != 0)
				{
					Debug.LogWarning($"propertyNamesByArgumentCount[{count}] value missing");
				}
				#endif

				usualPropertyNames = Array.Empty<string>();
			}

			for(int i = 0; i < count; i++)
			{
				var serializedProperty = serializedObject.FindProperty(usualPropertyNames[i]);
				if(serializedProperty is not null)
				{
					results[i] = serializedProperty;
					continue;
				}

				var argumentAnyType = typeof(Any<>).MakeGenericType(argumentTypes[i]);
				serializedProperty = serializedObject.GetIterator();
				serializedProperty.NextVisible(true);
				while(serializedProperty.NextVisible(false))
				{
					if(serializedProperty.GetMemberInfo() is FieldInfo field && field.FieldType == argumentAnyType)
					{
						results[i] = serializedProperty;
						break;
					}
				}
			}

			return results;
		}

		internal static Type GetMetaDataClassType(Type initializerType) => initializerType.GetNestedType(InitializerEditor.InitArgumentMetadataClassName, BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.Static);

		private static FieldInfo[] GetFieldInfosFromMetadata([DisallowNull] Type metadataClass)
		{ 
			var members = metadataClass.GetFields(BindingFlags.NonPublic | BindingFlags.Public | BindingFlags.Instance | BindingFlags.Static | BindingFlags.DeclaredOnly);
			Array.Sort(members, CompareOrderOfDefinition);
			return members;
			static int CompareOrderOfDefinition(MemberInfo x, MemberInfo y) => x.MetadataToken.CompareTo(y.MetadataToken);
		}

		private static InitParameterGUI[] GetPropertyDrawerData(SerializedObject serializedObject, Type clientType, Type[] argumentTypes, SerializedProperty[] serializedProperties)
		{
			var initializer = serializedObject.targetObject;
			var initializerType = initializer.GetType();
			var metadataClass = GetMetaDataClassType(initializerType);

			int argumentCount = argumentTypes.Length;
			var results = new InitParameterGUI[argumentCount];
			var fields = metadataClass is null ? Array.Empty<FieldInfo>() : GetFieldInfosFromMetadata(metadataClass);

			// If the Init class has one member per init argument + constructor, then we can try extracting
			// attributes and labels from the non-constructor members defined in it and use them when visualizing
			// the Init arguments in the Inspector.
			if(fields.Length == argumentCount)
			{
				for(int i = 0; i < argumentCount; i++)
				{
					var field = fields[i];
					if(field.FieldType == argumentTypes[i])
					{
						continue;
					}

					var newIndex = Array.IndexOf(argumentTypes, field.FieldType);
					if(newIndex != -1)
					{
						(fields[i], fields[newIndex]) = (fields[newIndex], fields[i]);
					}
				}
				
				for(int i = 0, count = serializedProperties.Length; i < count; i++)
				{
					var serializedProperty = serializedProperties[i];
					if(serializedProperty is null)
					{
						#if DEV_MODE
						Debug.LogWarning($"{clientType.Name}.serializedProperties[{i}] was null.");
						#endif

						var newResults = new InitParameterGUI[results.Length - 1];
						if(i < count - 1)
						{
							Array.Copy(results, i + 1, newResults, i, newResults.Length - i);
						}

						results = newResults;
						continue;
					}

					var argumentType = argumentTypes[i];
					var field = fields[i];
					var attributes = field.GetCustomAttributes().ToArray();
					var label = new GUIContent
					(
						GetLabel(field.Name),
						TryGetTooltip(attributes, out var tooltip) ? tooltip : serializedProperty.tooltip
					);

					results[i] = new
					(
						label,
						serializedProperty,
						argumentType,
						attributes
					);
				}
			}
			else if(TryGetInitParameters(clientType, argumentTypes, out ParameterInfo[] parameters))
			{
				for(int i = 0, count = serializedProperties.Length; i < count; i++)
				{
					var serializedProperty = serializedProperties[i];
					if(serializedProperty is null)
					{
						#if DEV_MODE
						Debug.LogWarning($"{clientType.Name}.serializedProperties[{i}] was null.");
						#endif

						var newResults = new InitParameterGUI[results.Length - 1];
						if(i < argumentCount - 1)
						{
							Array.Copy(results, i + 1, newResults, i, newResults.Length - i);
						}

						results = newResults;
						continue;
					}

					var parameter = parameters[i];
					var label = new GUIContent(GetLabel(parameter.Name));

					results[i] = new
					(
						label,
						serializedProperty,
						argumentTypes[i],
						Array.Empty<Attribute>()
					);
				}
			}
			else
			{
				for(int i = 0, count = serializedProperties.Length; i < count; i++)
				{
					var serializedProperty = serializedProperties[i];
					if(serializedProperty is null)
					{
						#if DEV_MODE
						Debug.LogWarning($"{clientType.Name}.serializedProperties[{i}] was null.");
						#endif

						var newResults = new InitParameterGUI[results.Length - 1];
						if(i < count - 1)
						{
							Array.Copy(results, i + 1, newResults, i, newResults.Length - i);
						}

						results = newResults;
						continue;
					}

					var argumentType = argumentTypes[i];
					var label = GetArgumentLabel(clientType, argumentType, i);

					results[i] = new
					(
						label,
						serializedProperty,
						argumentType
					);
				}
			}

			return results;
		}

		internal static bool TryGetInitParameters([DisallowNull] Type clientType, Type[] parameterTypes, [MaybeNullWhen(false), NotNullWhen(true)] out ParameterInfo[] parameters)
		{
			for(var type = clientType; !TypeUtility.IsNullOrBaseType(type); type = type.BaseType)
			{
				foreach(var initMethod in type.GetMethods(AnyDeclaredInstance).Where(x => string.Equals(x.Name, "Init")))
				{
					parameters = initMethod.GetParameters();
					if(ParametersMatch(parameters, parameterTypes))
					{
						return true;
					}
				}
			}

			parameters = null;
			return false;
		}

		private static bool ParametersMatch(ParameterInfo[] parameters, Type[] parameterTypes)
		{
			var parametersLength = parameters.Length;
			if(parametersLength != parameterTypes.Length)
			{
				return false;
			}

			for(int i = parametersLength - 1; i >= 0; i--)
			{
				if(parameters[i].ParameterType != parameterTypes[i])
				{
					return false;
				}
			}

			return true;
		}

		internal static bool TryGetAttributeBasedPropertyDrawer([DisallowNull] SerializedProperty serializedProperty, [DisallowNull] Attribute[] attributes, out PropertyDrawer propertyDrawer)
		{
			foreach(var attribute in attributes)
			{
				if(attribute is PropertyAttribute propertyAttribute
					&& TryGetAttributeBasedPropertyDrawer(serializedProperty, propertyAttribute, out propertyDrawer))
				{
					return true;
				}
			}

			propertyDrawer = null;
			return false;
		}

		private static bool TryGetAttributeBasedPropertyDrawer([DisallowNull] SerializedProperty serializedProperty, [DisallowNull] PropertyAttribute propertyAttribute, out PropertyDrawer propertyDrawer)
		{
			if(!TryGetDrawerType(propertyAttribute, out Type drawerType))
			{
				propertyDrawer = null;
				return false;
			}

			propertyDrawer = CreateInstance(drawerType) as PropertyDrawer;
			if(propertyDrawer == null)
			{
				return false;
			}

			PropertyDrawerUtility.SetAttribute(propertyDrawer, propertyAttribute);
			if(serializedProperty.GetMemberInfo() is FieldInfo fieldInfo)
			{
				PropertyDrawerUtility.SetFieldInfo(propertyDrawer, fieldInfo);
			}

			return true;
		}

		private static bool TryGetDrawerType([DisallowNull] PropertyAttribute propertyAttribute, out Type drawerType)
		{
			var propertyAttributeType = propertyAttribute.GetType();
			drawerType = null;

			foreach(var propertyDrawerType in TypeCache.GetTypesWithAttribute<CustomPropertyDrawer>())
			{
				foreach(var attribute in propertyDrawerType.GetCustomAttributes<CustomPropertyDrawer>())
				{
					var targetType = PropertyDrawerUtility.GetTargetType(attribute);
					if(targetType == propertyAttributeType)
					{
						drawerType = propertyDrawerType;
						return true;
					}

					if(targetType.IsAssignableFrom(propertyAttributeType) && PropertyDrawerUtility.GetUseForChildren(attribute))
					{
						drawerType = propertyDrawerType;
					}
				}
			}

			return drawerType != null;
		}

		internal static object CreateInstance(Type type)
		{
			#if DEV_MODE
			Debug.Assert(!type.IsAbstract, type.FullName);
			Debug.Assert(!type.IsGenericTypeDefinition, type.FullName);
			#endif

			try
			{
				return Activator.CreateInstance(type);
			}
			catch
			{
				try
				{
					return FormatterServices.GetUninitializedObject(type);
				}
				catch(Exception e)
				{
					Debug.LogWarning($"CreateInstance({TypeUtility.ToString(type)}: {e}");
					return null;
				}
			}
		}

		internal static GUIContent GetLabel(SerializedProperty anyProperty, Type argumentType, FieldInfo fieldInfo)
		{
			var initializerType = anyProperty.serializedObject.targetObject.GetType();
			if(!typeof(IInitializer).IsAssignableFrom(initializerType))
			{
				return new(anyProperty.displayName, anyProperty.tooltip);
			}

			if(GetMetaDataClassType(initializerType) is { } metadataClass)
			{
				var memberInfos = GetFieldInfosFromMetadata(metadataClass);
				if(propertyNameToInitParameterIndex.TryGetValue(fieldInfo.Name, out int parameterIndex) && parameterIndex < memberInfos.Length)
				{
					var memberInfo = memberInfos[parameterIndex];
					return new(GetLabel(memberInfo.Name), TryGetTooltip(memberInfo, out string tooltip) ? tooltip : anyProperty.tooltip);
				}

				#if DEV_MODE
				Debug.Log($"\"{fieldInfo.Name}\" propertyNameToInitParameterIndex result {parameterIndex} not valid index between 0...{memberInfos.Length} or not found in {nameof(propertyNameToInitParameterIndex)}.");
				#endif
			}

			return new(GetLabel(argumentType), anyProperty.tooltip);
		}

		private static GUIContent GetArgumentLabel(Type clientType, Type parameterType, int parameterIndex)
		{
			if(TryGetArgumentTargetMember(clientType, parameterType, null, parameterIndex, out var member))
			{
				var label = GetLabel(member);
				if(member.GetCustomAttribute<TooltipAttribute>() is { } tooltip)
				{
					label.tooltip = tooltip.tooltip;
				}

				return label;
			}

			return new(GetLabel(parameterType));
		}

		private static GUIContent GetLabel([DisallowNull] MemberInfo member) => new(GetLabel(member.Name), GetTooltip(member));

		private static string GetTooltip(MemberInfo member) => member.GetCustomAttribute<TooltipAttribute>() is TooltipAttribute tooltip ? tooltip.tooltip : "";

		private static bool TryGetTooltip(MemberInfo member, out string tooltip)
		{
			if(member.GetCustomAttribute<TooltipAttribute>() is TooltipAttribute tooltipAttribute)
			{
				tooltip = tooltipAttribute.tooltip;
				return true;
			}

			tooltip = null;
			return false;
		}

		private static bool TryGetTooltip(Attribute[] attributes, out string tooltip)
		{
			foreach(var attribute in attributes)
			{
				if(attribute is TooltipAttribute tooltipAttribute)
				{
					tooltip = tooltipAttribute.tooltip;
					return true;
				}
			}

			tooltip = null;
			return false;
		}

		private static bool TryGetArgumentTargetFieldName(Type clientType, Type parameterType, [MaybeNull] ParameterInfo parameter, int argumentIndex, out string targetFieldName)
		{
			if(TryGetArgumentTargetMember(clientType, parameterType, parameter, argumentIndex, out var member))
			{
				targetFieldName = member.Name;
				return true;
			}

			targetFieldName = null;
			return false;
		}
		
		/// <summary>
		/// Tries to get the member field or property into which an Init function or constructor parameter
		/// of the given <paramref name="parameterType">type</paramref> and at the given <paramref name="parameterIndex"/>
		/// will likely be assigned to during initialization.
		/// <para>
		/// This information could be useful for purposes such as retrieving the name of the member for displaying in the Inspector,
		/// or retrieving attributes from the field to draw it using custom property drawers in the Inspector.
		/// </para>
		/// </summary>
		/// <param name="clientType"> Type of the class that contains the Init function and the target member. </param>
		/// <param name="parameterType"> Type of the Init argument; the target member must be assignable from this type. </param>
		/// <param name="parameter"> Init method parameter, if available; otherwise, null. </param>
		/// <param name="parameterIndex"> Zero-based index of the argument among the Init function's arguments. </param>
		/// <param name="result"> The member of <paramref name="clientType"/> into which the Init argument is probably going to be assigned to. </param>
		/// <returns> <see langword="true"/> if a match was found; otherwise, <see langword="false"/>. </returns>
		internal static bool TryGetArgumentTargetMember([DisallowNull] Type clientType, [DisallowNull] Type parameterType, [MaybeNull] ParameterInfo parameter, int parameterIndex, [MaybeNullWhen(false), NotNullWhen(true)] out MemberInfo result)
		{
			// Prio 1: assignable field with name matching parameter name (including property backing fields)
			// Prio 2: assignable field with field index matching parameter index (including property backing fields)
			// Prio 3: first field of exact same type (including property backing fields)
			// Prio 4: assignable property with name matching parameter name
			// Prio 5: assignable property with field index matching parameter index
			// Prio 6: first property of exact same type
			// Prio 7: single assignable field
			// Prio 8: single assignable property
			var nicifiedParameterName = parameter is not null ? ObjectNames.NicifyVariableName(parameter.Name) : null;
			
			var assignableFields = new List<FieldInfo>();
			result = null;
			var nthMember = -1;
			for(var type = clientType; !TypeUtility.IsNullOrBaseType(type); type = type.BaseType)
			{
				var fields = clientType.GetFields(AnyDeclaredInstance);
				for(int i = 0; i < fields.Length; i++)
				{
					nthMember++;
					FieldInfo field = fields[i];
					if(!InjectionUtility.IsAssignableFrom(field.FieldType, parameterType))
					{
						continue;
					}

					// Prio 1: assignable field with name matching parameter name
					if(nicifiedParameterName != null && string.Equals(ObjectNames.NicifyVariableName(field.Name), nicifiedParameterName))
					{
						result = field;
						return true;
					}

					assignableFields.Add(field);
					if(nthMember == parameterIndex)
					{
						result = field;
					}
				}
			}
			
			// Prio 2: assignable field at member index
			if(result is not null)
			{
				return true;
			}

			// Prio 3: first field of exact same type
			result = assignableFields.FirstOrDefault(x => x.FieldType == parameterType);
			if(result is not null)
			{
				return true;
			}

			var nthProperty = -1;
			var assignableProperties = new List<PropertyInfo>();
			for(var type = clientType; !TypeUtility.IsNullOrBaseType(type); type = type.BaseType)
			{
				nthMember++;
				nthProperty++;
				var properties = clientType.GetProperties(AnyDeclaredInstance);
				for(int propertyIndex = 0; propertyIndex < properties.Length; propertyIndex++)
				{
					PropertyInfo property = properties[propertyIndex];
					if(!InjectionUtility.IsAssignableFrom(property.PropertyType, parameterType) || property.GetIndexParameters().Length > 0)
					{
						continue;
					}

					// Prio 4: assignable property with name matching parameter name
					if(nicifiedParameterName != null && string.Equals(ObjectNames.NicifyVariableName(property.Name), nicifiedParameterName))
					{
						result = property;
						return true;
					}

					if(nthMember == parameterIndex)
					{
						result = property;
					}
					else if(nthProperty == parameterIndex)
					{
						result ??= property;
					}

					assignableProperties.Add(property);
				}
			}

			// Prio 5: assignable property with field index matching parameter index
			if(result is not null)
			{
				return true;
			}
			
			// Prio 6: first property of exact same type
			result = assignableProperties.FirstOrDefault(x => x.PropertyType == parameterType);
			if(result is not null)
			{
				return true;
			}
			
			// Prio 7: single assignable field
			if(assignableFields.Count is 1)
			{
				result = assignableFields[0];
				return true;
			}
			
			// Prio 8: single assignable property
			if(assignableProperties.Count is 1)
			{
				result = assignableProperties[0];
				return true;
			}

			return false;
		}

		internal static bool IsInitializable(object client) => client is IOneArgument or ITwoArguments or IThreeArguments or IFourArguments or IFiveArguments or ISixArguments or ISevenArguments or IEightArguments or INineArguments or ITenArguments or IElevenArguments or ITwelveArguments;

		internal static bool IsInitializable(Type clientType) => GetClientInitArgumentCount(clientType) > 0;

		internal static int GetClientInitArgumentCount(Type clientType)
		{
			if(typeof(IOneArgument).IsAssignableFrom(clientType))
			{
				return 1;
			}

			if(typeof(ITwoArguments).IsAssignableFrom(clientType))
			{
				return 2;
			}

			if(typeof(IThreeArguments).IsAssignableFrom(clientType))
			{
				return 3;
			}

			if(typeof(IFourArguments).IsAssignableFrom(clientType))
			{
				return 4;
			}

			if(typeof(IFiveArguments).IsAssignableFrom(clientType))
			{
				return 5;
			}

			if(typeof(ISixArguments).IsAssignableFrom(clientType))
			{
				return 6;
			}

			if(typeof(ISevenArguments).IsAssignableFrom(clientType))
			{
				return 7;
			}

			if(typeof(IEightArguments).IsAssignableFrom(clientType))
			{
				return 8;
			}

			if(typeof(INineArguments).IsAssignableFrom(clientType))
			{
				return 9;
			}

			if(typeof(ITenArguments).IsAssignableFrom(clientType))
			{
				return 10;
			}

			if(typeof(IElevenArguments).IsAssignableFrom(clientType))
			{
				return 11;
			}

			if(typeof(ITwelveArguments).IsAssignableFrom(clientType))
			{
				return 12;
			}

			return 0;
		}

		internal static bool TryGetTintForNullGuardResult(NullGuardResult nullGuardResult, out Color color) => TryGetTintForNullGuardResult(nullGuardResult.Type, out color);

		internal static bool TryGetTintForNullGuardResult(NullGuardResultType resultType, out Color color)
		{
			if(resultType is NullGuardResultType.Passed)
			{
				color = Color.white;
				return false;
			}

			if(resultType is NullGuardResultType.Warning)
			{
				color = nullGuardWarningColor;
				return true;
			}

			color = nullGuardFailedColor;
			return true;
		}

		private static readonly Dictionary<Type, Dictionary<Type, bool>> isAssignableCaches = new();

		internal static bool TryGetAssignableType(Object reference, Object owner, Type anyType, Type valueType, out Type assignableType)
		{
			#if DEV_MODE && DEBUG && !INIT_ARGS_DISABLE_PROFILING
			using var x = tryGetAssignableTypeMarker.Auto();
			#endif

			if(!reference)
			{
				assignableType = null;
				return false;
			}

			if(valueType == typeof(Object) || valueType == typeof(object))
			{
				assignableType = reference.GetType();
				return true;
			}

			if(reference is GameObject gameObject && valueType != typeof(GameObject))
			{
				Type bestMatch = null;
				foreach(var component in gameObject.GetComponentsNonAlloc<Component>())
				{
					if(TryGetAssignableType(component, owner, anyType, valueType, out assignableType))
					{
						if(assignableType == valueType)
						{
							return true;
						}

						bestMatch = assignableType;
					}
				}

				if(bestMatch is null)
				{
					assignableType = null;
					return false;
				}

				assignableType = bestMatch;
				return true;
			}

			if(!isAssignableCaches.TryGetValue(anyType, out var isAssignableCache))
			{
				isAssignableCache = new();
				isAssignableCaches.Add(anyType, isAssignableCache);
			}

			var draggedType = reference.GetType();
			if(isAssignableCache.TryGetValue(draggedType, out bool isAssignable))
			{
				assignableType = isAssignable ? reference.GetType() : null;
				return isAssignable;
			}

			var isCreatableFromMethod = anyType.GetMethod(nameof(Any<object>.IsCreatableFrom), BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic);
			isAssignable = (bool)isCreatableFromMethod.Invoke(null, new object[] { reference });
			isAssignableCache.Add(draggedType, isAssignable);
			assignableType = isAssignable ? reference.GetType() : null;
			return isAssignable;
		}

		#if DEV_MODE && DEBUG && !INIT_ARGS_DISABLE_PROFILING
		private static readonly ProfilerMarker tryGetAssignableTypeMarker = new(ProfilerCategory.Gui, "InitializerEditorUtility.TryGetAssignableType");
		#endif
	}
}