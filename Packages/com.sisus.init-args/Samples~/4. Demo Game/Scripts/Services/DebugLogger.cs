using System;
using System.Diagnostics;
using Sisus.Init;
using Debug = UnityEngine.Debug;
using Object = UnityEngine.Object;

namespace Init.Demo
{
    /// <summary>
    /// Service responsible for <see cref="Debug.Log"/> logging behaviour
    /// both at runtime and in edit mode.
    /// </para>
    /// </summary>
    [Service(typeof(ILogger)), EditorService(typeof(ILogger))]
    public sealed class DebugLogger : ILogger
    {
        [DebuggerStepThrough]
        public void Log(object message, object context = null)
        {
            #if UNITY_EDITOR
            Object unityObject = context as Object;
            if(unityObject is null && !(context is null))
            {
                unityObject = Find.WrapperOf(context) as Object;
            }
            Debug.Log(message, unityObject);
            #elif DEBUG
            Debug.Log(message);
            #endif
        }

        [DebuggerStepThrough]
        public void LogWarning(object message, object context = null)
        {
            #if UNITY_EDITOR
            Object unityObject = context as Object;
            if(unityObject is null && !(context is null))
            {
                unityObject = Find.WrapperOf(context) as Object;
            }
            Debug.LogWarning(message, unityObject);
            #elif DEBUG
            Debug.LogWarning(message);
            #endif
        }

        [DebuggerStepThrough]
        public void LogError(object message, object context = null)
        {
            #if UNITY_EDITOR
            Object unityObject = context as Object;
            if(unityObject is null && !(context is null))
            {
                unityObject = Find.WrapperOf(context) as Object;
            }
            Debug.LogError(message, unityObject);
            #elif DEBUG
            Debug.LogError(message);
            #endif
        }

        [DebuggerStepThrough]
        public void LogException(Exception exception, object context = null)
        {
            #if UNITY_EDITOR
            Object unityObject = context as Object;
            if(unityObject is null && !(context is null))
            {
                unityObject = Find.WrapperOf(context) as Object;
            }
            Debug.LogException(exception, unityObject);
            #elif DEBUG
            Debug.LogException(exception);
            #endif
        }
    }
}