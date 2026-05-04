#define DEBUG_ENABLED

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.Linq;
using Sisus.Init.EditorOnly.Internal;
using Sisus.Init.Internal;
using Sisus.Shared.EditorOnly;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEditor.Search;
using UnityEngine;
using UnityEngine.SceneManagement;
using Object = UnityEngine.Object;
using Debug = UnityEngine.Debug;

namespace Sisus.Init.EditorOnly
{
	/// <summary>
	/// Responsible for drawing GUI for <see cref="CrossSceneReference"/>. 
	/// </summary>
	internal sealed class CrossSceneReferenceGUI : IDisposable
	{
		private readonly Dictionary<CrossSceneReference, CachedState> states = new ();

		private static GUIContent sceneIcon;
		private static GUIContent prefabIcon;

		private readonly Type valueType;
		private readonly Type anyType;
		private SerializedObject serializedObject;
		private SerializedProperty targetProperty;
		private SerializedProperty targetNameProperty;
		private SerializedProperty sceneOrPrefabNameProperty;
		private SerializedProperty sceneOrAssetGuidProperty;
		private SerializedProperty isCrossSceneProperty;
		private SerializedProperty iconProperty;

		public CrossSceneReferenceGUI(Type valueType)
		{
			this.valueType = valueType;
			anyType = typeof(Any<>).MakeGenericType(valueType);
		}

