//#define DEBUG_CAN_PROVIDE_VALUE

using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics.CodeAnalysis;
using System.Linq;
using System.Reflection;
using Sisus.Init.Internal;
using UnityEngine;
using UnityEngine.Scripting;
using Component = UnityEngine.Component;

namespace Sisus.Init
{
	/// <summary>
	/// Represents an object that can provide a value of a requested type to a <see cref="GameObject"/> client.
	/// <para>
	/// If a class derives from <see cref="Object"/> and implements <see cref="IValueByTypeProviderAsync"/> then
	/// <see cref="Any{T}"/> can wrap an instance of this class and return its value when <see cref="Any{T}.GetValue{TClient}"/> is called.
	/// </para>
	/// </summary>
	/// <seealso cref="IValueProvider{TValue}"/>
	/// <seealso cref="IValueByTypeProvider"/>
	/// <seealso cref="IValueByTypeProviderAsync"/>
	[RequireImplementors]
	public interface IValueByTypeProvider
	{
		/// <summary>
		/// Gets the value of type <typeparamref name="TValue"/> for the <paramref name="client"/>.
		/// </summary>
		/// <typeparam name="TValue"> Type of the requested <paramref name="value"/>. </typeparam>
		/// <param name="client">
		/// The component requesting the value, if request is coming from a component; otherwise, <see langword="null"/>.
		/// </param>
		/// <param name="value">
		/// When this method returns, contains the value of type <typeparamref name="TValue"/>, if available; otherwise, the default value of <typeparamref name="TValue"/>.
		/// This parameter is passed uninitialized.
		/// </param>
		/// <see langword="true"/> if was able to retrieve the value; otherwise, <see langword="false"/>.
		bool TryGetFor<TValue>([AllowNull] Component client, [NotNullWhen(true), MaybeNullWhen(false)] out TValue value);

		/// <summary>
		/// Gets the value of the given type for the <paramref name="client"/>.
		/// </summary>
		/// <param name="client">
		/// The component requesting the value, if request is coming from a component; otherwise, <see langword="null"/>.
		/// </param>
		/// <param name="valueType"> Type of the requested value. </param>
		/// <param name="value">
		/// When this method returns, contains the value of type <see paramref="valueType"/>, if available;
		/// otherwise, <see langword="null"/>.
		/// This parameter is passed uninitialized.
		/// </param>
		/// <see langword="true"/> if was able to retrieve the value; otherwise, <see langword="false"/>.
		bool TryGetFor([AllowNull] Component client, [DisallowNull] Type valueType, out object value)
		{
			#if DEV_MODE || INIT_ARGS_SAFE_MODE
			if(!TypeUtility.IsValidGenericTypeArgument(valueType))
			{
				Debug.LogWarning($"{TypeUtility.ToString(GetType())}.TryGetFor(client, valueType, out value) called with an invalid valueType: {TypeUtility.ToString(valueType, '.')}");
				value = null;
				return false;
			}
			#endif

			if(valueType.ContainsGenericParameters)
			{
				var genericArguments = valueType.GetGenericArguments();
				if(genericArguments.Length != 1 || !client || genericArguments[0].GetGenericParameterConstraints().Length > 0)
				{
					value = null;
					return false;
				}

				if(genericArguments[0].GetGenericParameterConstraints().Length > 0)
				{
					#if DEV_MODE
					Debug.Log($"{GetType().Name}.TryGetFor({TypeUtility.ToString(valueType, '.')}) -> returning false because type contains generic parameters. Could try to construct generic type using client's type as the generic argument type, but that's unsafe due to the generic argument type having constraints, which could result in an exception.");
					#endif

					value = null;
					return false;
				}

				valueType = valueType.MakeGenericType(client.GetType());
			}

			twoArguments[0] = client;
			if((bool)tryGetForGeneric.MakeGenericMethod(valueType).Invoke(this, twoArguments))
			{
				value = twoArguments[1];
				twoArguments[0] = null;
				twoArguments[1] = null;
				return true;
			}

			value = null;
			twoArguments[0] = null;
			return false;
		}

		/// <summary>
		/// Gets a value indicating whether this value provider can provide a value of type
		/// <typeparamref name="TValue"/> for the <paramref name="client"/> at this time.
		/// </summary>
		/// <param name="client">
		/// The component requesting the value, if request is coming from a component; otherwise, <see langword="null"/>.
		/// </param>
		/// <returns>
		/// <see langword="true"/> if can provide a value for the client at this time; otherwise, <see langword="false"/>.
		/// </returns>
		bool HasValueFor<TValue>(Component client) => this is not INullGuard nullGuard || nullGuard.EvaluateNullGuard(client).Type is NullGuardResultType.Passed;

		/// <summary>
		/// Gets a value indicating whether this value provider supports providing a value of the given type.
		/// <para>
		/// Used by the Inspector to determine if the value provider can be assigned to an Init argument field.
		/// </para>
		/// </summary>
		/// <param name="valueType"> Type of the value that would be provided. </param>
		/// <returns>
		/// <see langword="true"/> if this supports providing a value of the given type; otherwise, <see langword="false"/>.
		/// </returns>
		bool IsValueTypeSupported([DisallowNull] Type valueType)
		{
			#pragma warning disable CS0618
			return CanProvideValue(null, valueType);
			#pragma warning restore CS0618
		}

		/// <summary>
		/// Gets all value types that this value provider supports providing.
		/// <para>
		/// Used by the Inspector to populate dropdown list of compatible types.
		/// </para>
		/// </summary>
		/// <returns></returns>
		IEnumerable<Type> GetSupportedValueTypes()
			=> TypeUtility.GetAllTypesVisibleTo(GetType())
				.Where(IsValueTypeSupported)
				.OrderBy(x => x.IsInterface)
				.ThenBy(x => x.Name, StringComparer.Ordinal);

		[EditorBrowsable(EditorBrowsableState.Never)]
		[Obsolete("Use " + nameof(IsValueTypeSupported)+ " instead.")]
		bool CanProvideValue<TValue>([AllowNull] Component client) => true;

		[EditorBrowsable(EditorBrowsableState.Never)]
		[Obsolete("Use " + nameof(IsValueTypeSupported)+ " instead.")]
		bool CanProvideValue([AllowNull] Component client, [DisallowNull] Type valueType)
		{
			#if DEV_MODE || INIT_ARGS_SAFE_MODE
			if(!TypeUtility.IsValidGenericTypeArgument(valueType))
			{
				Debug.LogWarning($"{TypeUtility.ToString(GetType())}.CanProvideValue(client, valueType) called with an invalid valueType: {TypeUtility.ToString(valueType, '.')}.");
				return false;
			}
			#endif

			if(valueType.ContainsGenericParameters)
			{
				return false;
			}

			return (bool)canProvideValueGeneric.MakeGenericMethod(valueType).Invoke(this, new object[] { client });
		}

		private static readonly object[] twoArguments = new object[2];
		private static readonly MethodInfo canProvideValueGeneric = typeof(IValueByTypeProvider).GetMember(nameof(CanProvideValue)).Select(m => (MethodInfo)m).FirstOrDefault(m => m.IsGenericMethod);
		private static readonly MethodInfo tryGetForGeneric = typeof(IValueByTypeProvider).GetMember(nameof(TryGetFor)).Select(m => (MethodInfo)m).FirstOrDefault(m => m.IsGenericMethod);
	}
}