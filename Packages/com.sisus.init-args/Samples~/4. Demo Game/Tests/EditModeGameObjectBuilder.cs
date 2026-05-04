using System;
using UnityEngine;
using static UnityEngine.Object;

namespace Init.Demo
{
    /// <summary>
    /// Object reponsible for managing the instantiation and destruction
    /// of <see cref="GameObject">GameObjects</see> in edit mode.
    /// </summary>
    [Serializable]
    public sealed class EditModeGameObjectBuilder : IGameObjectBuilder
    {
        [SerializeField, Tooltip("The prefab which is cloned when creating instances.")]
        private GameObject prefab;

        /// <summary>
        /// Initializes a new instance of the <see cref="EditModeGameObjectBuilder"/> class.
        /// </summary>
        /// <param name="prefab">
        /// The prefab which is cloned when <see cref="Create">creating</see> instances.
        /// </param>
        public EditModeGameObjectBuilder(GameObject prefab)
        {
            this.prefab = prefab;
        }

        /// <inheritdoc/>
        public GameObject Create(Vector3 position)
        {
            return Instantiate(prefab, position, prefab.transform.rotation);
        }

        /// <inheritdoc/>
        public void Dispose(GameObject instance)
        {
            DestroyImmediate(instance);
        }
    }
}