		/// <summary>
		/// Draw GUI for a <see cref="CrossSceneReference"/>.
		/// </summary>
		public void OnGUI(Rect position, SerializedProperty referenceProperty, GUIContent label)
		{
			var guiColorWas = GUI.color;
			UpdateSerializedObjects(referenceProperty, ref serializedObject);

			var crossSceneReference = GetCrossSceneReferenceObject(referenceProperty) as CrossSceneReference;
			if(!crossSceneReference)
			{
				EditorGUI.PropertyField(position, referenceProperty, label);
				return;
			}

			if(!states.TryGetValue(crossSceneReference, out var state))
			{
				state = new(crossSceneReference);
				states.Add(crossSceneReference, state);
			}

			targetProperty = serializedObject.FindProperty(nameof(CrossSceneReference.target));
			targetNameProperty = serializedObject.FindProperty(nameof(CrossSceneReference.targetName));
			sceneOrPrefabNameProperty = serializedObject.FindProperty(nameof(CrossSceneReference.sceneOrAssetName));
			sceneOrAssetGuidProperty = serializedObject.FindProperty(nameof(CrossSceneReference.sceneOrAssetGuid));
			isCrossSceneProperty = serializedObject.FindProperty(nameof(CrossSceneReference.referenceType));
			iconProperty = serializedObject.FindProperty(nameof(CrossSceneReference.icon));

			var referenceValue = state.GetTarget(referenceProperty);
			var visualizedReferenceValue = state.CurrentObjectPicker ? state.ObjectPickerCurrentValue : referenceValue;
			var asset = state.ContainingAsset;
			var sceneOrAssetName = sceneOrPrefabNameProperty.stringValue;
			var sceneOrAssetGuid = sceneOrAssetGuidProperty.stringValue;
			var sceneOrAssetPath = state.SceneOrAssetPath;
			
			var referenceType = (CrossSceneReferenceType)isCrossSceneProperty.intValue;
			var isCrossSceneOrPrefabReference = referenceType is not CrossSceneReferenceType.None;
			var targetIcon = iconProperty.objectReferenceValue as Texture;
			var isSceneReference = referenceType is CrossSceneReferenceType.Scene;

			EditorGUI.BeginProperty(position, label, targetProperty);

			const float iconOffset = 2f;
			float iconWidth = EditorGUIUtility.singleLineHeight;
			Rect assetIconRect = position;
			
			bool isSceneOrPrefabOpen = IsSceneOrPrefabOpen();
			var nullGuardResult = ((INullGuard)crossSceneReference).EvaluateNullGuard(client: null);
			var canProvideValue =  nullGuardResult.Type is NullGuardResultType.Passed;
			if(isCrossSceneOrPrefabReference)
			{
				position = EditorGUI.PrefixLabel(position, label);

				float remainingWidth = position.width;
				assetIconRect = position;
				var tooltip = state.Tooltip;
				assetIconRect.width = iconWidth;
				remainingWidth -= iconWidth + iconOffset;
				GUIContent assetIcon;
				if(isSceneReference)
				{
					sceneIcon ??= new(EditorGUIUtility.IconContent("SceneAsset Icon"));
					sceneIcon.tooltip = tooltip;
					assetIcon = sceneIcon;
				}
				else
				{
					prefabIcon ??= new(EditorGUIUtility.IconContent("Prefab Icon"));
					prefabIcon.tooltip = tooltip;
					assetIcon = prefabIcon;
				}

				GUI.color = Color.white;
				GUI.Label(assetIconRect, assetIcon);
				GUI.color = guiColorWas;

				if(Event.current.type == EventType.MouseDown && assetIconRect.Contains(Event.current.mousePosition))
				{
					OnIconClicked(asset, crossSceneReference.GetTarget(Context.EditMode), isSceneOrPrefabOpen, crossSceneReference.CrossSceneId, crossSceneReference, referenceProperty, true, label);
					Event.current.Use();
				}

				position.x += iconWidth + iconOffset;
				position.width = remainingWidth;
				label = GUIContent.none;
			}

			var preventCrossSceneReferencesWas = EditorSceneManager.preventCrossSceneReferences;
			EditorSceneManager.preventCrossSceneReferences = false;
			var isContainingSceneMissing = !string.IsNullOrEmpty(sceneOrAssetGuid) && string.IsNullOrEmpty(AssetDatabase.GUIDToAssetPath(sceneOrAssetGuid));
			var referenceValueWas = referenceValue;
			bool draggedObjectIsAssignable;
			Type objectFieldConstraint;
			bool dragging = DragAndDrop.objectReferences.Length > 0;
			if(dragging && InitializerEditorUtility.TryGetAssignableType(DragAndDrop.objectReferences[0], referenceProperty.serializedObject.targetObject, anyType, valueType, out Type assignableType))
			{
				draggedObjectIsAssignable = true;
				objectFieldConstraint = assignableType;
			}
			else
			{
				draggedObjectIsAssignable = false;
				var objectReferenceValue = referenceProperty.objectReferenceValue;
				bool hasObjectReferenceValue = objectReferenceValue;
				var objectReferenceValueType = hasObjectReferenceValue ? objectReferenceValue.GetType() : null;

				if(typeof(Object).IsAssignableFrom(valueType) || valueType.IsInterface)
				{
					objectFieldConstraint = !hasObjectReferenceValue || valueType.IsAssignableFrom(objectReferenceValueType) ? valueType : typeof(Object);
				}
				else
				{
					var valueProviderType = typeof(IValueProvider<>).MakeGenericType(valueType);
					objectFieldConstraint = !hasObjectReferenceValue || valueProviderType.IsAssignableFrom(objectReferenceValueType) ? valueProviderType : typeof(Object);
				}
			}

			if(!canProvideValue)
			{
				InitializerEditorUtility.TryGetTintForNullGuardResult(nullGuardResult, out var tint);
				GUI.color = tint;
			}

			var targetName = targetNameProperty.stringValue;
			if(referenceValue || draggedObjectIsAssignable || !isCrossSceneOrPrefabReference)
			{
				OverrideObjectPicker(position, referenceProperty, valueType, state);
				var setReferenceValue = EditorGUI.ObjectField(position, label, visualizedReferenceValue, objectFieldConstraint, true);
				if(visualizedReferenceValue == referenceValue)
				{
					referenceValue = setReferenceValue;
					visualizedReferenceValue = setReferenceValue;
				}

				GUI.color = guiColorWas;
				EditorSceneManager.preventCrossSceneReferences = preventCrossSceneReferencesWas;
			}
			else
			{
				Rect overlayRect = position;
				overlayRect.width -= 19f;

				Rect clickToPingRect = position;
				clickToPingRect.width -= EditorGUIUtility.singleLineHeight;
				if(Event.current.type == EventType.MouseDown && clickToPingRect.Contains(Event.current.mousePosition))
				{
					OnIconClicked(asset, crossSceneReference.GetTarget(Context.EditMode), isSceneOrPrefabOpen, crossSceneReference.CrossSceneId, crossSceneReference, referenceProperty, false, label);
					Event.current.Use();
				}

				OverrideObjectPicker(position, referenceProperty, valueType, state);
				var setReferenceValue = EditorGUI.ObjectField(position, label, referenceValue, valueType, true);
				if(visualizedReferenceValue == referenceValue)
				{
					referenceValue = setReferenceValue;
					visualizedReferenceValue = setReferenceValue;
				}

				GUI.color = guiColorWas;
				EditorSceneManager.preventCrossSceneReferences = preventCrossSceneReferencesWas;

				var targetLabel = new GUIContent(targetName);
				if(targetLabel.text.Length == 0)
				{
					targetLabel.text = " ";
				}

				targetLabel.tooltip = isContainingSceneMissing ? "Containing scene not found." : !isSceneOrPrefabOpen ? "Containing scene not loaded." : "Missing Target";
				var clipRect = overlayRect;
				clipRect.x += 2f;
				clipRect.y += 2f;
				clipRect.width -= 4f;
				clipRect.height -= 3f;
				GUI.BeginClip(clipRect);
				overlayRect.x = -2f;
				overlayRect.y = -2f;
				if(isCrossSceneOrPrefabReference && !referenceValue && targetIcon)
				{
					EditorGUI.LabelField(overlayRect, GUIContent.none, GUIContent.none, EditorStyles.objectField);
					float roomForIcon = 14f + 1f;
					clipRect.x += roomForIcon;
					clipRect.width -= roomForIcon;
					overlayRect.width -= roomForIcon;
					GUI.EndClip();
					GUI.BeginClip(clipRect);
					overlayRect.x = -3f;
				}

				EditorGUI.LabelField(overlayRect, label, targetLabel, EditorStyles.objectField);
				GUI.EndClip();
			}

			if(isCrossSceneOrPrefabReference && !referenceValue && targetIcon)
			{
				assetIconRect.x += iconWidth + iconOffset;
				assetIconRect.x += 2f;
				assetIconRect.y += 3f;
				assetIconRect.width = 14f;
				assetIconRect.height = 14f;
				
				var iconSizeWas = EditorGUIUtility.GetIconSize();
				EditorGUIUtility.SetIconSize(new(12f, 12f));

				if(GUI.Button(assetIconRect, targetIcon, EditorStyles.label))
				{
					OnIconClicked(asset, crossSceneReference.GetTarget(Context.EditMode), isSceneOrPrefabOpen, crossSceneReference.CrossSceneId, crossSceneReference, referenceProperty, true, label);
				}
				
				EditorGUIUtility.SetIconSize(iconSizeWas);
			}

			var valueChanged = !ReferenceEquals(referenceValueWas, referenceValue);
			if(valueChanged)
			{
				// Clear the previous value
				referenceProperty.objectReferenceValue = null;

				try
				{
					if(!referenceValue || !InitializerEditorUtility.TryGetAssignableType(referenceValue, serializedObject.targetObject, anyType, valueType, out _))
					{
						#if DEV_MODE && DEBUG_ENABLED
						Debug.Log($"CrossSceneReferenceGUI: Dragged Object value {TypeUtility.ToString(referenceValue?.GetType())} is null or not assignable to {TypeUtility.ToString(valueType)}.");
						#endif
						GUIUtility.ExitGUI();
					}
					else if(CrossSceneReference.Detect(serializedObject.targetObject, referenceValue))
					{
						#if DEV_MODE && DEBUG_ENABLED
						Debug.Log($"Cross scene reference assigned: {referenceValue.name} ({referenceValue.GetType().Name}) scene: {GetScene(referenceValue)}");
						#endif
						Assign(referenceProperty, referenceValue);
						GUIUtility.ExitGUI();
					}
					else if(IsPartOfDifferentPrefabAsset(serializedObject.targetObject, referenceValue))
					{
						#if DEV_MODE && DEBUG_ENABLED
						Debug.Log($"Cross-scene reference changed to {referenceValue} which is a component in another prefab.");
						#endif
						ShowPickPrefabReferenceTypeDropdown(referenceProperty, referenceValue, position);
						GUIUtility.ExitGUI();
					}
					else
					{
						#if DEV_MODE && DEBUG_ENABLED
						Debug.Log($"Cross-scene reference changed to normal reference to  {TypeUtility.ToString(referenceValue.GetType())}.");
						#endif
						referenceProperty.objectReferenceValue = referenceValue;
						referenceProperty.serializedObject.ApplyModifiedProperties();
						GUIUtility.ExitGUI();
					}
				}
				finally
				{
					if(!AssetDatabase.Contains(crossSceneReference))
					{
						Undo.DestroyObjectImmediate(crossSceneReference);
					}
					else if(AssetDatabase.IsSubAsset(crossSceneReference))
					{
						var target = serializedObject.targetObject;
						// If sub-asset of a ScriptableObject, we can delete without asking, because ScriptableObjects can't have base assets or variants.
						if(target is ScriptableObject)
						{
							ValueProviderEditorUtility.DeleteSubAsset(crossSceneReference, saveToDisk: true);
						}
						// If sub-asset of a prefab, ask before deleting, because cross-scene reference could still be used by a base prefab or prefab variant.
						else
						{
							ValueProviderEditorUtility.AskToDeletePreviousSubAssetValue(crossSceneReference, saveToDisk: true);
						}
					}
				}
			}

			EditorGUI.EndProperty();

			bool IsSceneOrPrefabOpen()
			{
				if(!isCrossSceneOrPrefabReference)
				{
					return true;
				}

				if(!string.IsNullOrEmpty(sceneOrAssetName))
				{
					for(int i = 0, count = SceneManager.sceneCount; i < count; i++)
					{
						var scene = SceneManager.GetSceneAt(i);
						if(scene.isLoaded && string.Equals(scene.path, sceneOrAssetPath))
						{
							return true;
						}
					}
				}
				else
				{
					var prefabStage = PrefabStageUtility.GetCurrentPrefabStage();
					return prefabStage && string.Equals(prefabStage.assetPath, sceneOrAssetPath);
				}

				return false;
			}
		}

