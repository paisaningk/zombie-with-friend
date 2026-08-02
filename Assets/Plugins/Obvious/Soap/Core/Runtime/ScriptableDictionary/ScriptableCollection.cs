using System;
using UnityEngine;
using UnityEngine.SceneManagement;
#if UNITY_EDITOR
using UnityEditor;
#endif

namespace Obvious.Soap
{
    public abstract class ScriptableCollection : ScriptableBase, IReset
    {
        [Tooltip(
            "Clear the collection when:\n" +
            " Scene Loaded : when a scene is loaded.\n" +
            " Application Start : Once, when the application starts. Modifications persist between scenes")]
        [SerializeField] protected ResetTiming _resetOn = ResetTiming.OnSceneUnload;

        [HideInInspector] public Action Modified;
        public event Action OnCleared;
        public abstract int Count { get; }
        public abstract bool CanBeSerialized();
        private ResetTiming _lastResetTiming;
#if UNITY_6000_5_OR_NEWER
        private SceneHandle _activeSceneHandle;
#else
        private Int32 _activeSceneHandle;
#endif

        protected virtual void Awake()
        {
            hideFlags = HideFlags.DontUnloadUnusedAsset;
        }

        protected virtual void OnEnable()
        {
#if UNITY_EDITOR
            _lastResetTiming = _resetOn; 
#endif
            if (_resetOn == ResetTiming.Never)
                return;

            Clear();
            RegisterResetHooks(_resetOn);
        }

        protected virtual void OnDisable()
        {
            UnregisterResetHooks();
        }

        private void RegisterResetHooks(ResetTiming resetTiming)
        {
            if (resetTiming == ResetTiming.OnSceneUnload)
                SceneManager.sceneUnloaded += OnSceneUnloaded;
#if UNITY_EDITOR
            if (resetTiming != ResetTiming.Never)
                EditorApplication.playModeStateChanged += OnPlayModeStateChanged;
#endif
        }

        private void UnregisterResetHooks()
        {
            SceneManager.sceneUnloaded -= OnSceneUnloaded;
#if UNITY_EDITOR
            EditorApplication.playModeStateChanged -= OnPlayModeStateChanged;
#endif
        }

        protected virtual void OnSceneUnloaded(Scene scene)
        {
            if (_resetOn != ResetTiming.OnSceneUnload)
                return;
            if (IsSceneHandleNull() || _activeSceneHandle == scene.handle)
            { 
                Clear();
            }
        }

        public virtual void Clear()
        {
            OnCleared?.Invoke();
            Modified?.Invoke();
        }

        internal override void Reset()
        {
            base.Reset();
            _resetOn = ResetTiming.OnSceneUnload;
            Clear();
        }

        public void ResetValue() => Clear();

#if UNITY_EDITOR
        public virtual void OnPlayModeStateChanged(PlayModeStateChange playModeStateChange)
        {
            if (playModeStateChange == PlayModeStateChange.EnteredEditMode ||
                playModeStateChange == PlayModeStateChange.ExitingEditMode)
                Clear();
        }

        private void OnValidate()
        {
            if (_lastResetTiming != _resetOn)
            {
                UnregisterResetHooks();
                RegisterResetHooks(_resetOn);
                _lastResetTiming = _resetOn;
            }
        }
#endif

        private bool IsSceneHandleNull()
        {
#if UNITY_6000_5_OR_NEWER
            return _activeSceneHandle == SceneHandle.None;
#else
            return _activeSceneHandle == 0;
#endif
        }
    }
}