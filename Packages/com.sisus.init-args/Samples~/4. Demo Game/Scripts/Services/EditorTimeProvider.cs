#if UNITY_EDITOR
using System;
using Sisus.Init;
using UnityEditor;
using UnityEngine;

namespace Init.Demo
{
    /// <summary>
    /// Class responsible for providing information about the current time in edit mode.
    /// </summary>
    [EditorService(typeof(ITimeProvider))]
    public sealed class EditorTimeProvider : ITimeProvider
    { 
        /// <inheritdoc/>
        public float Time => (float)EditorApplication.timeSinceStartup;

        /// <inheritdoc/>
        public float DeltaTime => UnityEngine.Time.deltaTime;

        /// <inheritdoc/>
        public float RealtimeSinceStartup => (float)EditorApplication.timeSinceStartup;

        /// <inheritdoc/>
        public DateTime Now => DateTime.UtcNow;

        /// <inheritdoc/>
        public object WaitForSeconds(float seconds)
        {
            double stopTime = EditorApplication.timeSinceStartup + seconds;
            bool Predicate()
            {
                return EditorApplication.timeSinceStartup >= stopTime;
            }
            return new WaitUntil(Predicate);
        }
    }
}
#endif