using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Sisus.Init.ValueProviders;
using Sisus.Shared.EditorOnly;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using Object = UnityEngine.Object;

namespace Sisus.Init.EditorOnly
{
	internal static class ValueProviderEditorUtility
	{
		/// <summary>
		/// NOTE: Slow method; should not be called during every OnGUI.
		/// </summary>
		public static bool IsSingleSharedInstanceSlow(ScriptableObject valueProvider)
			=> !string.IsNullOrEmpty(AssetDatabase.GetAssetPath(valueProvider))
			&& ValueProviderUtility.TryGetSingleSharedInstanceSlow(valueProvider.GetType(), out var singleSharedInstance)
			&& ReferenceEquals(singleSharedInstance, valueProvider);

		public static IEnumerable<Type> GetAllValueProviderMenuItemTargetTypes()
			=> TypeCache.GetTypesWithAttribute<ValueProviderMenuAttribute>()
			.Where(t => !t.IsAbstract && typeof(ScriptableObject).IsAssignableFrom(t) && ValueProviderUtility.IsValueProvider(t));

		public static void Assign(SerializedProperty referenceProperty, ScriptableObject valueProvider, string name)
		{
			var oldValueProvider = referenceProperty.objectReferenceValue as ScriptableObject;
			if(oldValueProvider == valueProvider)
			{
				#if DEV_MODE
				Debug.Log($"Value assigned to '{referenceProperty.propertyPath}' matches old value. Ignoring.");
				#endif
				return;
			}

			var valueHolder = referenceProperty.serializedObject.targetObject;
			var propertyPath = referenceProperty.propertyPath;
			string createdSubAssetPath = null;
			Object valueHolderAsset = null;

			try
			{
				if(!valueProvider)
				{
					#if DEV_MODE
					Debug.LogWarning($"Value provider assigned to '{referenceProperty.propertyPath}' was null.");
					#endif
					return;
				}

				// Value provider is backed by an asset -> just assign the reference directly
				if(AssetDatabase.Contains(valueProvider))
				{
					referenceProperty.objectReferenceValue = valueProvider;
					return;
				}

				// Prefab or ScriptableObject asset -> make sub-asset
				if(AssetDatabase.Contains(valueHolder))
				{
					valueHolderAsset = valueHolder;
				}
				// Prefab being edited in a prefab stage -> make sub-asset
				else if(GetGameObject(valueHolder) is { } gameObject && gameObject
					&& PrefabStageUtility.GetPrefabStage(gameObject) is { } prefabStage && prefabStage)
				{
					valueHolderAsset = AssetDatabase.LoadMainAssetAtPath(prefabStage.assetPath);
				}
				// Non-asset -> just assign the reference directly
				else
				{
					referenceProperty.objectReferenceValue = valueProvider;
					return;
				}

				createdSubAssetPath = AssetDatabase.GetAssetPath(valueHolderAsset);
				MakeSubAsset(referenceProperty, valueHolderAsset, valueProvider, name, saveToDisk: true);
			}
			finally
			{
				if(!referenceProperty.serializedObject.IsValid())
				{
					using var serializedObject = new SerializedObject(valueHolder);
					using var serializedProperty = serializedObject.FindProperty(propertyPath);
					serializedProperty.objectReferenceValue = valueProvider;
					serializedObject.ApplyModifiedProperties();
				}
				else
				{
					referenceProperty.objectReferenceValue = valueProvider;
					referenceProperty.serializedObject.ApplyModifiedProperties();
				}

				var savedAssetPath = HandleDeletePreviousValueProvider(oldValueProvider, saveToDisk: true);
				if(!string.IsNullOrEmpty(createdSubAssetPath) && !string.Equals(createdSubAssetPath, savedAssetPath))
				{
					SaveAsset(createdSubAssetPath, setDirty: true, mainAsset: valueHolderAsset, subAsset: valueProvider);
				}
			}
		}

		/// <returns>
		/// Path of the asset from which the previous sub-asset value was deleted, or null if no deletion took place.
		/// </returns>
		private static string HandleDeletePreviousValueProvider(ScriptableObject oldValueProvider, bool saveToDisk)
		{
			if(!oldValueProvider)
			{
				return null;
			}

			if(AssetDatabase.IsSubAsset(oldValueProvider))
			{
				return AskToDeletePreviousSubAssetValue(oldValueProvider, saveToDisk: saveToDisk);
			}

			if(!AssetDatabase.Contains(oldValueProvider))
			{
				Undo.DestroyObjectImmediate(oldValueProvider);
			}

			return null;
		}

