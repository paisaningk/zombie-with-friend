//#define DEBUG_CAN_PROVIDE_VALUE

using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Linq;
using System.Reflection;
using System.Threading;
using Sisus.Init.Internal;
using UnityEngine;
using UnityEngine.Scripting;

namespace Sisus.Init
{
	/// <summary>
	/// Represents an object that can asynchronously retrieve a value of a given type for a client <see cref="Component"/>.
	/// <para>
	/// If a class derives from <see cref="UnityEngine.Object"/> and implements <see cref="IValueByTypeProviderAsync"/> then
	/// <see cref="Any{T}"/> can wrap an instance of this class and return its value when <see cref="Any{T}.GetValueAsync{TClient}"/> is called.
	/// </para>
	/// </summary>
	/// <seealso cref="IValueProvider{TValue}"/>
	/// <seealso cref="IValueProviderAsync{TValue}"/>
	/// <seealso cref="IValueByTypeProvider"/>
	[RequireImplementors]
	public interface IValueByTypeProviderAsync
	{
		private static readonly MethodInfo getForAsyncGenericMethod = typeof(IValueByTypeProviderAsync).GetMethods().First(m => m.IsGenericMethod && m.Name == nameof(GetForAsync));

		/// <summary>
		/// Asynchronously retrieves a value of type <typeparamref name="TValue"/> for the <paramref name="client"/>.
		/// </summary>
		/// <typeparam name="TValue"> Type of the requested value. </typeparam>
		/// <param name="client">
		/// The component requesting the value, if request is coming from a component; otherwise, <see langword="null"/>.
		/// </param>
		/// <param name="cancellationToken">
		/// Token that can be used to cancel the asynchronous operation (e.g. if the client gets destroyed while waiting for the value).
		/// </param>
		/// <returns>
		/// <see cref="Awaitable{TValue}"/> that can be <see langword="await">awaited</see> to get
		/// the value of type <typeparamref name="TValue"/>, if available; otherwise, a completed awaitable with the result of <see langword="default"/>.
		/// </returns>
		#if UNITY_2023_1_OR_NEWER
		Awaitable<TValue>
		#else
		System.Threading.Tasks.Task<TValue>
		#endif
		GetForAsync<TValue>([AllowNull] Component client, CancellationToken cancellationToken = default);

		/// <summary>
		/// Asynchronously retrieves a value of type <paramref name="valueType"/> for the <paramref name="client"/>.
		/// </summary>
		/// <param name="valueType"> Type of the requested value. </param>
		/// <param name="client">
		/// The component requesting the value, if request is coming from a component; otherwise, <see langword="null"/>.
		/// </param>
		/// <param name="cancellationToken">
		/// Token that can be used to cancel the asynchronous operation (e.g. if the client gets destroyed while waiting for the value).
		/// </param> 
		/// <returns>
		/// An object that can be <see langword="await">awaited</see> to get the value, if available;
		/// otherwise, a completed awaitable with the result of <see langword="default"/>.
		/// </returns>
		/// <returns></returns>
		#if UNITY_2023_1_OR_NEWER
		Awaitable<object>
		#else
		System.Threading.Tasks.Task<object>
		#endif
		GetForAsync([DisallowNull] Type valueType, [AllowNull] Component client, CancellationToken cancellationToken = default)
		{
			#if DEV_MODE || INIT_ARGS_SAFE_MODE
			if(!TypeUtility.IsValidGenericTypeArgument(valueType))
			{
				Debug.LogWarning($"{TypeUtility.ToString(GetType())}.GetForAsync(valueType, client) called with an invalid valueType: {TypeUtility.ToString(valueType, '.')}");
				return null;
			}
			#endif
			
			return
			#if UNITY_2023_1_OR_NEWER
			(Awaitable<object>)
			#else
			(System.Threading.Tasks.Task<object>)
			#endif
			getForAsyncGenericMethod.MakeGenericMethod(valueType).Invoke(this, new object[] { client, cancellationToken });
		}

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
			#if DEV_MODE || INIT_ARGS_SAFE_MODE
			if(!TypeUtility.IsValidGenericTypeArgument(valueType))
			{
				Debug.LogWarning($"{TypeUtility.ToString(GetType())}.IsValueTypeSupported(valueType, client) called with an invalid valueType: {TypeUtility.ToString(valueType, '.')}");
				return false;
			}
			#endif

			if(valueType.ContainsGenericParameters)
			{
				return false;
			}

			return (bool)isValueTypeSupportedGeneric.MakeGenericMethod(valueType).Invoke(this, Array.Empty<object>());
		}

		IEnumerable<Type> GetSupportedValueTypes()
			=> TypeUtility.GetAllTypesVisibleTo(GetType())
				.Where(IsValueTypeSupported)
				.OrderBy(x => x.IsInterface)
				.ThenBy(x => x.Name, StringComparer.Ordinal);

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
		/// Gets a value indicating whether this value provider can potentially provide
		/// a value of the given type to the client at runtime.
		/// <para>
		/// Used by the Inspector to determine if the value provider can be assigned to an Init argument field.
		/// </para>
		/// </summary>
		/// <typeparam name="TValue"> Type of the value that would be provided. </typeparam>
		/// <param name="client"> The client component that would receive the value. </param>
		/// <returns>
		/// <see langword="true"/> if can potentially provide a value of the given type to the client
		/// at runtime; otherwise, <see langword="false"/>.
		/// </returns>
		[Obsolete("Use " + nameof(IsValueTypeSupported)+ " instead.")]
		bool CanProvideValue<TValue>([AllowNull] Component client) => IsValueTypeSupported(typeof(TValue));

		/// <summary>
		/// Gets a value indicating whether this value provider can potentially provide
		/// a value of the given type to the client at runtime.
		/// <para>
		/// Used by the Inspector to determine if the value provider can be assigned to an Init argument field.
		/// </para>
		/// </summary>
		/// <param name="client"> The client component that would receive the value. </param>
		/// <param name="valueType"> Type of the value that would be provided. </param>
		/// <returns>
		/// <see langword="true"/> if can potentially provide a value of the given type to the client
		/// at runtime; otherwise, <see langword="false"/>.
		/// </returns>
		[Obsolete("Use " + nameof(IsValueTypeSupported)+ " instead.")]
		bool CanProvideValue([AllowNull] Component client, [DisallowNull] Type valueType) => IsValueTypeSupported(valueType);

		private static readonly MethodInfo isValueTypeSupportedGeneric = typeof(IValueByTypeProviderAsync).GetMember(nameof(IsValueTypeSupported)).Select(m => (MethodInfo)m).FirstOrDefault(m => m.IsGenericMethod);
	}
}