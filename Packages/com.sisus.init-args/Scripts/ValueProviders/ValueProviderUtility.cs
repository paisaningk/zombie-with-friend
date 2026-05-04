#pragma warning disable CS8524
#pragma warning disable CS8509

using System;
using System.Diagnostics.CodeAnalysis;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Threading.Tasks;
using Sisus.Init.Internal;
using UnityEngine;

namespace Sisus.Init.ValueProviders
{
	internal static class ValueProviderUtility
	{
		internal const string CREATE_ASSET_MENU_GROUP = "Value Providers/";

		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static bool IsValueProvider(Type type)
			=> typeof(IValueProvider).IsAssignableFrom(type)
			|| typeof(IValueByTypeProvider).IsAssignableFrom(type)
			|| typeof(IValueProviderAsync).IsAssignableFrom(type)
			|| typeof(IValueByTypeProviderAsync).IsAssignableFrom(type);

		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static bool IsValueProvider([AllowNull] object obj) => obj is IValueProvider or IValueByTypeProvider or IValueByTypeProviderAsync or IValueProviderAsync;

		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static bool IsAsyncValueProvider([AllowNull] object obj) => obj is IValueProviderAsync or IValueByTypeProviderAsync;

		public static bool TryGetValueProviderValue([AllowNull] object potentialValueProvider, [DisallowNull] Type valueType, Component client, [NotNullWhen(true), MaybeNullWhen(false)] out object valueOrAwaitableToGetValue)
		{
			if(potentialValueProvider is IValueProvider valueProvider)
			{
				// NOTE: Always use IValueProvider<T>.Value if available instead of IValueProvider.Value, because it is
				// possible for an object to implement multiple different IValueProvider<T> interfaces!
				var genericValueProviderType = typeof(IValueProvider<>).MakeGenericType(valueType);
				if(genericValueProviderType.IsInstanceOfType(potentialValueProvider))
				{
					object[] args = { client, null };
					var result = (bool)genericValueProviderType.GetMethod(nameof(IValueProvider<object>.TryGetFor)).Invoke(valueProvider, args);
					valueOrAwaitableToGetValue = args[1];
					return result;
				}

				// Prefer IValueByTypeProvider over IValueProvider, because an object could implement both interfaces,
				// and support retrieving more than one type of service using IValueByTypeProvider, but only one default
				// service using IValueProvider.
				if(potentialValueProvider is IValueByTypeProvider valueByTypeProvider)
				{
					return valueByTypeProvider.TryGetFor(client, out valueOrAwaitableToGetValue);
				}

				return valueProvider.TryGetFor(client, out valueOrAwaitableToGetValue);
			}
			else if(potentialValueProvider is IValueByTypeProvider valueByTypeProvider)
			{
				return valueByTypeProvider.TryGetFor(client, valueType, out valueOrAwaitableToGetValue);
			}

			if(potentialValueProvider is IValueByTypeProviderAsync valueByTypeProviderAsync)
			{
				valueOrAwaitableToGetValue = valueByTypeProviderAsync.GetForAsync(valueType, client);
				return valueOrAwaitableToGetValue is not null;
			}

			if(potentialValueProvider is IValueProviderAsync valueProviderAsync)
			{
				valueOrAwaitableToGetValue = valueProviderAsync.GetForAsync(client);
				return valueOrAwaitableToGetValue is not null;
			}

			valueOrAwaitableToGetValue = null;
			return false;
		}

		public static
		#if UNITY_2023_1_OR_NEWER
		Awaitable<object>
		#else
		System.Threading.Tasks.Task<object>
		#endif
		GetValueProviderValueAsync([AllowNull] object potentialValueProvider, [DisallowNull] Type valueType, Component client)
		{
			if(potentialValueProvider is IValueByTypeProviderAsync valueByTypeProviderAsync)
			{
				return valueByTypeProviderAsync.GetForAsync(valueType, potentialValueProvider as Component);
			}

			if(potentialValueProvider is IValueProviderAsync valueProviderAsync)
			{
				var genericValueProviderAsyncType = typeof(IValueProviderAsync<>).MakeGenericType(valueType);
				if(genericValueProviderAsyncType.IsInstanceOfType(valueProviderAsync))
				{
					object[] args = { client};
					var genericAwaitable = genericValueProviderAsyncType.GetMethod(nameof(IValueProviderAsync<object>.GetForAsync)).Invoke(valueProviderAsync, args);
					return GetResultAsync(genericAwaitable);
				}

				return valueProviderAsync.GetForAsync(client);
			}

			if(TryGetValueProviderValueSync(potentialValueProvider, valueType, client, out var value))
			{
				return
					#if UNITY_2023_1_OR_NEWER
					AwaitableUtility
					#else
					System.Threading.Tasks.Task
					#endif
					.FromResult(value);
			}

			return
				#if UNITY_2023_1_OR_NEWER
				AwaitableUtility
				#else
				System.Threading.Tasks.Task
				#endif
				.FromResult<object>(null);
		}

