//#define SHOW_REF_TAGS_IN_INSPECTOR
//#define DEBUG_ENABLED
//#define REF_TAG_THREAD_SAFETY_ENABLED
//#define REF_TAG_THREAD_SAFETY_DISABLED

using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Linq;
using Sisus.Init.Internal;
using UnityEngine;
using Id = Sisus.Init.Internal.Id;
using Object = UnityEngine.Object;
#if UNITY_EDITOR
using UnityEditor;
using UnityEditor.SceneManagement;
#endif

namespace Sisus.Init
{
	[DefaultExecutionOrder(ExecutionOrder.IdTag), ExecuteAlways]
	internal sealed class IdTag : MonoBehaviour, IInitializable<Object>, IIdentifiable, ISerializationCallbackReceiver
	{
		private static readonly Dictionary<Id, Action<Object>> loadedListeners = new(0);
		private static readonly Dictionary<Id, Object> activeInstances = new();

		#if UNITY_EDITOR
		private static readonly List<IdTag> allTags = new();
		internal static List<IdTag> AllTags
		{
			get
			{
				allTags.RemoveAll(x => !x);
				return allTags;
			}
		}
		#endif

		[SerializeField]
		internal Id guid = Id.Empty;

		[SerializeField]
		internal Object target = null;
		
		#if DEBUG || INIT_ARGS_SAFE_MODE
		#pragma warning disable CS0414
		[SerializeField]
		private string globalObjectIdSlow = null;
		#endif

		#if UNITY_EDITOR
		#pragma warning disable CS0414
		[SerializeField]
		private SceneAsset scene = null;
		#pragma warning restore CS0414
		#endif

		public Id Guid => guid;
		public Object Target => target;
		Id IIdentifiable.Id => guid;

		public static Object GetInstance(Id guid)
		{
			#if REF_TAG_THREAD_SAFETY_ENABLED && !REF_TAG_THREAD_SAFETY_DISABLED
			lock(activeInstances)
			#endif
			{
				return activeInstances.TryGetValue(guid, out var instance) ? instance : null;
			}
		}

		public static void AddLoadedListener(Id id, Action<Object> listener)
		{
			if(loadedListeners.TryGetValue(id, out var existingListeners))
			{
				loadedListeners[id] = existingListeners + listener;
			}
			else
			{
				loadedListeners[id] = listener;
			}
		}

		public static void RemoveLoadedListener(Id id, Action<Object> listener)
		{
			if(!loadedListeners.TryGetValue(id, out var existingListeners))
			{
				return;
			}

			existingListeners -= listener;
			if(existingListeners is null)
			{
				loadedListeners.Remove(id);
			}
			else
			{
				loadedListeners[id] = existingListeners;
			}
		}

		internal static bool TryGet(GameObject gameObject, Object target, out IdTag idTag)
		{
			foreach(var someIdTag in gameObject.GetComponentsNonAlloc<IdTag>())
			{
				if(someIdTag.Target == target)
				{
					idTag = someIdTag;
					return true;
				}
			}

			idTag = null;
			return false;
		}

		[return: NotNull]
		internal static IdTag GetOrCreate([DisallowNull] Object target)
		{
			var gameObject = GetGameObject(target);
			if(TryGet(gameObject, target, out var idTag))
			{
				return idTag;
			}

			#if UNITY_EDITOR
			if(!Application.isPlaying || !gameObject.scene.IsValid() || PrefabStageUtility.GetPrefabStage(gameObject))
			{
				idTag = Undo.AddComponent<IdTag>(gameObject);
				((IInitializable<Object>)idTag).Init(target);
				return idTag;
			}
			#endif

			return gameObject.AddComponent<IdTag, Object>(target);
		}

		void IInitializable<Object>.Init(Object target)
		{
			#if DEV_MODE && DEBUG_ENABLED
			Debug.Log($"IdTag.Init({target?.GetType().Name}) on {gameObject.name} ({gameObject.scene.name})", this);
			#endif

			this.target = target;

			#if UNITY_EDITOR
			if(!target)
			{
				scene = null;
				guid = Id.Empty;
				globalObjectIdSlow = null;
				return;
			}

			scene = AssetDatabase.LoadAssetAtPath<SceneAsset>(gameObject.scene.path);
			globalObjectIdSlow = GlobalObjectId.GetGlobalObjectIdSlow(target).ToString();
			#endif
		}

