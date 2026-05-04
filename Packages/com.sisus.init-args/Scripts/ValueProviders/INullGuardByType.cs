using System;
using System.Diagnostics.CodeAnalysis;
using System.Reflection;
using UnityEngine;
using Component = UnityEngine.Component;

namespace Sisus.Init
{
	/// <summary>
	/// Represents an object that is responsible for providing an initialization argument, and can be
	/// validated by an initializer to verify that it will be able to fulfill that responsibility at runtime.
	/// </summary>
	public interface INullGuardByType
	{
		/// <summary>
		/// Gets a value indicating whether null guard passes for this object or not, and if not,
		/// what was the cause of the failure.
		/// </summary>
		/// <typeparam name="TValue"> Type of the value whose availability should be checked. </typeparam>
		/// <param name="client">
		/// The component performing the evaluation, if being performed by a component; otherwise, <see langword="null"/>.
		/// </param>
		/// <returns>
		/// Value representing the result of the null guard.
		/// </returns>
		NullGuardResult EvaluateNullGuard<TValue>([AllowNull] Component client);

		NullGuardResult EvaluateNullGuard([DisallowNull] Type valueType, [AllowNull] Component client)
		{
			#if DEV_MODE || INIT_ARGS_SAFE_MODE
			if(!Internal.TypeUtility.IsValidGenericTypeArgument(valueType))
			{
				Debug.LogWarning($"{Internal.TypeUtility.ToString(GetType())}.EvaluateNullGuard(valueType, client) called with an invalid valueType: {Internal.TypeUtility.ToString(valueType, '.')}");
				return NullGuardResult.Error($"Value type {Internal.TypeUtility.ToString(valueType)} is not supported.");
			}
			#endif
			
			if(valueType.ContainsGenericParameters)
			{
				var genericArguments = valueType.GetGenericArguments();
				if(genericArguments.Length != 1)
				{
					return NullGuardResult.Error($"Requested value {Internal.TypeUtility.ToString(valueType)} contains open generic type parameters. Can not resolve concrete type.");
				}

				if(!client)
				{
					return NullGuardResult.Error($"{Internal.TypeUtility.ToString(valueType)}) contains an open generic type parameter, and can not resolve it using the client's type, because the client is null.");
				}
				
				if(genericArguments[0].GetGenericParameterConstraints().Length > 0)
				{
					return NullGuardResult.Error($"{Internal.TypeUtility.ToString(valueType)}) contains an open generic type parameter, and can not safely resolve it using the client's type, because the generic argument type has constraints.");
				}

				try
				{
					return (NullGuardResult)canProvideValueGeneric.MakeGenericMethod(valueType.MakeGenericType(client.GetType())).Invoke(this, GetArguments(client));
				}
				catch(Exception e)
				{
					return NullGuardResult.Exception(e);
				}
			}

			try
			{
				return (NullGuardResult)canProvideValueGeneric.MakeGenericMethod(valueType).Invoke(this, GetArguments(client));
			}
			catch(Exception e)
			{
				return NullGuardResult.Exception(e);
			}

			static object[] GetArguments(object client)
			{
				arguments[0] = client;
				return arguments;
			}
		}

		private static readonly object[] arguments = new object[1];
		private static readonly MethodInfo canProvideValueGeneric = typeof(INullGuardByType).GetMethod("EvaluateNullGuard", new[] { typeof(Component) });
	}
}