		private static object GetCrossSceneReferenceObject(SerializedProperty crossSceneReferenceProperty) => crossSceneReferenceProperty.GetValue();

		private static void UpdateSerializedObjects(SerializedProperty crossSceneReferenceProperty, ref SerializedObject serializedObject)
		{
			crossSceneReferenceProperty.serializedObject.Update();

			if(crossSceneReferenceProperty.propertyType is not SerializedPropertyType.ObjectReference)
			{
				serializedObject = crossSceneReferenceProperty.serializedObject;
				return;
			}

			var crossSceneReferenceScriptableObject = crossSceneReferenceProperty.objectReferenceValue;
			if(!crossSceneReferenceScriptableObject)
			{
				serializedObject = null;
				return;
			}

			if(serializedObject == null || serializedObject.targetObject != crossSceneReferenceScriptableObject)
			{
				serializedObject = new(crossSceneReferenceScriptableObject);
			}
			else
			{
				serializedObject.Update();
			}
		}

		[Conditional("UNITY_2023_2_OR_NEWER")]
		public static void OverrideObjectPicker(Rect objectFieldRect, SerializedProperty referenceProperty, Type objectType, IObjectPickerPreviewable state)
		{
			#if UNITY_2023_2_OR_NEWER
			var objectPickerRect = objectFieldRect;
			objectPickerRect.width = 19f;
			objectPickerRect.x += objectFieldRect.width - objectPickerRect.width;
			if(GUI.Button(objectPickerRect, GUIContent.none, EditorStyles.label))
			{
				ShowObjectPicker(type:objectType, referenceProperty:referenceProperty, state:state);
			}
			#endif
		}