		private void OnEnable()
		{
			#if DEV_MODE || DEBUG || INIT_ARGS_SAFE_MODE 
			if(!target)
			{
				return;
			}
			#endif

			Register();
		}

		private void OnDisable()
		{
			#if DEV_MODE || DEBUG || INIT_ARGS_SAFE_MODE 
			if(!target)
			{
				return;
			}
			#endif

			Deregister();
		}

		private void Register()
		{
			#if REF_TAG_THREAD_SAFETY_ENABLED && !REF_TAG_THREAD_SAFETY_DISABLED
			lock(activeInstances)
			#endif
			{
				#if UNITY_EDITOR
				if(!allTags.Contains(this))
				{
					allTags.Add(this);
				}

				if(!Application.isPlaying || !gameObject.scene.IsValid() || PrefabStageUtility.GetPrefabStage(gameObject))
				{
					return;
				}
				#endif

				activeInstances[guid] = target;

				#if DEV_MODE && DEBUG_ENABLED && UNITY_EDITOR 
				Debug.Log($"Registered {target.GetType().Name} as activate instance\nguid:{guid}, Scene:{(gameObject.scene.IsValid() ? gameObject.scene.name : "n/a")}, Path:{AssetDatabase.GetAssetOrScenePath(gameObject)}", this);
				#endif

				if(loadedListeners.TryGetValue(guid, out var onLoaded))
				{
					#if DEV_MODE && DEBUG_ENABLED && UNITY_EDITOR 
					Debug.Log($"Notifying {onLoaded.GetInvocationList().Length} Loaded Listeners of {target.GetType().Name}.\nguid:{guid}), Path:{AssetDatabase.GetAssetOrScenePath(gameObject)}", this);
					#endif
					loadedListeners.Remove(guid);
					onLoaded(target);
				}
				else
				{
					#if DEV_MODE && DEBUG_ENABLED && UNITY_EDITOR 
					Debug.Log($"{target.GetType().Name} had no Loaded Listeners.\nguid:{guid}), Path:{AssetDatabase.GetAssetOrScenePath(gameObject)}", this);
					#endif
				}
			}
		}

		private void Deregister()
		{
			#if REF_TAG_THREAD_SAFETY_ENABLED && !REF_TAG_THREAD_SAFETY_DISABLED
			lock(activeInstances)
			#endif
			{
				#if UNITY_EDITOR
				allTags.Remove(this);
				#endif

				if(activeInstances.TryGetValue(guid, out var instance) && ReferenceEquals(instance, target))
				{
					#if DEV_MODE && DEBUG_ENABLED && UNITY_EDITOR 
					Debug.Log($"Deregistering {target.GetType().Name} ({guid})\nPath:{AssetDatabase.GetAssetOrScenePath(gameObject)}", this);
					#endif
					activeInstances.Remove(guid);
				}
			}
		}
		
		void ISerializationCallbackReceiver.OnBeforeSerialize() { }

		public async void OnAfterDeserialize()
		{
			await Until.UnitySafeContext();

			#if !UNITY_EDITOR
			if(!target || !gameObject.scene.IsValid())
			{
				return;
			}
			#else
			if(!this)
			{
				return;
			}
			#endif

			if(!target)
			{
				Register();
			}
		}

		private static GameObject GetGameObject(Object target) => target is Component component && component ? component.gameObject : target as GameObject;

		#if UNITY_EDITOR
		private void Reset()
		{
			guid = Id.NewId();

			#if !DEV_MODE || !SHOW_REF_TAGS_IN_INSPECTOR
			hideFlags = HideFlags.HideInInspector;
			#endif
		}

		private async void OnValidate()
		{
			await Until.UnitySafeContext();
			OnValidateMainThread();
		}

