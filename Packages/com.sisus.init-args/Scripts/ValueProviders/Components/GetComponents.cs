using System;
using System.Collections.Generic;
using Sisus.Init.Internal;
using UnityEngine;
using static Sisus.Init.Internal.TypeUtility;
using static Sisus.Init.ValueProviders.ValueProviderUtility;

namespace Sisus.Init.ValueProviders
{
	/// <summary>
	/// Returns all objects of the requested type attached to the client's <see cref="GameObject"/>.
	/// <para>
	/// Can be used to retrieve an Init argument at runtime.
	/// </para>
	/// </summary>
	#if !INIT_ARGS_DISABLE_VALUE_PROVIDER_MENU_ITEMS
	[ValueProviderMenu(MENU_NAME, WhereAll = Is.Collection | Is.SceneObject, Order = 1.1f, Tooltip = "Collection will be created at runtime from the components attached to this game object.")]
	#endif
	#if DEV_MODE
	[CreateAssetMenu(fileName = MENU_NAME, menuName = CREATE_ASSET_MENU_GROUP + MENU_NAME)]
	#endif
	internal sealed class GetComponents : ScriptableObject, IValueByTypeProvider
	#if UNITY_EDITOR
	, INullGuardByType
	#endif
	{
		private const string MENU_NAME = "Hierarchy/Get Components";

		/// <summary>
		/// Gets all objects attached to the <paramref name="client"/> that match the element type of the <typeparamref name="TValue"/> array.
		/// </summary>
		/// <typeparam name="TValue"> Type of result array. </typeparam>
		/// <param name="client"> The <see cref="GameObject"/> to search. </param>
		/// <param name="value">
		/// When this method returns, contains an array of type <typeparamref name="TValue"/> if one or more results were found; otherwise, the <see langword="null"/>. This parameter is passed uninitialized.
		/// </param>
		/// <returns>
		/// <see langword="true"/> if at least one matching object is found on the <see cref="GameObject"/>; otherwise, <see langword="false"/>.
		/// </returns>
		public bool TryGetFor<TValue>(Component client, out TValue value)
		{
			if(!client)
			{
				value = default;
				return false;
			}

			object[] found = Find.AllIn(client.gameObject, GetCollectionElementType(typeof(TValue)));
			value = ConvertToCollection<TValue, object>(found);
			return true;
		}

		bool IValueByTypeProvider.IsValueTypeSupported(Type valueType) => Find.typesToComponentTypes.ContainsKey(GetCollectionElementType(valueType));
		IEnumerable<Type> IValueByTypeProvider.GetSupportedValueTypes() => Find.typesToComponentTypes.Keys;
		bool IValueByTypeProvider.HasValueFor<TValue>(Component client) => client;

		#if UNITY_EDITOR
		NullGuardResult INullGuardByType.EvaluateNullGuard<TValue>(Component client)
		{
			if(!client)
			{
				return NullGuardResult.Error($"{nameof(GetComponents)} only supports clients attached to GameObjects.");
			}

			if(GetCollectionElementType(typeof(TValue)) is not { } elementType)
			{
				return NullGuardResult.Error($"{nameof(GetComponents)} can only provide collection type values. {TypeUtility.ToString(typeof(TValue))} is not supported.");
			}

			if(!Find.typesToComponentTypes.ContainsKey(elementType))
			{
				return NullGuardResult.Error($"{nameof(GetComponents)} can only provide values that are attachable to GameObjects. {TypeUtility.ToString(typeof(TValue))} is not supported.");
			}

			return NullGuardResult.Passed;
		}
		#endif
	}
}