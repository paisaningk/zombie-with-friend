using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using Sisus.Init.ValueProviders;
using UnityEngine;
using Object = UnityEngine.Object;

namespace Sisus.Init.Internal
{
	public static class ServiceTagUtility
	{
		private static readonly List<ServiceTag> serviceTags = new();

		public static
		#if DEV_MODE
		IEnumerable<ServiceTag>
		#else
		List<ServiceTag>
		#endif
			GetServiceTagsTargeting(Component serviceOrServiceProvider)
		{
			serviceTags.Clear();
			serviceOrServiceProvider.GetComponents(serviceTags);

			for(int i = serviceTags.Count - 1; i >= 0; i--)
			{
				if(serviceTags[i].Service != serviceOrServiceProvider)
				{
					serviceTags.RemoveAt(i);
				}
			}

			return serviceTags;
		}

		public static bool HasServiceTag(object service) => service is Component component && HasServiceTag(component);

		public static bool HasServiceTag(Component service)
		{
			serviceTags.Clear();
			service.GetComponents(serviceTags);

			foreach(var tag in serviceTags)
			{
				if(tag.Service == service)
				{
					serviceTags.Clear();
					return true;
				}
			}

			serviceTags.Clear();
			return false;
		}

		public static bool IsValidDefiningType([DisallowNull] Type definingType, [DisallowNull] Object service)
		{
			if(definingType.IsInstanceOfType(service))
			{
				return true;
			}

			if(!ValueProviderUtility.IsValueProvider(service))
			{
				return false;
			}

			Type[] interfaces = null;

			if(service is IValueProvider)
			{
				interfaces ??= service.GetType().GetInterfaces();
				foreach(var interfaceType in interfaces)
				{
					if(!interfaceType.IsGenericType || interfaceType.GetGenericTypeDefinition() != typeof(IValueProvider<>))
					{
						continue;
					}

					if(definingType.IsAssignableFrom(interfaceType.GetGenericArguments()[0]))
					{
						return true;
					}
				}
			}

			if(service is IValueProviderAsync)
			{
				interfaces ??= service.GetType().GetInterfaces();
				foreach(var interfaceType in interfaces)
				{
					if(!interfaceType.IsGenericType || interfaceType.GetGenericTypeDefinition() != typeof(IValueProviderAsync<>))
					{
						continue;
					}

					if(definingType.IsAssignableFrom(interfaceType.GetGenericArguments()[0]))
					{
						return true;
					}
				}
			}

			if(service is IValueByTypeProvider valueByTypeProvider)
			{
				if(TypeUtility.IsValidGenericTypeArgument(definingType) && valueByTypeProvider.IsValueTypeSupported(definingType))
				{
					return true;
				}
			}

			if(service is IValueByTypeProviderAsync valueByTypeProviderAsync)
			{
				if(TypeUtility.IsValidGenericTypeArgument(definingType) && valueByTypeProviderAsync.IsValueTypeSupported(definingType))
				{
					return true;
				}
			}

			return false;
		}
	}
}