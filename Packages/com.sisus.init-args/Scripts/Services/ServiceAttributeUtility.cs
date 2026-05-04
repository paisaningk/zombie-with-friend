using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text;
using UnityEngine;

namespace Sisus.Init.Internal
{
	internal static class ServiceAttributeUtility
	{
		private static readonly ServiceInfo updaterInfo = new(typeof(Updater), new []{ new ServiceAttribute(typeof(ICoroutineRunner))}, typeof(Updater), new []{ typeof(ICoroutineRunner)});

		internal static readonly Dictionary<Type, ServiceInfo> concreteTypes = new()
		{
			{ typeof(Updater), updaterInfo }
		};

		internal static readonly Dictionary<Type, ServiceInfo> definingTypes = new()
		{
			{ typeof(ICoroutineRunner), updaterInfo }
		};

		static ServiceAttributeUtility()
		{
			foreach(var typeWithAttribute in TypeUtility.GetTypesWithAttribute<ServiceAttribute>())
			{
				// TODO: At some point change this to typeWithAttribute.GetCustomAttribute (singular) once customers have had enough time to adapt to the change.
				var attributes = typeWithAttribute.GetCustomAttributes<ServiceAttribute>().ToArray();

				#if DEBUG
				if(attributes.Length > 1)
				{
					Debug.LogWarning("Multiple [Service] attributes per class is no longer supported; please merge them into a single one with multiple defining types:\n" +
						$"[Service({string.Join(", ", attributes.SelectMany(attribute => attribute.definingTypes.Any() ? attribute.definingTypes : new[] { typeWithAttribute }).Select(type => TypeUtility.ToString(type)))})]" +
						$"class {TypeUtility.ToString(typeWithAttribute)}");
				}
				#endif

				foreach(var info in ServiceInfo.From(typeWithAttribute, attributes))
				{
					if(info.concreteType is not null)
					{
						#if DEBUG
						if(concreteTypes.TryGetValue(info.concreteType, out var conflictingInfo))
						{
							Debug.LogWarning(new StringBuilder()
								.Append("[Service] attribute targeting the same concrete type ")
								.Append(TypeUtility.ToString(info.concreteType))
								.Append(" found on two types: ")
								.Append(TypeUtility.ToString(conflictingInfo.classWithAttribute))
								.Append(", ")
								.Append(TypeUtility.ToString(info.classWithAttribute))
								.Append(".\nOne of them will be ignored.").ToString());
						}
						#endif

						concreteTypes[info.concreteType] = info;
					}
					#if DEV_MODE
					else Debug.Log($"{TypeUtility.ToString(typeWithAttribute)} concreteType is null. serviceProviderType:{info.serviceProviderType}, definingTypes:{TypeUtility.ToString(info.definingTypes)}");
					#endif

					foreach(var definingType in info.definingTypes)
					{
						#if DEBUG
						if(definingTypes.TryGetValue(definingType, out var conflictingInfo) && !conflictingInfo.classWithAttribute.IsAbstract && !info.classWithAttribute.IsAbstract)
						{
							Debug.LogWarning(new StringBuilder()
								.Append("[Service] attribute with the defining type ")
								.Append(TypeUtility.ToString(definingType))
								.Append(" found on two types: ")
								.Append(TypeUtility.ToString(conflictingInfo.classWithAttribute))
								.Append(", ")
								.Append(TypeUtility.ToString(info.classWithAttribute))
								.Append(". One of them will be ignored.").ToString());
						}
						#endif

						definingTypes[definingType] = info;
					}
				}
			}
		}

		public static bool ContainsDefiningType(Type definingType)
			=> definingTypes.ContainsKey(definingType)
			|| (definingType.IsGenericType && definingTypes.ContainsKey(definingType.GetGenericTypeDefinition()));

		public static bool TryGetInfoForDefiningType(Type definingType, out ServiceInfo serviceInfo)
			=> definingTypes.TryGetValue(definingType, out serviceInfo) || (definingType.IsGenericType && definingTypes.TryGetValue(definingType.GetGenericTypeDefinition(), out serviceInfo));
	}
}