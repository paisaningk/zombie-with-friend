using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Linq;
using Sisus.Init.Internal;
using UnityEngine;
using static Sisus.Init.ValueProviders.ValueProviderUtility;

namespace Sisus.Init.ValueProviders
{
	/// <summary>
	/// Returns an object of the given type that is currently loaded.
	/// <para>
	/// Can be used to retrieve an Init argument at runtime.
	/// </para>
	/// </summary>
	/// <remarks>
	/// Components attached to inactive GameObjects can also be provided,
	/// but only if no active instance of the requested type exists in the loaded scenes.
	/// </remarks>
	#if !INIT_ARGS_DISABLE_VALUE_PROVIDER_MENU_ITEMS
	[ValueProviderMenu(MENU_NAME, Is.SceneObject, Order = 2.1f, Tooltip = "Value will be located at runtime from any of the active scenes.")]
	#endif
	#if DEV_MODE
	[CreateAssetMenu(fileName = MENU_NAME, menuName = CREATE_ASSET_MENU_GROUP + MENU_NAME)]
	#endif
	internal sealed class FindAnyObjectByType : ScriptableObject, IValueByTypeProvider
	#if UNITY_EDITOR
	, INullGuardByType
	#endif
	{
		private const string MENU_NAME = "Hierarchy/Find Any Object By Type";

		/// <summary>
		/// Gets an object of type <typeparamref name="TValue"/> that is currently loaded.
		/// </summary>
		/// <typeparam name="TValue"> Type of object to find. </typeparam>
		/// <param name="client"> This parameter is ignored. </param>
		/// <param name="value">
		/// When this method returns, contains an object <typeparamref name="TValue"/> if one was found; otherwise, the <see langword="null"/>. This parameter is passed uninitialized.
		/// </param>
		/// <returns>
		/// <see langword="true"/> if an object was found; otherwise, <see langword="false"/>.
		/// </returns>
		public bool TryGetFor<TValue>([AllowNull] Component client, out TValue value) => Find.Any(out value, false) || Find.Any(out value, true);

		bool IValueByTypeProvider.IsValueTypeSupported(Type valueType) => Find.typesToFindableTypes.ContainsKey(valueType);
		IEnumerable<Type> IValueByTypeProvider.GetSupportedValueTypes() => Find.typesToFindableTypes.Keys;
		bool IValueByTypeProvider.HasValueFor<TValue>(Component client) => Find.Any<TValue>(out _, true);

		#if UNITY_EDITOR
		NullGuardResult INullGuardByType.EvaluateNullGuard<TValue>(Component client)
		{
			if(!Find.typesToFindableTypes.ContainsKey(typeof(TValue)))
			{
				return NullGuardResult.Error($"{nameof(FindAnyObjectByType)} can only provide values that are attachable to GameObjects or derive from ScriptableObject. {TypeUtility.ToString(typeof(TValue))} is not supported.");
			}

			if(!TryGetFor<TValue>(client, out _))
			{
				if(Application.isPlaying)
				{
					return NullGuardResult.Warning($"No instance of type {TypeUtility.ToString(typeof(TValue))} found in the currently loaded scenes.");
				}

				return NullGuardResult.Warning($"No instance of type {TypeUtility.ToString(typeof(TValue))} found in the currently loaded scenes at this time (but one could become available at runtime).");
			}

			return NullGuardResult.Passed;
		}
		#endif
	}
}