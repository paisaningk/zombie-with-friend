//#define DEBUG_LOAD_SCENE

#if UNITY_EDITOR
using System;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
#endif
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Init.Demo
{
	/// <summary>
	/// Component responsible for loading all the subscenes of the demo
	/// when the main scene is loaded in edit mode or at runtime.
	/// </summary>
	[AddComponentMenu("Initialization/Demo/Subscenes Load Handler")]
	public sealed class SubscenesLoadHandler : MonoBehaviour
	{
		[SerializeField]
		private string[] subscenes = new string[0];

		#pragma warning disable CS0414
		[SerializeField]
		private bool autoLoadInEditMode = false;
		#pragma warning restore CS0414

		private void Awake() => LoadAllSubscenes();

		#if UNITY_EDITOR
		private void OnValidate()
		{
			if(autoLoadInEditMode)
			{
				EditorApplication.delayCall -= AutoLoadSubscenesWhenReady;
				EditorApplication.delayCall += AutoLoadSubscenesWhenReady;
			}
		}

		private void Reset() => RebuildSubscenesList();
		#endif

		[ContextMenu("Load All Subscenes")]
		private void LoadAllSubscenes()
		{
			#if UNITY_EDITOR
			if(!EditorApplication.isPlaying)
			{
				OpenAllSubscenesInEditMode();
				return;
			}
			#endif

			for(int i = 0, count = subscenes.Length; i < count; i++)
			{
				string subsceneName = subscenes[i];
				var subscene = SceneManager.GetSceneByName(subsceneName);
				if(!subscene.IsValid())
				{ 
					#if DEV_MODE && DEBUG_LOAD_SCENE
					Debug.Log($"SubscenesLoadHandler - LoadScene({subsceneName}) with sceneCount:{SceneManager.sceneCount}", this);
					#endif

					SceneManager.LoadScene(subsceneName, LoadSceneMode.Additive);
				}
			}
		}

		#if UNITY_EDITOR
		[ContextMenu("Unload All Subscenes")]
		private void UnloadAllSubscenes()
		{
			if(!EditorApplication.isPlaying)
			{
				var mainScene = gameObject.scene;

				for(int i = SceneManager.sceneCount - 1; i >= 0; i--)
				{
					var scene = SceneManager.GetSceneAt(i);
					string path = scene.path;
					string name = Path.GetFileNameWithoutExtension(path);
					if(Array.IndexOf(subscenes, name) != -1 && scene != mainScene)
					{
						EditorSceneManager.CloseScene(scene, true);
					}
				}

				return;
			}

			for(int i = 0, count = subscenes.Length; i < count; i++)
			{
				SceneManager.UnloadSceneAsync(subscenes[i]);
			}
		}
		
		[ContextMenu("Rebuild Subscenes List")]
		private void RebuildSubscenesList()
		{
			string mainSceneName = gameObject.scene.name;
			string subscenePrefix = mainSceneName + ".";

			foreach(string guid in AssetDatabase.FindAssets("t:SceneAsset"))
			{
				var sceneAsset = AssetDatabase.LoadAssetAtPath<SceneAsset>(AssetDatabase.GUIDToAssetPath(guid));
				if(sceneAsset.name.StartsWith(subscenePrefix))
				{
					int index = subscenes.Length;
					Array.Resize(ref subscenes, index + 1);
					subscenes[index] = sceneAsset.name;
				}
			}
		}

		private void OpenAllSubscenesInEditMode()
		{
			var sceneAssetPaths = AssetDatabase.FindAssets("t:SceneAsset").Select(AssetDatabase.GUIDToAssetPath).ToList();
			foreach(var subsceneName in subscenes)
			{
				if(sceneAssetPaths.FirstOrDefault(x => string.Equals(subsceneName, Path.GetFileNameWithoutExtension(x))) is not { } sceneAssetPath)
				{
					continue;
				}

				if(Enumerable.Range(0, SceneManager.sceneCount)
					.Select(SceneManager.GetSceneAt)
					.Any(x => x.name == subsceneName))
				{
					continue;
				}

				#if DEV_MODE && DEBUG_LOAD_SCENE
				Debug.Log($"SubscenesLoadHandler - OpenScene({sceneAssetPath}) with sceneCount:{SceneManager.sceneCount}", this);
				#endif

				EditorSceneManager.OpenScene(sceneAssetPath, OpenSceneMode.Additive);
			}
		}

		private void AutoLoadSubscenesWhenReady()
		{
			if(!this || !autoLoadInEditMode || Application.isPlaying)
			{
				return;
			}

			if(EditorGUIUtility.editingTextField || EditorApplication.isCompiling || EditorApplication.isUpdating || BuildPipeline.isBuildingPlayer)
			{
				EditorApplication.delayCall -= AutoLoadSubscenesWhenReady;
				EditorApplication.delayCall += AutoLoadSubscenesWhenReady;
				return;
			}

			var mainScene = gameObject.scene;
			for(int i = SceneManager.sceneCount - 1; i >= 0; i--)
			{
				var scene = SceneManager.GetSceneAt(i);
				string path = scene.path;
				string name = Path.GetFileNameWithoutExtension(path);
				if(Array.IndexOf(subscenes, name) == -1 && scene != mainScene)
				{
					EditorSceneManager.CloseScene(scene, true);
				}
			}

			LoadAllSubscenes();
		}
		#endif
	}
}