		#if UNITY_2023_2_OR_NEWER
		private static void ShowObjectPicker
		(
			Type type,
			SerializedProperty referenceProperty,
			IObjectPickerPreviewable state,
			SearchFlags flags = SearchFlags.None,
			string searchText = "",
			float defaultWidth = 850f,
			float defaultHeight = 539f
		)
		{
			#if DEV_MODE
			Debug.Log($"Showing object picker for {TypeUtility.ToString(type)}...");
			#endif

			var filterTypes = Find.typesToFindableTypes.TryGetValue(type, out var types) ? types : new []{ typeof(Object) };
			var providers = new List<SearchProvider>();
			if(filterTypes.Any(t => typeof(Component).IsAssignableFrom(t) || t == typeof(GameObject)))
			{
				providers.Add(SearchService.GetProvider("scene"));
			}

			if(filterTypes.Any(t => !typeof(Component).IsAssignableFrom(t) && t != typeof(GameObject)))
			{
				providers.Add(SearchService.GetProvider("asset"));
			}

			var targetObjects = referenceProperty.serializedObject.targetObjects;
			var propertyPath = referenceProperty.propertyPath;
			var originalValue = referenceProperty.objectReferenceValue;
			var context = SearchService.CreateContext(providers, searchText, flags | SearchFlags.FocusContext);
			var typeName = TypeUtility.ToStringNicified(type);
			var pickerState = SearchViewState.CreatePickerState
			(
				title:"Select " + typeName,
				context:context,
				selectObjectHandler:OnSelectionConfirmed,
				trackingObjectHandler:OnSelectionChanged,
				typeName:typeName,
				filterType:GetSharedBaseType(filterTypes)
			);

			pickerState.position = new(0f, 0f, defaultWidth, defaultHeight);

			state.CurrentObjectPicker = SearchService.ShowPicker(pickerState) as EditorWindow;

			void OnSelectionChanged(Object reference)
			{
				Convert(ref reference, type);
				state.ObjectPickerCurrentValue = reference;
			}

			void OnSelectionConfirmed(Object reference, bool wasCanceled)
			{
				state.ObjectPickerCurrentValue = null;
				if(wasCanceled)
				{
					return;
				}

				if(!referenceProperty.serializedObject.IsValid())
				{
					var serializedObject = new SerializedObject(targetObjects);
					referenceProperty = serializedObject.FindProperty(propertyPath);
				}

				Convert(ref reference, type);
				if(reference == originalValue)
				{
					return;
				}

				#if DEV_MODE
				Debug.Log($"Value changed from {originalValue} to {reference}. canceled:{wasCanceled}");
				#endif

				if(!reference)
				{
					referenceProperty.objectReferenceValue = null;
					referenceProperty.serializedObject.ApplyModifiedProperties();

					if(originalValue is ScriptableObject oldScriptableObject && oldScriptableObject)
					{
						if(!AssetDatabase.Contains(oldScriptableObject))
						{
							Undo.DestroyObjectImmediate(oldScriptableObject);
						}
						else if(AssetDatabase.IsSubAsset(oldScriptableObject))
						{
							// If sub-asset of a ScriptableObject, we can delete without asking, because ScriptableObjects can't have base assets or variants.
							if(targetObjects.FirstOrDefault() is ScriptableObject)
							{
								ValueProviderEditorUtility.DeleteSubAsset(oldScriptableObject, saveToDisk: true);
							}
							// If sub-asset of a prefab, ask before deleting, because cross-scene reference could still be used by a base prefab or prefab variant.
							else if(ValueProviderEditorUtility.AskToDeletePreviousSubAssetValue(oldScriptableObject, saveToDisk: true) is not { Length : > 0 })
							{
								return;
							}

							if(Event.current != null)
							{
								GUIUtility.ExitGUI();
							}
						}
					}

					return;
				}

				Assign(referenceProperty, reference);
			}

			static void Convert(ref Object reference, Type type)
			{
				if(!reference || type.IsInstanceOfType(reference))
				{
					return;
				}

				if(!Find.In(GetGameObject(reference), type, out var obj))
				{
					#if DEV_MODE
					Debug.LogWarning($"ObjectPicker selected object of type {reference.GetType().Name} which is not assignable to {type.Name}. Setting target to null.");
					#endif
					reference = null;
				}
				else if(obj is Object unityObject)
				{
					#if DEV_MODE
					Debug.Log($"Converting selection from {reference.GetType().Name} to {unityObject.GetType().Name}.");
					#endif
					reference = unityObject;
				}
				else if(Find.WrapperOf(obj) is Object wrapper)
				{
					#if DEV_MODE
					Debug.Log($"Converting selection from {reference.GetType().Name} to {wrapper.GetType().Name}.");
					#endif
					reference = wrapper;
				}
				else
				{
					#if DEV_MODE
					Debug.LogWarning($"ObjectPicker selected object of type {reference.GetType().Name} which is not an Object. Setting target to null.");
					#endif
					reference = null;
				}
			}

			static Type GetSharedBaseType(Type[] types)
			{
				int count = types.Length;
				if(count == 0)
				{
					return typeof(Object);
				}

				var result = types[0];

				for(int i = count - 1; i >= 1; i--)
				{
					if(types[i].IsAssignableFrom(result))
					{
						result = types[i];
						continue;
					}

					while(result is not null && !result.IsAssignableFrom(types[i]))
					{
						result = result.BaseType;
					}
				}

				return result == typeof(object) ? typeof(Object) : result;
			}
		}
		#endif

