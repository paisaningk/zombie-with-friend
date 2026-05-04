//#define DEBUG_ENABLED

using System;
using System.Reflection;
using System.Diagnostics.CodeAnalysis;
using System.Linq;
using UnityEngine;

namespace Sisus.Init.Internal
{
	public delegate void SetInstanceHandler<in TService>(TService instance);

	internal sealed class GlobalServiceSetter
	{
		private static readonly MethodInfo methodDefinition;
		private static readonly object[] arguments = new object[1];

		private readonly Delegate @delegate;

		static GlobalServiceSetter()
		{
			const BindingFlags flags = BindingFlags.Static | BindingFlags.Public;
			methodDefinition = typeof(Service).GetMethod(nameof(Service.Set), flags);

			if(methodDefinition is null)
			{
				Debug.LogWarning("MethodInfo Service.SetInstance<> not found.");
				methodDefinition = typeof(GlobalServiceSetter).GetMethod(nameof(DoNothing), BindingFlags.NonPublic | BindingFlags.Instance);
			}
			#if DEV_MODE && UNITY_ASSERTIONS
			else
			{
				Debug.Assert(methodDefinition.IsGenericMethodDefinition);
				var genericArguments = methodDefinition.GetGenericArguments();
				Debug.Assert(genericArguments.Length is 1);
				var parameters = methodDefinition.GetParameters();
				Debug.Assert(parameters.Length is 1);
				Debug.Assert(parameters.FirstOrDefault()?.ParameterType == genericArguments.FirstOrDefault());
			}
			#endif
		}

		public GlobalServiceSetter([DisallowNull] Type definingType)
		{
			var method = methodDefinition.MakeGenericMethod(definingType);
			var delegateType = typeof(SetInstanceHandler<>).MakeGenericType(definingType);
			@delegate = Delegate.CreateDelegate(delegateType, method);
		}

		/// <exception cref="TargetInvocationException">
		/// Thrown if an exception is thrown during execution of <see cref="Service.Set{TInstance}"/>.
		/// This can happen if an exception occurs in an event handler listening to the
		/// <see cref="ServiceChanged{TService}.listeners"/> event.
		/// </exception>
		public void SetInstance(object instance)
		{
			#if DEV_MODE
			try
			{
			#endif

			arguments[0] = instance;

			#if DEV_MODE && UNITY_EDITOR && DEBUG_ENABLED
			Debug.Log($"Executing Service.SetInstance<{instance.GetType().Name}> with arguments: {string.Join(", ", arguments.Select(x => TypeUtility.ToString(x?.GetType())))}\n@delegate:{@delegate}");
			#endif

			@delegate.DynamicInvoke(arguments);

			#if DEV_MODE
			}
			catch(Exception e)
			{
				Debug.LogError($"Exception thrown while setting instance of type {instance.GetType().Name}:\n{e}");
				throw;
			}
			#endif
		}

		private void DoNothing<TService>(TService instance) { }
	}
}