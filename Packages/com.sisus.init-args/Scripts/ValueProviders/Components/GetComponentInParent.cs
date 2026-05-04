using System;
using System.Collections.Generic;
using Sisus.Init.Internal;
using UnityEngine;
using static Sisus.Init.ValueProviders.ValueProviderUtility;

namespace Sisus.Init.ValueProviders
{
	/// <summary>
	/// Returns an object of the requested type attached to the client's <see cref="GameObject"/> or any of its parents.
	/// <para>
	/// Can be used to retrieve an Init argument at runtime.
	/// </para>
	/// </summary>
	#if !INIT_ARGS_DISABLE_VALUE_PROVIDER_MENU_ITEMS
	[ValueProviderMenu(MENU_NAME, Is.SceneObject, Order = 1.2f, Tooltip = "Value will be located at runtime from this game object or any of its parents.")]
	#endif
	#if DEV_MODE
	[CreateAssetMenu(fileName = MENU_NAME, menuName = CREATE_ASSET_MENU_GROUP + MENU_NAME)]
	#endif
	internal sealed class GetComponentInParent : ScriptableObject, IValueByTypeProvider
	#if UNITY_EDITOR
	, INullGuardByType
	#endif
	{
		private const string MENU_NAME = "Hierarchy/Get Component In Parent";

		/// <summary>
		/// Gets an object of type <typeparamref name="TValue"/> attached to the <paramref name="client"/> or any its parents.
		/// </summary>
		/// <typeparam name="TValue"> Type of object to find. </typeparam>
		/// <param name="client"> The <see cref="GameObject"/> to search along with its parents. </param>
		/// <param name="value">
		/// When this method returns, contains an object <typeparamref name="TValue"/> if one was found; otherwise, the <see langword="null"/>. This parameter is passed uninitialized.
		/// </param>
		/// <returns>
		/// <see langword="true"/> if an object was found; otherwise, <see langword="false"/>.
		/// </returns>
		public bool TryGetFor<TValue>(Component client, out TValue value)
		{
			if(!client)
			{
				value = default;
				return false;
			}

			return Find.InParents(client.gameObject, out value, client.gameObject.activeInHierarchy);
		}

		bool IValueByTypeProvider.IsValueTypeSupported(Type valueType) => Find.typesToComponentTypes.ContainsKey(valueType);
		IEnumerable<Type> IValueByTypeProvider.GetSupportedValueTypes() => Find.typesToComponentTypes.Keys;
		bool IValueByTypeProvider.HasValueFor<TValue>(Component client) => TryGetFor<TValue>(client, out _);

		#if UNITY_EDITOR
		NullGuardResult INullGuardByType.EvaluateNullGuard<TValue>(Component client)
		{
			if(!client)
			{
				return NullGuardResult.Error($"{nameof(GetComponentInParent)} only supports clients attached to GameObjects.");
			}

			if(!Find.typesToComponentTypes.ContainsKey(typeof(TValue)))
			{
				return NullGuardResult.Error($"{nameof(GetComponentInParent)} can only provide values that are attachable to GameObjects. {TypeUtility.ToString(typeof(TValue))} is not supported.");
			}

			if(!Find.InParents<TValue>(client.gameObject, out _, client.gameObject.activeInHierarchy))
			{
				return NullGuardResult.Error($"No object of type {TypeUtility.ToString(typeof(TValue))} is found attached to the GameObject or its parents.");
			}

			return NullGuardResult.Passed;
		}
		#endif
	}
}