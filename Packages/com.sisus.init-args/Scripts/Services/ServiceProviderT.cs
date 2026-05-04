using System;
using System.Diagnostics.CodeAnalysis;
using Sisus.Init.Internal;
using Sisus.Init.ValueProviders;
using UnityEngine;

namespace Sisus.Init
{
	/// <summary>
	/// Encapsulates all the different value provider types that can be used to provide a service of type <see paramref="TService"/>.
	/// </summary>
	/// <typeparam name="TService"> Type of the service provided. </typeparam>
	internal sealed class ServiceProvider<TService> : IEquatable<ServiceProvider<TService>>
	{
		public ServiceProviderType Type { get; }
		private readonly object reference;
		private IValueProvider<TService> valueProviderT => Type is ServiceProviderType.IValueProviderT ? (IValueProvider<TService>)reference : null;
		private IValueProviderAsync<TService> valueProviderAsyncT => Type is ServiceProviderType.IValueProviderAsyncT ? (IValueProviderAsync<TService>)reference : null;
		private IValueByTypeProvider valueByTypeProvider => Type is ServiceProviderType.IValueByTypeProvider ? (IValueByTypeProvider)reference : null;
		private IValueByTypeProviderAsync valueByTypeProviderAsync => Type is ServiceProviderType.IValueByTypeProviderAsync ? (IValueByTypeProviderAsync)reference : null;
		private IValueProviderAsync valueProviderAsync => Type is ServiceProviderType.IValueProviderAsync ? (IValueProviderAsync)reference : null;
		private IValueProvider valueProvider => Type is ServiceProviderType.IValueProvider ? (IValueProvider)reference : null;

		internal ServiceProvider(object provider)
		{
			reference = provider;
			Type = provider switch
			{
				IValueProvider<TService> => ServiceProviderType.IValueProviderT,
				IValueByTypeProvider => ServiceProviderType.IValueByTypeProvider,
				IValueProviderAsync<TService> => ServiceProviderType.IValueProviderAsyncT,
				IValueByTypeProviderAsync => ServiceProviderType.IValueByTypeProviderAsync,
				IValueProvider => ServiceProviderType.IValueProvider,
				IValueProviderAsync => ServiceProviderType.IValueProviderAsync,
				_ => ServiceProviderType.None
			};
		}

		internal ServiceProvider(IValueProvider<TService> provider)
		{
			Type = ServiceProviderType.IValueProviderT;
			reference = provider;
		}

		internal ServiceProvider(IValueProviderAsync<TService> provider)
		{
			Type = ServiceProviderType.IValueProviderAsyncT;
			reference = provider;
		}

		internal ServiceProvider(IValueByTypeProvider provider)
		{
			Type = ServiceProviderType.IValueByTypeProvider;
			reference = provider;
		}

		internal ServiceProvider(CrossSceneReference crossSceneReference)
		{
			Type = ServiceProviderType.IValueByTypeProvider;
			reference = crossSceneReference;
		}

		internal ServiceProvider(IValueByTypeProviderAsync provider)
		{
			Type = ServiceProviderType.IValueByTypeProviderAsync;
			reference = provider;
		}

		internal ServiceProvider(IValueProvider provider)
		{
			Type = ServiceProviderType.IValueProvider;
			reference = provider;
		}

		internal ServiceProvider(IValueProviderAsync provider)
		{
			Type = ServiceProviderType.IValueProviderAsync;
			reference = provider;
		}

		public bool TryGetFor([AllowNull] Component client, out TService result) => Type switch
		{
			ServiceProviderType.IValueProviderT => valueProviderT.TryGetFor(client, out result),
			ServiceProviderType.IValueProviderAsyncT => ValueProviderUtility.TryGetFromAwaitableIfCompleted(valueProviderAsyncT.GetForAsync(client), out result),
			ServiceProviderType.IValueByTypeProvider => valueByTypeProvider.TryGetFor(client, out result),
			ServiceProviderType.IValueByTypeProviderAsync => ValueProviderUtility.TryGetFromAwaitableIfCompleted(valueByTypeProviderAsync.GetForAsync<TService>(client), out result),
			ServiceProviderType.IValueProvider => valueProvider.TryGetFor(client, out object objectValue) ? Find.In(objectValue, out result) : None(out result),
			ServiceProviderType.IValueProviderAsync => ValueProviderUtility.TryGetFromAwaitableIfCompleted(valueProviderAsync.GetForAsync(client), out result),
			_ => None(out result)
		};

		private static bool None(out TService result)
		{
			result = default;
			return false;
		}

#if (ENABLE_BURST_AOT || ENABLE_IL2CPP) && !INIT_ARGS_DISABLE_AUTOMATIC_AOT_SUPPORT
		private static void EnsureAOTPlatformSupport() => ServiceUtility.EnsureAOTPlatformSupportForService<TService>();
#endif

		public bool Equals(ServiceProvider<TService> other) => other is not null && Type == other.Type && ReferenceEquals(GetValueProvider(), other.GetValueProvider());

		public override bool Equals(object obj)
		{
			if(obj is not ServiceProvider<TService> serviceProvider)
			{
				return false;
			}

			return Equals(serviceProvider);
		}

		public override int GetHashCode() => HashCode.Combine(Type, GetValueProvider()?.GetHashCode() ?? 0);

		internal object GetValueProvider() => Type switch
		{
			ServiceProviderType.IValueProviderT => valueProviderT,
			ServiceProviderType.IValueProviderAsyncT => valueProviderAsyncT,
			ServiceProviderType.IValueByTypeProvider => valueByTypeProvider,
			ServiceProviderType.IValueByTypeProviderAsync => valueByTypeProviderAsync,
			ServiceProviderType.IValueProvider => valueProvider,
			ServiceProviderType.IValueProviderAsync => valueProviderAsync,
			_ => null
		};
	}
}