		private void OnValidateMainThread()
		{
			if(!this)
			{
				#if UNITY_2022_3_OR_NEWER
				if(!Application.isPlaying)
				{
					PrefabUtility.prefabInstanceUnpacked -= OnPrefabInstanceUnpacked;
				}
				#endif
				return;
			}

			#if UNITY_2022_3_OR_NEWER
			if(!Application.isPlaying)
			{
				PrefabUtility.prefabInstanceUnpacked -= OnPrefabInstanceUnpacked;
				PrefabUtility.prefabInstanceUnpacked += OnPrefabInstanceUnpacked;
			}
			#endif

			// Target can temporarily appear as null etc. when script compilation has failed.
			if(EditorUtility.scriptCompilationFailed)
			{
				return;
			}

			if(!TargetExistsOnSameGameObject())
			{
				#if DEV_MODE && UNITY_EDITOR
				Debug.LogWarning($"Cross-scene reference target of '{AssetDatabase.GetAssetOrScenePath(this)}/{name}' exist on another GameObject '{AssetDatabase.GetAssetOrScenePath(this)}/{(target ? target.name : "null")}'. Destroying IdTag...", target);
				#endif

				DestroySelf();
				return;
			}

			#if DEV_MODE && SHOW_REF_TAGS_IN_INSPECTOR
			hideFlags = HideFlags.None;
			#else
			hideFlags = HideFlags.HideInInspector;
			#endif

			bool dirty = false;

			var isPrefabAsset = PrefabUtility.IsPartOfPrefabAsset(this);
			var isOpenInPrefabStage = PrefabStageUtility.GetPrefabStage(gameObject);
			var isPrefabAssetOrOpenInPrefabStage = isPrefabAsset || isOpenInPrefabStage;

			var setScene = isPrefabAssetOrOpenInPrefabStage ? null : AssetDatabase.LoadAssetAtPath<SceneAsset>(gameObject.scene.path);
			if(setScene != scene)
			{
				#if DEV_MODE && DEBUG_ENABLED && UNITY_EDITOR
				Debug.Log("IdTag.SetScene(" + (setScene ? setScene.name : "null") + ") for " + name, this);
				#endif

				Undo.RecordObject(this, "Update Cross-Scene Reference");
				if(AllTags.Any(x => x.guid == guid && !CouldHaveEssentiallyTheSameTarget(this, x)))
				{
					#if DEV_MODE
					Debug.LogWarning($"Found another IdTag with the same GlobalObjectIdSlow '{globalObjectIdSlow}' and target '{target}'. Generating a new Id for this IdTag." +
					                 $"this:{name} at {scene}", this);
					#endif
					GenerateNewId();
				}
				scene = isPrefabAssetOrOpenInPrefabStage ? null : AssetDatabase.LoadAssetAtPath<SceneAsset>(gameObject.scene.path);
				dirty = true;
			}

			// Handle events where prefab, scene or GameObject containing this Referenceable has been duplicated.
			var newGlobalObjectIdSlow = GlobalObjectId.GetGlobalObjectIdSlow(target).ToString();
			if(!newGlobalObjectIdSlow.Equals(globalObjectIdSlow) && newGlobalObjectIdSlow != default(GlobalObjectId).ToString())
			{
				#if DEV_MODE
				if(!target)
				{
					Debug.LogWarning($"GlobalObjectId of {target.GetType().Name} on game object '{name}' in '{AssetDatabase.GetAssetOrScenePath(target)}' has changed from {globalObjectIdSlow} to {newGlobalObjectIdSlow}. GameObject has potentially been duplicated. Updating Id...", this);
				}
				else if(!scene)
				{
					Debug.LogWarning($"GlobalObjectId of target on game object '{name}' in scene '{scene}' has changed from {globalObjectIdSlow} to {newGlobalObjectIdSlow}. GameObject has potentially been duplicated. Updating Id...", this);
				}
				else
				{
					Debug.LogWarning($"GlobalObjectId of '{name}' in scene '{guid}' has changed from {globalObjectIdSlow} to {newGlobalObjectIdSlow}. GameObject has potentially been duplicated. Updating Id...", this);
				}
				#endif

				Undo.RecordObject(this, "Update Cross-Scene Reference");
				globalObjectIdSlow = newGlobalObjectIdSlow;
				if(AllTags.Any(x => x.guid == guid && !CouldHaveEssentiallyTheSameTarget(this, x)))
				{
					#if DEV_MODE
					Debug.LogWarning($"Found another IdTag with the same GlobalObjectIdSlow '{globalObjectIdSlow}' and target '{target}'. Generating a new Id for this IdTag." +
					                 $"this:{name} at {scene}", this);
					#endif
					GenerateNewId();
				}

				dirty = true;
			}

			if(!HasValidId() && (!Application.isPlaying || isPrefabAsset))
			{
				Undo.RecordObject(this, "Update Cross-Scene Reference");
				GenerateNewId();
				dirty = true;
			}

			if(dirty && isPrefabAsset)
			{
				EditorApplication.delayCall += ()=>
				{
					if(!this)
					{
						return;
					}

					PrefabUtility.SavePrefabAsset(gameObject.transform.root.gameObject);
				};
			}

			Register();
		}