		public static bool TryGetValueProviderValueSync([AllowNull] object potentialValueProvider, [DisallowNull] Type valueType, Component client, [NotNullWhen(true), MaybeNullWhen(false)] out object value)
		{
			if(potentialValueProvider is IValueByTypeProvider)
			{
				object[] args = { client, null };
				if((bool)typeof(IValueByTypeProvider).GetMethod(nameof(IValueByTypeProvider.TryGetFor)).MakeGenericMethod(valueType).Invoke(potentialValueProvider, args))
				{
					value = args[1];
					return value is not null;
				}
			}

			if(potentialValueProvider is IValueProvider valueProvider)
			{
				var genericValueProviderType = typeof(IValueProvider<>).MakeGenericType(valueType);
				if(genericValueProviderType.IsInstanceOfType(valueProvider))
				{
					object[] args = { client, null };
					if((bool)genericValueProviderType.GetMethod(nameof(IValueProvider<object>.TryGetFor)).Invoke(valueProvider, args))
					{
						value = args[1];
						return value is not null;
					}
				}

				return valueProvider.TryGetFor(client, out value);
			}

			value = null;
			return false;
		}

		public static bool TryGetValueProviderValue<TValue>([AllowNull] object potentialValueProvider, [NotNullWhen(true), MaybeNullWhen(false)] out TValue value) => potentialValueProvider switch
		{
			// Prefer non-async value provider interfaces over async ones
			IValueProvider<TValue> valueProvider => valueProvider.TryGetFor(null, out value),
			IValueByTypeProvider valueProvider => valueProvider.TryGetFor(null, out value),
			IValueProvider valueProvider when valueProvider.TryGetFor(null, out var objectValue) && Find.In(objectValue, out value) => true,
			IValueProviderAsync<TValue> valueProvider => TryGetFromAwaitableIfCompleted(valueProvider.GetForAsync(null), out value),
			IValueByTypeProviderAsync valueProvider => TryGetFromAwaitableIfCompleted(valueProvider.GetForAsync<TValue>(null), out value),
			_ => None(out value)
		};

		/// <summary>
		/// Gets a value indicating whether the object can potentially provide a value of the specified type, based on the value provider interfaces that it implements.
		/// </summary>
		/// <param name="potentialValueProvider"> The object to check. </param>
		/// <param name="valueType"> The type of value to check for. </param>
		/// <returns> <see langword="true"/> if the object can provide a value of the specified type; otherwise <see langword="false"/>. </returns>
		public static bool IsValueTypeSupported([AllowNull] object potentialValueProvider, [DisallowNull] Type valueType)
		{
			#if DEV_MODE
			Debug.Assert(potentialValueProvider is not Type);
			Debug.Assert(potentialValueProvider is not Task);
			Debug.Assert(potentialValueProvider is not Awaitable<object>);
			#endif

			Type[] interfaces = null;
			if(potentialValueProvider is IValueProvider)
			{
				interfaces = potentialValueProvider.GetType().GetInterfaces();
				foreach(var interfaceType in interfaces)
				{
					if(interfaceType.IsGenericType
						&& interfaceType.GetGenericTypeDefinition() == typeof(IValueProvider<>)
						&& valueType == interfaceType.GetGenericArguments()[0])
					{
						return true;
					}
				}
			}

			if(potentialValueProvider is IValueProviderAsync)
			{
				interfaces ??= potentialValueProvider.GetType().GetInterfaces();
				foreach(var interfaceType in interfaces)
				{
					if(interfaceType.IsGenericType
						&& interfaceType.GetGenericTypeDefinition() == typeof(IValueProviderAsync<>)
						&& valueType == interfaceType.GetGenericArguments()[0])
					{
						return true;
					}
				}
			}

			if(potentialValueProvider is IValueByTypeProvider valueByTypeProvider)
			{
				return valueByTypeProvider.IsValueTypeSupported(valueType);
			}

			if(potentialValueProvider is IValueByTypeProviderAsync valueByTypeProviderAsync)
			{
				return valueByTypeProviderAsync.IsValueTypeSupported(valueType);
			}

			return false;
		}

		public static TValue GetFromAwaitableIfCompleted<TValue>
		(
			#if UNITY_2023_1_OR_NEWER
			Awaitable<TValue>
			#else
			System.Threading.Tasks.Task<TValue>
			#endif
			awaitable
		)
		{
			#if UNITY_2023_1_OR_NEWER
			var awaiter = awaitable.GetAwaiter();
			return awaiter.IsCompleted ? awaiter.GetResult() : default;
			#else
			return awaitable.IsCompletedSuccessfully ? awaitable.Result : default;
			#endif
		}

		internal static TValue GetFromAwaitableIfCompleted<TValue>
		(
			#if UNITY_2023_1_OR_NEWER
			Awaitable<object>
			#else
			System.Threading.Tasks.Task<object>
			#endif
			awaitable
		)
		{
			#if UNITY_2023_1_OR_NEWER
			var awaiter = awaitable.GetAwaiter();
			return awaiter.IsCompleted ? (TValue)awaiter.GetResult() : default;
			#else
			return awaitable.IsCompletedSuccessfully ? (TValue)awaitable.Result : default;
			#endif
		}
		