		private void OnIconClicked(Object asset, Object target, bool isSceneOrPrefabOpen, string guid, CrossSceneReference crossSceneReference, SerializedProperty referenceProperty, bool pingAssetNotTarget, GUIContent label)
		{
			switch(Event.current.button)
			{
				case 0 when Event.current.clickCount == 2:
					if(asset)
					{
						if(!isSceneOrPrefabOpen)
						{
							OpenSceneOrPrefab();
						}

						EditorApplication.delayCall += SelectTarget;
					}

					return;
				case 0:
					Ping();
					return;
				case 1:
					var menu = new GenericMenu();
					if(asset)
					{
						menu.AddItem(new("Ping"), false, Ping);
					}

					if(asset && !isSceneOrPrefabOpen)
					{
						menu.AddItem(new("Open"), false, OpenSceneOrPrefab);
					}

					menu.AddItem(new("Copy Id"), false, () => EditorGUIUtility.systemCopyBuffer = guid);

					if(crossSceneReference)
					{
						var valueHolder = GetGameObject(referenceProperty.serializedObject.targetObject);
						var crossSceneReferenceIsAsset = AssetDatabase.Contains(crossSceneReference);

						if(crossSceneReferenceIsAsset)
						{
							if(AssetDatabase.IsSubAsset(crossSceneReference) && PrefabUtility.IsPartOfPrefabInstance(valueHolder))
							{
								menu.AddItem(new("Embed Into Prefab Instance"), false, () =>
								{
									if(EditorUtility.DisplayDialog("Convert Into Embedded Cross-Scene Reference?", $"Do you want to convert the cross-scene reference that is currently a sub-asset of the prefab '{AssetDatabase.GetAssetPath(crossSceneReference)}' into one that is embedded in this prefab instance?\n\nChoosing 'Convert' will cause the sub-asset to be removed and embedded into this particular instance of the prefab.", "Convert", "Cancel"))
									{
										ValueProviderEditorUtility.MakeEmbeddedInstance(referenceProperty, crossSceneReference);
									}
								});
							}
						}
						// Allow converting embedded cross-scene references on prefab instances into sub-assets of the prefab asset
						else if(PrefabUtility.IsPartOfPrefabInstance(valueHolder) || PrefabStageUtility.GetPrefabStage(valueHolder))
						{
							menu.AddItem(new("Make Sub-Asset of Prefab"), false, () => ValueProviderEditorUtility.MakeSubAsset(referenceProperty, $"Cross-Scene Reference: {crossSceneReference.CrossSceneId}", saveToDisk: true));
						}
					}

					menu.ShowAsContext();
					return;
			}

			void Ping()
			{
				var objectToPing = !pingAssetNotTarget && target ? target : asset;
				if(objectToPing)
				{
					EditorGUIUtility.PingObject(objectToPing);
				}
			}
			
			void OpenSceneOrPrefab()
			{
				var assetPath = AssetDatabase.GetAssetPath(asset);
				if(asset is SceneAsset)
				{
					EditorSceneManager.OpenScene(assetPath, OpenSceneMode.Additive);
				}
				else
				{
					PrefabStageUtility.OpenPrefab(assetPath);
				}
			}

			void SelectTarget() => Selection.activeGameObject = GetGameObject(targetProperty?.objectReferenceValue);
		}

