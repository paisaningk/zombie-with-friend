using System;
using System.Collections.Generic;
using UnityEngine;
using static UnityEngine.Object;

namespace Init.Demo
{
	/// <summary>
	/// Object reponsible for managing the instantiation and destruction
	/// of <see cref="GameObject">GameObjects</see> in play mode.
	/// </summary>
	[Serializable]
	public sealed class GameObjectPool : IGameObjectBuilder
	{
		[SerializeField, Tooltip("The prefab which is cloned when creating instances.")]
		private GameObject prefab;

		private readonly Stack<GameObject> pool;

		/// <summary>
		/// Initializes a new instance of the <see cref="GameObjectPool"/> class
		/// with an empty initial stack and no <see cref="prefab"/>.
		/// </summary>
		public GameObjectPool()
		{
			prefab = null;
			pool = new Stack<GameObject>();
		}

		/// <summary>
		/// Initializes a new instance of the <see cref="GameObjectPool"/> class.
		/// </summary>
		/// <param name="prefab">
		/// The prefab which is cloned when <see cref="Create">creating</see> instances.
		/// </param>
		/// <param name="defaultCapacity">
		/// The default capacity the stack will be created with.
		/// </param>
		public GameObjectPool(GameObject prefab, int defaultCapacity)
		{
			this.prefab = prefab;

			pool = new Stack<GameObject>(defaultCapacity);

			for(int i = 0; i < defaultCapacity; i++)
			{
				Dispose(Instantiate(prefab));
			}
		}

		/// <inheritdoc/>
		public GameObject Create(Vector3 position)
		{
			if(pool.Count == 0)
			{
				return Instantiate(prefab, position, prefab.transform.rotation);
			}

			var instance = pool.Pop();
			instance.transform.position = position;
			instance.SetActive(true);
			return instance;
		}

		/// <inheritdoc/>
		public void Dispose(GameObject instance)
		{
			instance.SetActive(false);
			pool.Push(instance);
		}
	}
}