		public static void MakeSubAsset(SerializedProperty referenceProperty, string name, bool saveToDisk)
		{
			var valueProvider = referenceProperty.objectReferenceValue as ScriptableObject;
			if(!valueProvider)
			{
				#if DEV_MODE
				Debug.LogWarning($"Cannot make sub-asset: The property '{referenceProperty.propertyPath}' does not reference a ScriptableObject.");
				#endif
				return;
			}

			if(AssetDatabase.Contains(valueProvider))
			{
				#if DEV_MODE
				Debug.LogWarning($"Cannot make sub-asset: The value provider '{valueProvider}' is already an asset in the project.");
				#endif
				return;
			}

			var instance = referenceProperty.serializedObject.targetObject;
			Object asset;
			if(AssetDatabase.Contains(instance))
			{
				asset = instance;
			}
			else if(PrefabUtility.GetPrefabAssetPathOfNearestInstanceRoot(instance) is { Length: > 0 } prefabPath)
			{
				asset = AssetDatabase.LoadMainAssetAtPath(prefabPath);
			}
			else if(GetGameObject(instance) is { } gameObject && gameObject
				&& PrefabStageUtility.GetPrefabStage(gameObject) is { } prefabStage && prefabStage)
			{
				asset = AssetDatabase.LoadMainAssetAtPath(prefabStage.assetPath);
			}
			else
			{
				#if DEV_MODE
				Debug.LogWarning($"Cannot make sub-asset: The target object '{instance}' is not part of a prefab instance.");
				#endif
				return;
			}

			MakeSubAsset(referenceProperty, asset, valueProvider, name, saveToDisk);
		}

		public static void MakeSubAsset(SerializedProperty referenceProperty, Object asset, ScriptableObject embeddedValueProvider, string name, bool saveToDisk)
		{
			var assetPath = AssetDatabase.GetAssetPath(asset);
			var undoName = $"Make Sub-Asset of {(asset is GameObject ? "Prefab" : "asset")}";

			if(!string.IsNullOrEmpty(name))
			{
				embeddedValueProvider.name = name;
			}

			EditorUtility.SetDirty(embeddedValueProvider);

			Undo.RegisterImporterUndo(assetPath, undoName);
			var valueHolder = referenceProperty.serializedObject.targetObject;
			if(valueHolder != asset)
			{
				Undo.RegisterFullObjectHierarchyUndo(valueHolder, undoName);
				EditorUtility.SetDirty(valueHolder);
			}

			AssetDatabase.AddObjectToAsset(embeddedValueProvider, assetPath);
			EditorUtility.SetDirty(asset);

			if(saveToDisk)
			{
				SaveAsset(assetPath, setDirty: true, mainAsset: asset, subAsset: embeddedValueProvider);
			}

			if(valueHolder && PrefabUtility.IsPartOfPrefabInstance(valueHolder) && referenceProperty.serializedObject.IsValid())
			{
				PrefabUtility.ApplyPropertyOverride(referenceProperty, assetPath, InteractionMode.UserAction);
			}

			Debug.Log($"Converted embedded value provider {embeddedValueProvider.GetType().Name} into a sub-asset of '{assetPath}'.", asset);
		}