		internal static bool IsPartOfDifferentPrefabAsset([DisallowNull] Object subject, [MaybeNull] Object target)
		{
			// Reference must be a component, or it couldn't be instantiated into a scene.
			if(!target || GetGameObject(target) is not { } referenceGameObject)
			{
				return false;
			}

			// Reference must be part of a prefab asset or open in a prefab stage.
			if(!AssetDatabase.Contains(referenceGameObject) && !PrefabStageUtility.GetPrefabStage(referenceGameObject))
			{
				return false;
			}

			// Subject and reference must not be part of the same prefab asset or prefab stage.
			return subject is not Component subjectComponent || !ReferenceEquals(subjectComponent.transform.root, referenceGameObject.transform.root);
		}

		internal static void ShowPickPrefabReferenceTypeDropdown(SerializedProperty referenceProperty, Object referenceValue, Rect controlRect)
		{
			var menu = new GenericMenu();
			menu.AddItem(new("Prefab Asset"), false, ()=> SetPrefabAssetReference(referenceProperty, referenceValue));
			menu.AddItem(new("Prefab Instance"), false, ()=> Assign(referenceProperty, referenceValue));
			menu.DropDown(controlRect);

			static void SetPrefabAssetReference(SerializedProperty referenceProperty, Object referenceValue)
			{
				#if DEV_MODE && DEBUG_CROSS_SCENE_REFERENCES
				Debug.Log($"Setting prefab asset reference: {referenceProperty.name} <- {referenceValue.GetType().Name} from {AssetDatabase.GetAssetPath(referenceValue)}", referenceValue);
				#endif

				referenceProperty.objectReferenceValue = referenceValue;
				referenceProperty.serializedObject.ApplyModifiedProperties();
			}
		}