		internal static bool TryGetFromAwaitableIfCompleted<TValue>
		(
			#if UNITY_2023_1_OR_NEWER
			Awaitable<TValue>
			#else
			System.Threading.Tasks.Task<TValue>
			#endif
			awaitable, out TValue result
		)
		{
			#if UNITY_2023_1_OR_NEWER
			var awaiter = awaitable.GetAwaiter();
			if(awaiter.IsCompleted && awaiter.GetResult()
			#else
			if(awaitable.IsCompletedSuccessfully && awaitable.Result
			#endif
				is TValue service)
			{
				result = service;
				return true;
			}

			result = default;
			return false;
		}
		
		internal static bool TryGetFromAwaitableIfCompleted<TValue>
		(
			#if UNITY_2023_1_OR_NEWER
			Awaitable<object>
			#else
			System.Threading.Tasks.Task<object>
			#endif
			awaitable, out TValue result
		)
		{
			#if UNITY_2023_1_OR_NEWER
			var awaiter = awaitable.GetAwaiter();
			if(awaiter.IsCompleted && awaiter.GetResult()
			#else
			if(awaitable.IsCompletedSuccessfully && awaitable.Result
			#endif
				is TValue service)
			{
				result = service;
				return true;
			}

			result = default;
			return false;
		}

		#if UNITY_EDITOR
		/// <summary>
		/// NOTE: Slow method; should not be called during every OnGUI.
		/// </summary>
		internal static bool TryGetSingleSharedInstanceSlow(Type valueProviderType, out ScriptableObject singleSharedInstance)
		{
			var guidsOfPotentialInstancesInProject = UnityEditor.AssetDatabase.FindAssets("t:" + valueProviderType.Name);
			if(guidsOfPotentialInstancesInProject.Length == 0)
			{
				#if DEV_MODE
				Debug.Log($"No instances of value provider {valueProviderType} found in the project.");
				#endif

				singleSharedInstance = null;
				return false;
			}

			singleSharedInstance = guidsOfPotentialInstancesInProject
				.Select(guid => UnityEditor.AssetDatabase.LoadAssetAtPath<ScriptableObject>(UnityEditor.AssetDatabase.GUIDToAssetPath(guid)))
				.SingleOrDefault(asset => asset && asset.GetType() == valueProviderType);

			if(!singleSharedInstance)
			{
				#if DEV_MODE
				Debug.Log($"No single shared instance of value provider {valueProviderType} found in the project. Found {guidsOfPotentialInstancesInProject.Length} instances.");
				#endif
				return false;
			}

			if(UnityEditor.AssetDatabase.IsSubAsset(singleSharedInstance))
			{
				return false;
			}

			using(var serializedObject = new UnityEditor.SerializedObject(singleSharedInstance))
			{
				var firstProperty = serializedObject.GetIterator();
				if(firstProperty.NextVisible(true) && !firstProperty.NextVisible(false))
				{
					#if DEV_MODE
					Debug.Log($"Single shared instance found: {singleSharedInstance.name}.", singleSharedInstance);
					#endif
					return true;
				}
			}

			#if DEV_MODE
			Debug.Log($"Single shared instance disqualified due to having serialized fields: {singleSharedInstance.name}.", singleSharedInstance);
			#endif

			singleSharedInstance = null;
			return false;
		}
		#endif

		/// <summary>
		/// NOTE: This should never be called twice for the same awaitable object!
		/// </summary>
		/// <param name="awaitable"> <see cref="Awaitable{object}"/> or <see cref="Task{object}"/> await and extract service from. </param>
		/// <returns> <see cref="Task{object}"/> that returns the result of the awaitable, if possible, or otherwise returns null. </returns>
		private static
		#if UNITY_2023_1_OR_NEWER
		Awaitable<object>
		#else
		System.Threading.Tasks.Task<object>
		#endif
		GetResultAsync(object awaitable)
		{
			#if !UNITY_2023_1_OR_NEWER
			if(awaitable is Task task)
			{
				return task.GetResult();
			}
			
			return Task.FromResult(awaitable);
			#else
			if(awaitable.GetType().GetMethod(nameof(Awaitable<object>.GetAwaiter)) is not { } getAwaiterMethod)
			{
				return AwaitableUtility.FromResult<object>(null);
			}

			INotifyCompletion awaiter = (INotifyCompletion)getAwaiterMethod.Invoke(awaitable, null);
			var completionSource = new AwaitableCompletionSource<object>();
			awaiter.OnCompleted(() =>
			{
				if(awaiter.GetType().GetMethod(nameof(Awaitable<object>.Awaiter.GetResult)) is not { } getResultMethod)
				{
					completionSource.SetResult(null);
					return;
				}
				
				try
				{
					completionSource.SetResult(getResultMethod.Invoke(awaiter, null));
				}
				catch(Exception exception)
				{
					completionSource.SetException(exception);
				}
			});

			return completionSource.Awaitable;
			#endif
		}

		private static bool None<TValue>(out TValue result)
		{
			result = default;
			return false;
		}
	}
}