		/// <summary>
		/// Given an Any.reference on a prefab instance with an sub-asset ScriptableObject value provider, converts the value provider
		/// into being embedded in the prefab instance instead.
		/// </summary>
		public static void MakeEmbeddedInstance(SerializedProperty referenceProperty, ScriptableObject subAssetValueProvider)
		{
			var selectionWas = Selection.objects;
			var embeddedValueProvider = Object.Instantiate(subAssetValueProvider);
			while(embeddedValueProvider.name.EndsWith("(Clone)", StringComparison.Ordinal))
			{
				embeddedValueProvider.name = embeddedValueProvider.name.Substring(0, embeddedValueProvider.name.Length - "(Clone)".Length);
			}

			var prefabPath = AssetDatabase.GetAssetPath(subAssetValueProvider);
			Undo.RegisterImporterUndo(prefabPath, "Embed Into Prefab Instance");
			var targetObject = referenceProperty.serializedObject.targetObject;
			Undo.RegisterFullObjectHierarchyUndo(targetObject, "Embed Into Prefab Instance");
			AssetDatabase.RemoveObjectFromAsset(subAssetValueProvider);
			EditorUtility.SetDirty(targetObject);
			referenceProperty.objectReferenceValue = embeddedValueProvider;
			referenceProperty.serializedObject.ApplyModifiedProperties();
			var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(prefabPath);
			if(prefab)
			{
				EditorUtility.SetDirty(prefab);
				PrefabUtility.SavePrefabAsset(prefab);
			}
			Selection.objects = selectionWas;
			EditorGUIUtility.PingObject(targetObject);

			Debug.Log($"Converted the value provider {embeddedValueProvider.GetType().Name} from being a sub-asset of the prefab '{prefabPath}' into being embedded in the prefab instance '{targetObject}'.", targetObject);
		}

		public static void DeleteSubAsset(ScriptableObject subAssetValueProvider, bool saveToDisk)
		{
			var selectionWas = Selection.objects;
			var assetPath = AssetDatabase.GetAssetPath(subAssetValueProvider);
			Undo.RegisterImporterUndo(assetPath, "Delete Sub-Asset");
			Undo.DestroyObjectImmediate(subAssetValueProvider);
			var asset = AssetDatabase.LoadAssetAtPath<Object>(assetPath);
			if(asset)
			{
				EditorUtility.SetDirty(asset);
			}

			if(saveToDisk)
			{
				SaveAsset(assetPath, setDirty: false, mainAsset: asset);
			}

			Debug.Log($"Deleted the sub-asset value provider {subAssetValueProvider.GetType().Name} from '{assetPath}'.", asset);

			Selection.objects = selectionWas;
		}

		public static void SaveAsset(string assetPath, bool setDirty, Object mainAsset = null, Object subAsset = null)
		{
			if(!mainAsset)
			{
				mainAsset = AssetDatabase.LoadAssetAtPath<Object>(assetPath);
			}

			if(setDirty)
			{
				if(mainAsset)
				{
					EditorUtility.SetDirty(mainAsset);
				}

				if(subAsset)
				{
					EditorUtility.SetDirty(subAsset);
				}
			}
			
			if(PrefabStageUtility.GetCurrentPrefabStage() is { } prefabStage 
				&& prefabStage && string.Equals(prefabStage.assetPath, assetPath))
			{
				PrefabUtility.SaveAsPrefabAsset(prefabStage.prefabContentsRoot, prefabStage.assetPath);
			}
			else if(mainAsset is GameObject prefab && prefab)
			{
				PrefabUtility.SavePrefabAsset(prefab);
			}
			else
			{
				if(subAsset)
				{
					AssetDatabase.SaveAssetIfDirty(subAsset);
				}

				if(mainAsset)
				{
					AssetDatabase.SaveAssetIfDirty(mainAsset);
				}
				else
				{
					AssetDatabase.ImportAsset(assetPath, ImportAssetOptions.ForceUpdate);
				}
			}
		}

		/// <returns>
		/// Path of the asset from which the previous sub-asset value was deleted, or null if no deletion took place.
		/// </returns>
		public static string AskToDeletePreviousSubAssetValue(ScriptableObject previousSubAssetValue, bool saveToDisk)
		{
			var assetPath = AssetDatabase.GetAssetPath(previousSubAssetValue);
			if(EditorUtility.DisplayDialog("Delete Sub-Asset?", $"The previous value is serialized as a sub-asset of '{Path.GetFileName(assetPath)}'.\n\nWould you like to delete the sub-asset '{previousSubAssetValue.name}'?", "Delete", "Leave It"))
			{
				DeleteSubAsset(previousSubAssetValue, saveToDisk: saveToDisk);
				return assetPath;
			}

			return null;
		}

		private static GameObject GetGameObject(Object obj)
		{
			if(obj is Component component)
			{
				return component.gameObject;
			}

			if(obj is GameObject go)
			{
				return go;
			}

			return null;
		}
	}
}