		internal static void Assign(SerializedProperty referenceProperty, [DisallowNull] Object to)
		{
			_ = IdTag.GetOrCreate(to);
			var valueHolderGameObject = GetGameObject(referenceProperty.serializedObject.targetObject);
			var crossSceneReference = Create.Instance<CrossSceneReference, GameObject, Object>(valueHolderGameObject, to);
			var name = $"Cross-Scene Reference: {crossSceneReference.CrossSceneId}";
			crossSceneReference.name = name;
			ValueProviderEditorUtility.Assign(referenceProperty, crossSceneReference, name);
			if(!referenceProperty.serializedObject.IsValid())
			{
				GUIUtility.ExitGUI();
			}
		}

		public void Dispose() => serializedObject.Dispose();

		private static Scene GetScene(Object target) => target is Component component && component ? component.gameObject.scene : target is GameObject gameObject && gameObject ? gameObject.scene : default;
		private static GameObject GetGameObject(Object target) => target is Component component && component ? component.gameObject : target as GameObject;
		
		static GameObject GetGameObject(SerializedProperty serializedProperty)
		{
			var gameObject = GetGameObject(serializedProperty.serializedObject.targetObject);
			if(gameObject)
			{
				return gameObject;
			}

			var editor = LayoutUtility.NowDrawing;
			if(editor)
			{
				return GetGameObject(editor.target);
			}
				
			if(InitParameterGUI.NowDrawing is { } initParameterGUI)
			{
				gameObject = GetGameObject(initParameterGUI.anyProperty?.serializedObject?.targetObject);
				if(gameObject)
				{
					return gameObject;
				}
			}

			return null;
		}

