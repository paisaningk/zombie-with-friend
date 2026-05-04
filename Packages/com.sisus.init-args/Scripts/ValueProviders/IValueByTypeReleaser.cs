using System;
using System.Diagnostics.CodeAnalysis;
using System.Linq;
using System.Reflection;
using Sisus.Init.Internal;
using UnityEngine;

namespace Sisus.Init
{
	/// <summary>
	/// Represents a value provider (<see cref="IValueByTypeProvider"/> or
	/// <see cref="IValueByTypeProviderAsync"/>) to which clients should return
	/// the value that was provided to them, when they no longer need it
	/// (for example, when the client is destroyed).
	/// <para>
	/// Can be used, for example, to release unmanaged memory, or to return
	/// reusable objects into an object pool.
	/// </para>
	/// </summary>
	public interface IValueByTypeReleaser
	{
		/// <summary>
		/// Returns the value of type <typeparamref name="TValue"/> that was being used by the <paramref name="client"/>.
		/// </summary>
		/// <typeparam name="TValue"> Type of the returned <paramref name="value"/>. </typeparam>
		/// <param name="client">
		/// The object that requested the value for the value provider, or <see langword="null"/> if the requester
		/// was unknown or was not a <see cref="Component"/>.
		/// </param>
		/// <param name="value">
		/// The value of type <typeparamref name="TValue"/> that is being returned.
		/// </param>
		void Release<TValue>([AllowNull] Component client, TValue value);

		/// <summary>
		/// Returns the value of type <paramref name="valueType"/> that was being used by the <paramref name="client"/>.
		/// </summary>
		/// <param name="client">
		/// The object that requested the value for the value provider, or <see langword="null"/> if the requester
		/// was unknown or was not a <see cref="Component"/>.
		/// </param>
		/// <param name="valueType"> Type of the returned <paramref name="value"/>. </param>
		/// <param name="value">
		/// The value of type <paramref name="valueType"/> that is being returned.
		/// </param>
		void Release([AllowNull] Component client, [DisallowNull] Type valueType, object value)
		{
			#if DEV_MODE || INIT_ARGS_SAFE_MODE
			if(!TypeUtility.IsValidGenericTypeArgument(valueType))
			{
				Debug.LogWarning($"{TypeUtility.ToString(GetType())}.Release(client, valueType, value) called with an invalid valueType: {TypeUtility.ToString(valueType, '.')}");
				return;
			}
			#endif

			if(valueType.ContainsGenericParameters)
			{
				var genericArguments = valueType.GetGenericArguments();
				if(genericArguments.Length != 1 || !client || genericArguments[0].GetGenericParameterConstraints().Length > 0)
				{
					return;
				}

				if(genericArguments[0].GetGenericParameterConstraints().Length > 0)
				{
					#if DEV_MODE
					Debug.Log($"{GetType().Name}.Release({TypeUtility.ToString(valueType, '.')}) -> aborting because type contains generic parameters. Could try to construct generic type using client's type as the generic argument type, but that's unsafe due to the generic argument type having constraints, which could result in an exception.");
					#endif
					return;
				}

				valueType = valueType.MakeGenericType(client.GetType());
			}

			twoArguments[0] = client;
			twoArguments[1] = value;
			releaseGeneric.MakeGenericMethod(valueType).Invoke(this, twoArguments);
		}

		private static readonly object[] twoArguments = new object[2];
		private static readonly MethodInfo releaseGeneric = typeof(IValueByTypeProvider).GetMember(nameof(Release)).Select(m => (MethodInfo)m).FirstOrDefault(m => m.IsGenericMethod);
	}
}