		private void DestroySelf()
		{
			if(!Application.isPlaying || !gameObject.scene.IsValid() || PrefabStageUtility.GetPrefabStage(gameObject))
			{
				DestroyImmediate(this, allowDestroyingAssets: true);
				return;
			}

			Destroy(this);
		}

		#if UNITY_2022_3_OR_NEWER
		private void OnPrefabInstanceUnpacked(GameObject gameObject, PrefabUnpackMode mode) => OnValidateMainThread();
		#endif

		private bool HasValidId()
		{
			if(guid == Id.Empty)
			{
				return false;
			}

			foreach(var refTag in allTags)
			{
				// In case of duplicates, we consider the earliest index valid, and any later ones invalid.
				// Thus once we've reached this tag among all the tags, we can stop iteration.
				if(ReferenceEquals(refTag, this))
				{
					return true;
				}

				if(refTag && refTag.guid == guid && !CouldHaveEssentiallyTheSameTarget(this, refTag))
				{
					return false;
				}
			}

			return true;
		}
		
		private static bool CouldHaveEssentiallyTheSameTarget([DisallowNull] IdTag a, [DisallowNull] IdTag b)
		{
			if(ReferenceEquals(a, b))
			{
				return true;
			}

			var targetA = a.target;
			var targetB = b.target;
			if(ReferenceEquals(targetA, targetB))
			{
				return true;
			}

			var gameObjectA = a.gameObject;
			bool aIsPrefabAsset = PrefabUtility.IsPartOfPrefabAsset(gameObjectA);
			bool aIsOpenInPrefabStage = PrefabStageUtility.GetPrefabStage(gameObjectA);
			bool aIsPrefabInstance = PrefabUtility.IsPartOfPrefabInstance(gameObjectA);
			bool aIsNormalSceneObject = !aIsPrefabAsset && !aIsOpenInPrefabStage && !aIsPrefabInstance;

			var gameObjectB = b.gameObject;
			bool bIsPrefabAsset = PrefabUtility.IsPartOfPrefabAsset(gameObjectB);
			bool bIsOpenInPrefabStage = PrefabStageUtility.GetPrefabStage(gameObjectB);
			bool bIsPrefabInstance = PrefabUtility.IsPartOfPrefabInstance(gameObjectB);
			bool bIsNormalSceneObject = !bIsPrefabAsset && !bIsOpenInPrefabStage && !bIsPrefabInstance;

			// If either one is a scene object which is not a prefab instance, we have an id conflict.
			// A scene object could have been duplicated, or a prefab instance unpacked.
			if(aIsNormalSceneObject || bIsNormalSceneObject)
			{
				return false;
			}

			// Prefab assets can share their id with a prefab instance or an object being edited in prefab stage.
			// One reason why prefab instances should be allowed to share an id with a prefab asset is that a user
			// could temporarily drag-and-drop a prefab asset into a scene to edit it, and then apply changes back to the prefab asset.
			// If both are prefab assets, it's likely that one asset was duplicated, in which case we want to generate a new id for the new one. 
			if(aIsPrefabAsset)
			{
				return bIsPrefabInstance || bIsOpenInPrefabStage;
			}

			// Objects being edited in prefab stage can share their id with prefab assets and prefab instance.
			// If two objects in the same prefab stage share the same id, it is likely that one of them was duplicated,
			// in which case we want to generate a new id for the new one.
			if(aIsOpenInPrefabStage)
			{
				return bIsPrefabAsset || bIsPrefabInstance;
			}

			// Prefab instances can share their id with a prefab asset and an object being edited in prefab stage.
			// If two prefab instances share the same id, it is likely that one of them was duplicated,
			// in which case we want to generate a new id for the new one.
			return bIsPrefabAsset || bIsOpenInPrefabStage;
		}

		internal void GenerateNewId()
		{
			#if DEV_MODE && DEBUG_ENABLED && UNITY_EDITOR
			Debug.Log($"Generating a new Id for {target?.GetType().Name} on {name} in {AssetDatabase.GetAssetOrScenePath(gameObject)}\nOld guid:{guid}", target);
			#endif

			guid = Id.NewId();
		}

		private bool TargetExistsOnSameGameObject() => GetGameObject(target) == gameObject;
		#endif
	}
}