		private sealed class CachedState : IObjectPickerPreviewable
		{
			public readonly CrossSceneReference crossSceneReference;
			[NonSerialized] private string sceneOrAssetPath;
			[NonSerialized] private string tooltip;
			[NonSerialized] private Object sceneOrAsset;
			[NonSerialized] private Object lastTarget;

			public Object ObjectPickerCurrentValue { get; set; }
			public EditorWindow CurrentObjectPicker { get; set; }

			public CachedState(CrossSceneReference crossSceneReference) => this.crossSceneReference = crossSceneReference;

			public Object GetTarget(SerializedProperty serializedProperty)
			{
				var newTarget = crossSceneReference.GetTarget(Context.EditMode);
				if(ReferenceEquals(newTarget, lastTarget))
				{
					return newTarget;
				}

				// Clear cached values here
				lastTarget = newTarget;
				sceneOrAsset = null;
				sceneOrAssetPath = null;
				tooltip = null;

				// Update target-related cached values in CrossSceneReference as well.
				if(newTarget && crossSceneReference.target != newTarget)
				{
					var clientGameObject = GetGameObject(serializedProperty);
					if(!Application.isPlaying || !clientGameObject || !clientGameObject.scene.IsValid())
					{
						((IInitializable<GameObject, Object>)crossSceneReference).Init(GetGameObject(serializedProperty), newTarget);
					}
				}

				return newTarget;
			}

			/// <summary>
			/// Reference to the scene or prefab asset that contains the target.
			/// </summary>
			internal Object ContainingAsset => sceneOrAsset ??= SceneOrAssetPath is { Length: > 0 } ? AssetDatabase.LoadAssetAtPath<Object>(SceneOrAssetPath) : null;

			internal string SceneOrAssetPath
			{
				get
				{
					if(sceneOrAssetPath is not null)
					{
						return sceneOrAssetPath;
					}

					sceneOrAssetPath = AssetDatabase.GUIDToAssetPath(crossSceneReference.sceneOrAssetGuid);
					if(sceneOrAssetPath.Length is 0)
					{
						sceneOrAssetPath = crossSceneReference.sceneOrAssetName;
					}

					return sceneOrAssetPath;
				}
			}

			internal string Tooltip => tooltip ??= $"{(crossSceneReference.referenceType is CrossSceneReferenceType.Scene ? $"In scene '{crossSceneReference.sceneOrAssetName}'" : $"Instance of '{SceneOrAssetPath}'")}\n\nCross-Scene Id:\n{crossSceneReference.guid}";
		}
	}

	internal interface IObjectPickerPreviewable
	{
		Object ObjectPickerCurrentValue { get; set; }
		EditorWindow CurrentObjectPicker { get; set; }
	}
}