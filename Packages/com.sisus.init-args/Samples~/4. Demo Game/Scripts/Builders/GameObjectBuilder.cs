using System;
using UnityEngine;
using static UnityEngine.Object;

namespace Init.Demo
{
	/// <summary>
	/// Object reponsible for managing the instantiation and destruction
	/// of <see cref="GameObject">GameObjects</see> in play mode.
	/// </summary>
	[Serializable]
	public sealed class GameObjectBuilder : IGameObjectBuilder
	{
		[SerializeField, Tooltip("The prefab which is cloned when creating instances.")]
		private GameObject prefab;

		/// <summary>
		/// Initializes a new instance of the <see cref="GameObjectBuilder"/> class.
		/// </summary>
		/// <param name="prefab">
		/// The prefab which is cloned when <see cref="Create">creating</see> instances.
		/// </param>
		public GameObjectBuilder(GameObject prefab) => this.prefab = prefab;

		/// <inheritdoc/>
		public GameObject Create(Vector3 position) => Instantiate(prefab, position, prefab.transform.rotation);

		/// <inheritdoc/>
		public void Dispose(GameObject instance) => Destroy(instance);
	}
}