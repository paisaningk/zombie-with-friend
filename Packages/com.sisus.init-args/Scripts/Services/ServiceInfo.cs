using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Linq;
using System.Runtime.CompilerServices;
using Sisus.Init.Internal;
using UnityEngine;

#if UNITY_ENTITIES && !INIT_ARGS_DISABLE_ECS_SUPPORT
using Unity.Entities;
#endif

namespace Sisus.Init
{
	/// <summary>
	/// Information about a shared and globally accessible service registered using the <see cref="ServiceAttribute"/>.
	/// </summary>
	internal sealed class ServiceInfo
	{
		private static readonly Dictionary<Type, ServiceProviderType> genericInterfaceToServiceProviderType = new()
		{
			{ typeof(IServiceInitializer<>), ServiceProviderType.ServiceInitializer },
			{ typeof(IServiceInitializer<,>), ServiceProviderType.ServiceInitializer },
			{ typeof(IServiceInitializer<,,>), ServiceProviderType.ServiceInitializer },
			{ typeof(IServiceInitializer<,,,>), ServiceProviderType.ServiceInitializer },
			{ typeof(IServiceInitializer<,,,,>), ServiceProviderType.ServiceInitializer },
			{ typeof(IServiceInitializer<,,,,,>), ServiceProviderType.ServiceInitializer },
			{ typeof(IServiceInitializer<,,,,,,>), ServiceProviderType.ServiceInitializer },
			{ typeof(IServiceInitializer<,,,,,,,>), ServiceProviderType.ServiceInitializer },
			{ typeof(IServiceInitializer<,,,,,,,,>), ServiceProviderType.ServiceInitializer },
			{ typeof(IServiceInitializer<,,,,,,,,,>), ServiceProviderType.ServiceInitializer },
			{ typeof(IServiceInitializer<,,,,,,,,,,>), ServiceProviderType.ServiceInitializer },
			{ typeof(IServiceInitializer<,,,,,,,,,,,>), ServiceProviderType.ServiceInitializer },
			{ typeof(IServiceInitializer<,,,,,,,,,,,,>), ServiceProviderType.ServiceInitializer },
			{ typeof(IServiceInitializerAsync<>), ServiceProviderType.ServiceInitializerAsync },
			{ typeof(IServiceInitializerAsync<,>), ServiceProviderType.ServiceInitializerAsync },
			{ typeof(IServiceInitializerAsync<,,>), ServiceProviderType.ServiceInitializerAsync },
			{ typeof(IServiceInitializerAsync<,,,>), ServiceProviderType.ServiceInitializerAsync },
			{ typeof(IServiceInitializerAsync<,,,,>), ServiceProviderType.ServiceInitializerAsync },
			{ typeof(IServiceInitializerAsync<,,,,,>), ServiceProviderType.ServiceInitializerAsync },
			{ typeof(IServiceInitializerAsync<,,,,,,>), ServiceProviderType.ServiceInitializerAsync },
			{ typeof(IServiceInitializerAsync<,,,,,,,>), ServiceProviderType.ServiceInitializerAsync },
			{ typeof(IServiceInitializerAsync<,,,,,,,,>), ServiceProviderType.ServiceInitializerAsync },
			{ typeof(IServiceInitializerAsync<,,,,,,,,,>), ServiceProviderType.ServiceInitializerAsync },
			{ typeof(IServiceInitializerAsync<,,,,,,,,,,>), ServiceProviderType.ServiceInitializerAsync },
			{ typeof(IServiceInitializerAsync<,,,,,,,,,,,>), ServiceProviderType.ServiceInitializerAsync },
			{ typeof(IServiceInitializerAsync<,,,,,,,,,,,,>), ServiceProviderType.ServiceInitializerAsync },
			{ typeof(IInitializer<>), ServiceProviderType.Initializer },
			{ typeof(IWrapper<>), ServiceProviderType.Wrapper },
			{ typeof(IValueProvider<>), ServiceProviderType.IValueProviderT },
			{ typeof(IValueProviderAsync<>), ServiceProviderType.IValueProviderAsyncT },
		};

		/// <summary>
		/// Type of the class that has the <see cref="ServiceAttribute"/> for this service.
		/// </summary>
		/// <remarks>
		/// <para>
		/// If <see cref="serviceProviderType"/> is other than <see cref="ServiceProviderType.None"/>,
		/// then this is the type of the service provider class.
		/// </para>
		/// <para>
		/// If <see cref="serviceProviderType"/> is <see cref="ServiceProviderType.None"/>,
		/// then this is the concrete type of the service, an interface that the service implements,
		/// or an abstract class that the service derives from.
		/// </para>
		/// </remarks>
		[NotNull] public readonly Type classWithAttribute;

		/// <summary>
		/// Specifies which clients have access to the service.
		/// </summary>
		public Clients clients => Clients.Everywhere;

		/// <summary>
		/// The concrete type of the registered service.
		/// </summary>
		/// <remarks>
		/// <para>
		/// Can be null when the <see cref="ServiceAttribute"/> is attached to an initializer
		/// like a <see cref="CustomInitializer{TClient, TArgument}"/> where the generic type of the initialized object is abstract.
		/// </para>
		/// <para>
		/// Can also be a generic type definition; e.g. type Logger{T} registered using [Service(typeof(ILogger{}))].
		/// </para>
		/// </remarks>
		[MaybeNull] public readonly Type concreteType;

		/// <summary>
		/// An array containing of the defining types of the service.
		/// </summary>
		[NotNull] public readonly Type[] definingTypes;

		public readonly LoadMethod loadMethod;
		public readonly ReferenceType referenceType;
		private readonly string loadData;

		/// <summary>
		/// Returns <see cref="concreteType"/> if it's not <see langword="null"/>,
		/// otherwise returns first element from <see cref="definingTypes"/>.
		/// <para>
		/// Can be an abstract or a generic type definition.
		/// </para>
		/// </summary>
		[NotNull] public Type ConcreteOrDefiningType => concreteType ?? definingTypes.FirstOrDefault();

		public bool HasClosedConcreteType => concreteType is { ContainsGenericParameters: false };

		public readonly ServiceProviderType serviceProviderType;

		public bool IsValueProvider => serviceProviderType
				is ServiceProviderType.IValueProvider
				or ServiceProviderType.IValueProviderT
				or ServiceProviderType.IValueProviderAsyncT
				or ServiceProviderType.IValueByTypeProvider
				or ServiceProviderType.IValueByTypeProviderAsync
				or ServiceProviderType.IValueProviderAsync;

		public readonly bool LazyInit;
		public readonly bool IsTransient;
		public readonly bool LoadAsync;
		public readonly bool DontDestroyOnLoad;
		public bool FindFromScene => loadMethod is LoadMethod.FindFromScene;
		//public string SceneName => referenceType is ReferenceType.SceneName ? loadData : null;
		//public int SceneBuildIndex => referenceType is ReferenceType.SceneBuildIndex && int.TryParse(loadData, out int buildIndex) ? buildIndex : -1;
		public string ResourcePath => referenceType is ReferenceType.ResourcePath ? loadData : null;

		public string SceneName
		{
			get
			{
				if(referenceType is ReferenceType.SceneName)
				{
					return loadData;
				}

				if(referenceType is ReferenceType.SceneBuildIndex)
				{
					int buildIndex = SceneBuildIndex;
					if(buildIndex >= 0)
					{
						var scenePath = UnityEngine.SceneManagement.SceneUtility.GetScenePathByBuildIndex(buildIndex);
						return System.IO.Path.GetFileNameWithoutExtension(scenePath);
					}
				}

				return null;
			}
		}

		public int SceneBuildIndex
		{
			get
			{
				if(referenceType is ReferenceType.SceneBuildIndex)
				{
					if(int.TryParse(loadData, out int buildIndex))
					{
						return buildIndex;
					}
				}
				else if(referenceType is ReferenceType.SceneName)
				{
					#if UNITY_EDITOR
					foreach(var scene in UnityEditor.EditorBuildSettings.scenes)
					{
						if(string.Equals(System.IO.Path.GetFileNameWithoutExtension(scene.path), loadData))
						{
							return UnityEngine.SceneManagement.SceneUtility.GetBuildIndexByScenePath(scene.path);
						}
					}
					// Support not needed at the moment, so commented out for performance.
					// #else
					// const int MaxScenesInBuildSettings = 10_000;
					// for(var i = 0; i < MaxScenesInBuildSettings; i++)
					// {
					// 	var someScenePath = UnityEngine.SceneManagement.SceneUtility.GetScenePathByBuildIndex(i);
					// 	if(string.IsNullOrEmpty(someScenePath))
					// 	{
					// 		break;
					// 	}
					//
					// 	if(string.Equals(System.IO.Path.GetFileNameWithoutExtension(someScenePath), loadData))
					// 	{
					// 		return i;
					// 	}
					// }
					#endif
				}

				return -1;
			}
		} 

		public string ScenePath
		{
			get
			{
				if(referenceType is ReferenceType.SceneName)
				{
					#if UNITY_EDITOR
					string result = null;

					foreach(var scene in UnityEditor.EditorBuildSettings.scenes)
					{
						if(!string.Equals(System.IO.Path.GetFileNameWithoutExtension(scene.path), loadData))
						{
							continue;
						}

						if(result is null)
						{
							result = scene.path;
							continue;
						}

						// If Build Settings contain multiple scenes with the same name, then we can't determine which one to load.
						return null;
					}

					return result;
					#else
					return null;
					#endif
				}

				if(referenceType is ReferenceType.SceneBuildIndex)
				{
					return UnityEngine.SceneManagement.SceneUtility.GetScenePathByBuildIndex(SceneBuildIndex);
				}

				return null;
			}
		}

		#if UNITY_ADDRESSABLES_1_17_4_OR_NEWER
		public string AddressableKey => referenceType is ReferenceType.AddressableKey ? loadData : null;
		#endif

		#if UNITY_ENTITIES && !INIT_ARGS_DISABLE_ECS_SUPPORT
		public string WorldName => referenceType is ReferenceType.World ? loadData : null;
		public readonly bool IsSystem;
		public readonly bool IsWorld;
		#endif

		public bool ShouldInstantiate(bool isPrefab) => loadMethod switch
		{
			LoadMethod.Instantiate => true,
			LoadMethod.Load or LoadMethod.FindFromScene => false,
			_ => isPrefab
		};

		public static IEnumerable<ServiceInfo> From([DisallowNull] Type typeWithAttribute, [DisallowNull] ServiceAttribute[] attributes)
		{
			if (!typeWithAttribute.IsAbstract)
			{
				yield return new(typeWithAttribute, attributes);
				yield break;
			}

			var registeredDefiningTypes = new Dictionary<Type, bool>(8);
			var concreteToDefiningTypes = new Dictionary<Type, HashSet<Type>>(8);

			foreach(var attribute in attributes)
			{
				foreach(var definingType in attribute.definingTypes)
				{
					// Support
					// [Service(typeof(Logger<>))]
					// public interface ILogger<T> { }
					// registering the concrete class Logger<> as ILogger<>
					if(!definingType.IsAbstract)
					{
						yield return new(typeWithAttribute, attributes, concreteType: definingType, definingTypes: new[] { typeWithAttribute });
						registeredDefiningTypes[definingType] = false;
						continue;
					}

					// Support
					// [Service(typeof(ISingleton<>))]
					// public interface ISingleton<T> { }
					// registering all types that implement ISingleton<TService> and ISingleton<TService>
					if(definingType.ContainsGenericParameters)
					{
						foreach(var derivedType in TypeUtility.GetOpenGenericTypeDerivedTypes(definingType, concreteOnly: true))
						{
							AddConcreteDerivedAndImplementedTypes(concreteToDefiningTypes, registeredDefiningTypes, derivedType);
						}

						continue;
					}

					// Support
					// [Service(typeof(Logger))]
					// public interface ILogger { }
					// registering all Logger<T> as ILogger<TService>
					foreach(var derivedType in TypeUtility.GetDerivedTypes(typeWithAttribute))
					{
						if(!derivedType.IsAbstract)
						{
							AddConcreteDerivedAndImplementedTypes(concreteToDefiningTypes, registeredDefiningTypes, derivedType);
						}
					}
				}
			}

			if (attributes.Length != 1 || attributes[0].definingTypes.Length > 0)
			{
				goto RegisterAll;
			}

			if(typeWithAttribute.IsGenericTypeDefinition)
			{
				// Support
				// [Service]
				// public interface ISingleton<TSingleton> { }
				// registering all implementing types as services, with their concrete type, all base types and all the interfaces types
				// as the defining types of the service, which fulfill these requirements:
				// 1. None other of the types that derive from Manager may also derive from or implement that same type.
				// 2. The type can't be a common built-in type, such as System.Object, UnityEngine.Object or IEnumerable.
				foreach(var derivedType in TypeUtility.GetOpenGenericTypeDerivedTypes(typeWithAttribute, concreteOnly:true))
				{
					AddConcreteDerivedAndImplementedTypes(concreteToDefiningTypes, registeredDefiningTypes, derivedType);
				}

				goto RegisterAll;
			}

			// Support:
			// [Service]
			// public abstract class Manager { }
			// registering all derived types as services, with their concrete type, all base types and all the interfaces types
			// as the defining types of the service, which fulfill these requirements:
			// 1. None other of the types that derive from Manager may also derive from or implement that same type.
			// 2. The type can't be a common built-in type, such as System.Object, UnityEngine.Object or IEnumerable.
			foreach(var serviceType in TypeUtility.GetDerivedTypes(typeWithAttribute))
			{
				if(!serviceType.IsAbstract)
				{
					AddConcreteDerivedAndImplementedTypes(concreteToDefiningTypes, registeredDefiningTypes, serviceType);
				}
			}

			RegisterAll:

			foreach(var (concreteType, definingTypes) in concreteToDefiningTypes)
			{
				yield return new(typeWithAttribute, attributes, concreteType, definingTypes.ToArray());
			}

			static void AddConcreteDerivedAndImplementedTypes(Dictionary<Type, HashSet<Type>> concreteToDefiningTypes, Dictionary<Type, bool> registeredDefiningTypes, Type serviceType)
			{
				// Registering service serviceType as it's concrete type, and as its defines types the following:
				// its own type, all the types it derives from, and all the interface types it implements, which fulfill these requirements:
				// 1. None other of the types that derive from serviceType may also derive from/implement that same type.
				// 2. The type can't be a common built-in type, such as System.Object, UnityEngine.Object or IEnumerable.

				Add(concreteToDefiningTypes, registeredDefiningTypes, concreteType:serviceType, definingType:serviceType);

				foreach(var baseType in serviceType.GetBaseTypes())
				{
					Add(concreteToDefiningTypes, registeredDefiningTypes, concreteType:serviceType, definingType:baseType);
				}

				foreach(var interfaceType in serviceType.GetInterfaces())
				{
					const int SystemLength = 6;
					if(interfaceType.Namespace is string @namespace
					&& @namespace.StartsWith("System")
					&& (@namespace.Length == SystemLength || @namespace[SystemLength] is '.'))
					{
						continue;
					}

					Add(concreteToDefiningTypes, registeredDefiningTypes, serviceType, interfaceType);
				}
			}

			static void Add(Dictionary<Type, HashSet<Type>> concreteToDefiningTypes, Dictionary<Type, bool> registeredDefiningTypes, Type concreteType, Type definingType)
			{
				if(registeredDefiningTypes.TryGetValue(definingType, out var isFirst))
				{
					if(isFirst)
					{
						foreach(var removeFrom in concreteToDefiningTypes.Values)
						{
							removeFrom.Remove(definingType);
						}

						registeredDefiningTypes[definingType] = false;
					}

					return;
				}

				registeredDefiningTypes.Add(definingType, true);

				if(!concreteToDefiningTypes.TryGetValue(concreteType, out var addTo))
				{
					addTo = new();
					concreteToDefiningTypes.Add(concreteType, addTo);
				}

				addTo.Add(definingType);
			}
		}

		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		private static void SetupFromAttributes([DisallowNull] ServiceAttribute[] attributes, out LoadMethod loadMethod, out ReferenceType referenceType, out string loadData, out bool dontDestroyOnLoad, ref bool loadAsync)
		{
			bool? setDontDestroyOnLoad = null;
			loadMethod = LoadMethod.Default;
			referenceType = ReferenceType.None;
			loadData = "";

			foreach(var attribute in attributes)
			{
				if(attribute.loadMethod is not LoadMethod.Default)
				{
					#if DEV_MODE || DEBUG || INIT_ARGS_SAFE_MODE
					if(loadMethod != LoadMethod.Default && loadMethod != attribute.loadMethod) Debug.LogWarning($"Replacing loadMethod {loadMethod} with {attribute.loadMethod} for [Service] target with defining types {string.Join(", ", attributes.SelectMany(a => a.definingTypes))}.");
					#endif

					loadMethod = attribute.loadMethod;

					if(loadMethod is LoadMethod.FindFromScene)
					{
						setDontDestroyOnLoad ??= false;
					}
				}

				if(attribute.referenceType is not ReferenceType.None)
				{
					#if DEV_MODE || DEBUG || INIT_ARGS_SAFE_MODE
					if(referenceType != ReferenceType.None && referenceType != attribute.referenceType) Debug.LogWarning($"Replacing referenceType {referenceType} with {attribute.referenceType} for [Service] target with defining types {string.Join(", ", attributes.SelectMany(a => a.definingTypes))}.");
					#endif

					referenceType = attribute.referenceType;
				}

				if(attribute.loadData is { Length: > 0 })
				{
					#if DEV_MODE || DEBUG || INIT_ARGS_SAFE_MODE
					if(loadData is { Length: > 0 } && !string.Equals(loadData, attribute.loadData)) Debug.LogWarning($"Replacing loadData \"{loadData}\" with \"{attribute.loadData}\" for [Service] target with defining types {string.Join(", ", attributes.SelectMany(a => a.definingTypes))}.");
					#endif

					loadData = attribute.loadData;
				}

				if(attribute.dontDestroyOnLoad is { } notNullValue)
				{
					setDontDestroyOnLoad = notNullValue;
				}

				if(attribute.LoadAsync)
				{
					loadAsync = true;
				}
			}

			dontDestroyOnLoad = setDontDestroyOnLoad ?? true;
		}

		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		private void SetupLazyInitAndIsTransient([DisallowNull] ServiceAttribute[] attributes, out bool lazyInit, out bool isTransient)
		{
			foreach(var attribute in attributes)
			{
				if(attribute.lazyInit.HasValue)
				{
					#if DEBUG || INIT_ARGS_SAFE_MODE
					if(!attribute.lazyInit.Value && (concreteType ?? classWithAttribute) is { ContainsGenericParameters: true })
					{
						Debug.LogWarning($"LazyInit has been disabled on [Service] attribute on {TypeUtility.ToString(classWithAttribute)}, but the type contains open generic type parameters.\n" +
							"LazyInit should not be disabled for types with open generic type parameters, because the generic type arguments can not be resolved until a client requests the service." 
							#if UNITY_EDITOR
							, Find.Script(classWithAttribute)
							#endif
							);
						lazyInit = false;
						isTransient = true;
						return;
					}
					#endif

					lazyInit = attribute.lazyInit.Value;
					// If LazyInit was explicitly set to false, then it makes no sense for the service to be transient (per-client).
					isTransient = !lazyInit && IsValueProvider;
					return;
				}
			}

			// Objects located from scenes should be retrieved lazily by default, to help avoid errors, in case they're not present in the initial scene.
			if(loadMethod is LoadMethod.FindFromScene && referenceType is not ReferenceType.SceneName and not ReferenceType.SceneBuildIndex)
			{
				lazyInit = true;
				isTransient = IsValueProvider;
				return;
			}

			if(serviceProviderType is ServiceProviderType.None)
			{
				// If we will only know the concrete and closed type of the service when a client requests it, then the service should be initialized lazily.
				lazyInit = concreteType is not { ContainsGenericParameters: false };
				isTransient = false;
				return;
			}

			// Value providers should be transient by default, so we should only initialize them lazily when a client requests an instance.
			// Service initializers should only be initialized lazily if they contain open generic type parameters that can't be resolved without knowing the requested type.
			isTransient = IsValueProvider;
			lazyInit = isTransient || classWithAttribute.ContainsGenericParameters;
		}

		private ServiceInfo([DisallowNull] Type concreteClassWithAttribute, [DisallowNull] ServiceAttribute[] attributes)
		{
			classWithAttribute = concreteClassWithAttribute;
			concreteType = GetConcreteType(classWithAttribute, attributes, out serviceProviderType);
			LoadAsync = serviceProviderType is ServiceProviderType.ServiceInitializerAsync or ServiceProviderType.IValueProviderAsyncT or ServiceProviderType.IValueByTypeProviderAsync;

			definingTypes = GetDefiningTypes(concreteType, attributes);

			SetupFromAttributes(attributes, out loadMethod, out referenceType, out loadData, out DontDestroyOnLoad, ref LoadAsync);
			SetupLazyInitAndIsTransient(attributes, out LazyInit, out IsTransient);

			#if UNITY_ENTITIES && !INIT_ARGS_DISABLE_ECS_SUPPORT
			if(concreteType is not null)
			{
				if(typeof(ISystem).IsAssignableFrom(concreteType) || typeof(ComponentSystemBase).IsAssignableFrom(concreteType))
				{
					IsSystem = true;
				}
				else if(typeof(World).IsAssignableFrom(concreteType))
				{
					IsWorld = true;
				}
			}
			#endif

			[return: MaybeNull]
			static Type GetConcreteType([DisallowNull] Type concreteClassWithAttribute, [DisallowNull] ServiceAttribute[] attributes, out ServiceProviderType serviceProviderType)
			{
				if(TryGetConcreteAndServiceProviderTypeFromGenericValueProviderInterface(concreteClassWithAttribute, attributes, out Type concreteType, out serviceProviderType))
				{
					return concreteType;
				}

				if(AllDefiningTypesAreAssignableFrom(attributes, concreteClassWithAttribute))
				{
					return concreteClassWithAttribute;
				}

				concreteType = attributes.SelectMany(attribute => attribute.definingTypes).SingleOrDefaultNoException(t => t is { IsAbstract: false });

				// NOTE: TryGetConcreteAndServiceProviderTypeFromGenericValueProviderInterface can return false but still assign serviceProviderType to a value other than None,
				// if the service provider returns an abstract type (e.g. IValueProvider<ILogger>).
				if(serviceProviderType is ServiceProviderType.None)
				{
					if(typeof(IValueByTypeProvider).IsAssignableFrom(concreteClassWithAttribute))
					{
						serviceProviderType = ServiceProviderType.IValueByTypeProvider;
					}
					else if(typeof(IValueByTypeProviderAsync).IsAssignableFrom(concreteClassWithAttribute))
					{
						serviceProviderType = ServiceProviderType.IValueByTypeProviderAsync;
					}
					else if(typeof(IValueProvider).IsAssignableFrom(concreteClassWithAttribute))
					{
						serviceProviderType = ServiceProviderType.IValueProvider;
					}
					else if(typeof(IValueProviderAsync).IsAssignableFrom(concreteClassWithAttribute))
					{
						serviceProviderType = ServiceProviderType.IValueProviderAsync;

						#if DEV_MODE || DEBUG || INIT_ARGS_SAFE_MODE
						if(concreteClassWithAttribute.GetInterfaces().FirstOrDefault(x => x.IsGenericType && x.GetGenericTypeDefinition() == typeof(IValueProviderAsync<>)) is { } genericValueProviderInterface)
						{
							var nonAssignableValueType = genericValueProviderInterface.GetGenericArguments()[0];
							var definingTypes = GetDefiningTypes(concreteType, attributes);
							if(definingTypes.FirstOrDefault(x => !IsAssignableFrom(x, nonAssignableValueType) && !IsAssignableFrom(nonAssignableValueType, x)) is { } unassignableDefiningType)
							{
								Debug.LogWarning(new System.Text.StringBuilder()
									.Append("[Service] attribute")
									.Append(" on value provider ")
									.Append(TypeUtility.ToString(concreteClassWithAttribute))
									.Append(" has the defining type ")
									.Append(TypeUtility.ToString(unassignableDefiningType))
									.Append(" which is not assignable from its returned value type ")
									.Append(TypeUtility.ToString(nonAssignableValueType))
									.Append(".\nEither change the defining type to ")
									.Append(TypeUtility.ToString(nonAssignableValueType))
									.Append(" or make ")
									.Append(TypeUtility.ToString(concreteClassWithAttribute))
									.Append(" implement IValueProvider<")
									.Append(TypeUtility.ToString(unassignableDefiningType))
									.Append(">.")
									#if UNITY_EDITOR
									, Find.Script(concreteClassWithAttribute)
									#endif
									);
							}
						}
						#endif
					}
					else
					{
						#if DEV_MODE || DEBUG || INIT_ARGS_SAFE_MODE
						var definingTypes = GetDefiningTypes(concreteType, attributes);
						if(definingTypes.FirstOrDefault(x => !IsAssignableFrom(x, concreteClassWithAttribute) && !IsAssignableFrom(concreteClassWithAttribute, x)) is { } unassignableDefiningType)
						{
							Debug.LogWarning(new System.Text.StringBuilder()
								.Append("[Service] attribute has the defining type ")
								.Append(TypeUtility.ToString(unassignableDefiningType))
								.Append(" which is not assignable from the concrete class with the attribute ")
								.Append(TypeUtility.ToString(concreteClassWithAttribute))
								.Append(".")
								#if UNITY_EDITOR
								, Find.Script(concreteClassWithAttribute)
								#endif
								);
						}
						#endif
					}
				}
				#if DEV_MODE || DEBUG || INIT_ARGS_SAFE_MODE
				else if(concreteClassWithAttribute.GetInterfaces().FirstOrDefault(x => x.IsGenericType && x.GetGenericTypeDefinition() == typeof(IValueProvider<>)) is { } genericValueProviderInterface)
				{
					var nonAssignableValueType = genericValueProviderInterface.GetGenericArguments()[0];
					var definingTypes = GetDefiningTypes(concreteType, attributes);
					if(definingTypes.FirstOrDefault(x => !IsAssignableFrom(x, nonAssignableValueType) && !nonAssignableValueType.IsAssignableFrom(x)) is { } unassignableDefiningType)
					{
						Debug.LogWarning(new System.Text.StringBuilder()
							.Append("[Service] attribute")
							.Append(" on value provider ")
							.Append(TypeUtility.ToString(concreteClassWithAttribute))
							.Append(" has the defining type ")
							.Append(TypeUtility.ToString(unassignableDefiningType))
							.Append(" which is not assignable from its returned value type ")
							.Append(TypeUtility.ToString(nonAssignableValueType))
							.Append(".\nEither change the defining type to ")
							.Append(TypeUtility.ToString(nonAssignableValueType))
							.Append(" or make ")
							.Append(TypeUtility.ToString(concreteClassWithAttribute))
							.Append(" implement IValueProvider<")
							.Append(TypeUtility.ToString(unassignableDefiningType))
							.Append(">.")
							#if UNITY_EDITOR
							, Find.Script(concreteClassWithAttribute)
							#endif
							);
					}
				}
				#endif

				#if DEV_MODE || DEBUG || INIT_ARGS_SAFE_MODE
				if(concreteType is not null)
				{
					foreach(var attribute in attributes)
					{
						foreach(var definingType in attribute.definingTypes)
						{
							if(!ServiceUtility.IsValidDefiningTypeFor(definingType, concreteType))
							{
								var classWithAttributeName = TypeUtility.ToString(concreteClassWithAttribute);
								var definingTypeName = TypeUtility.ToString(definingType);
								Debug.LogAssertion($"Invalid {nameof(ServiceAttribute)} detected on {classWithAttributeName}. {classWithAttributeName} is not assignable to service defining type {definingTypeName}, and does not implement {nameof(IInitializer)}<{definingTypeName}> or {nameof(IWrapper)}<{definingTypeName}>, IServiceInitializer<{definingTypeName}> or IValueProvider<{definingTypeName}>. Unable to determine concrete type of the service.");
							}
						}
					}
				}
				#endif

				return concreteType;
			}

			[MethodImpl(MethodImplOptions.AggressiveInlining)]
			static bool TryGetConcreteAndServiceProviderTypeFromGenericValueProviderInterface([DisallowNull] Type concreteClassWithAttribute, [DisallowNull] ServiceAttribute[] attributes, [NotNullWhen(true)][MaybeNullWhen(false)] out Type concreteType, out ServiceProviderType serviceProviderType)
			{
				serviceProviderType = ServiceProviderType.None;
				concreteType = null;

				var implementedInterfaces = concreteClassWithAttribute.GetInterfaces();
				for(int i = implementedInterfaces.Length - 1; i >= 0; i--)
				{
					var interfaceType = implementedInterfaces[i];
					if(!interfaceType.IsGenericType)
					{
						continue;
					}

					var genericTypeDefinition = interfaceType.GetGenericTypeDefinition();
					if(!genericInterfaceToServiceProviderType.TryGetValue(genericTypeDefinition, out var potentialServiceProviderType))
					{
						continue; 
					}

					var providedValueType = interfaceType.GetGenericArguments()[0];
					if(TypeUtility.IsBaseType(providedValueType) || !AllDefiningTypesAreAssignableFrom(attributes, providedValueType))
					{
						#if DEV_MODE
						Debug.Log($"Generic argument {TypeUtility.ToString(providedValueType)} extracted from interface {TypeUtility.ToString(interfaceType)} implemented by {TypeUtility.ToString(concreteClassWithAttribute)} was not a valid concrete type. IsBaseType:{TypeUtility.IsBaseType(providedValueType)}, AllDefiningTypesAreAssignableFrom:{AllDefiningTypesAreAssignableFrom(attributes, providedValueType)}.");
						#endif
						continue;
					}

					if(providedValueType.IsAbstract)
					{
						#if DEV_MODE
						Debug.Log($"Generic argument {TypeUtility.ToString(providedValueType)} extracted from interface {TypeUtility.ToString(interfaceType)} implemented by {TypeUtility.ToString(concreteClassWithAttribute)} was not a valid concrete type because it is abstract.");
						#endif

						if(serviceProviderType is not ServiceProviderType.None
							// Service Initializer over Initializer over Wrapper over Value Provider
							&& GetPriority(serviceProviderType) <= GetPriority(potentialServiceProviderType))
						{
							#if DEV_MODE
							Debug.Log($"Skipping setting serviceProviderType to {potentialServiceProviderType} because it was already set to {serviceProviderType}, which has higher priority.");
							#endif
							continue;
						}

						serviceProviderType = potentialServiceProviderType;
						continue;
					}

					if(serviceProviderType is not ServiceProviderType.None)
					{
						// Service Initializer over Initializer over Wrapper over Value Provider
						if(GetPriority(serviceProviderType) <= GetPriority(potentialServiceProviderType))
						{
							#if DEV_MODE
							Debug.Log($"Skipping setting serviceProviderType to {potentialServiceProviderType} because it was already set to {serviceProviderType}, which has higher priority.");
							#endif
							continue;
						}
					}

					serviceProviderType = potentialServiceProviderType;
					concreteType = providedValueType;

					static int GetPriority(ServiceProviderType type) => (int)type;
				}

				return concreteType is not null;
			}
		}

		internal ServiceInfo([DisallowNull] Type classWithAttribute, [DisallowNull] ServiceAttribute[] attributes, Type concreteType, Type[] definingTypes)
		{
			this.classWithAttribute = classWithAttribute;
			this.concreteType = concreteType;
			this.definingTypes = definingTypes;

			SetupFromAttributes(attributes, out loadMethod, out referenceType, out loadData, out DontDestroyOnLoad, ref LoadAsync);
			SetupLazyInitAndIsTransient(attributes, out LazyInit, out IsTransient);

			#if UNITY_ENTITIES && !INIT_ARGS_DISABLE_ECS_SUPPORT
			if(concreteType is not null)
			{
				if(typeof(ISystem).IsAssignableFrom(concreteType) || typeof(ComponentSystemBase).IsAssignableFrom(concreteType))
				{
					IsSystem = true;
				}
				else if(typeof(World).IsAssignableFrom(concreteType))
				{
					IsWorld = true;
				}
			}
			#endif
		}

		internal ServiceInfo(Type registeringClass, Type concreteType, Type[] definingTypes, LoadMethod loadMethod = LoadMethod.Default, ReferenceType referenceType = ReferenceType.None, string loadData = null, bool? lazyInit = null, bool? dontDestroyOnLoad = null, bool loadAsync = false)
		{
			classWithAttribute = registeringClass;
			this.concreteType = concreteType;
			this.definingTypes = definingTypes;
			this.loadMethod = loadMethod;
			this.referenceType = referenceType;
			this.loadData = loadData ?? string.Empty;
			LoadAsync = loadAsync;
			serviceProviderType = ServiceProviderType.None;
			DontDestroyOnLoad = dontDestroyOnLoad ?? true;

			SetupLazyInitAndIsTransient(out LazyInit, out IsTransient);

			[MethodImpl(MethodImplOptions.AggressiveInlining)]
			void SetupLazyInitAndIsTransient(out bool LazyInit, out bool IsTransient)
			{
				if(lazyInit.HasValue)
				{
					#if DEBUG || INIT_ARGS_SAFE_MODE
					if(!lazyInit.Value && (concreteType ?? classWithAttribute) is { ContainsGenericParameters: true })
					{
						Debug.LogWarning($"LazyInit was disabled for service {TypeUtility.ToString(concreteType)}, but the type contains open generic type parameters.\n" +
							"LazyInit should not be disabled for types with open generic type parameters, because the generic type arguments can not be resolved until a client requests the service."
							#if UNITY_EDITOR
							, Find.Script(concreteType)
							#endif
							);
						
						LazyInit = false;
						IsTransient = true;
						return;
					}
					#endif

					LazyInit = lazyInit.Value;
					// If LazyInit was explicitly set to false, then it makes no sense for the service to be transient (per-client).
					IsTransient = !LazyInit && IsValueProvider;
					return;
				}

				// Objects located from scenes should be retrieved lazily by default, to help avoid errors, in case they're not present in the initial scene.
				if(FindFromScene)
				{
					LazyInit = true;
					IsTransient = IsValueProvider;
					return;
				}

				if(serviceProviderType is ServiceProviderType.None)
				{
					// If we will only know the concrete and closed type of the service when a client requests it, then the service should be initialized lazily.
					LazyInit = FindFromScene || concreteType is not { ContainsGenericParameters: false };
					IsTransient = false;
					return;
				}

				// Value providers should be transient by default, so we should only initialize them lazily when a client requests an instance.
				// Service initializers should only be initialized lazily if they contain open generic type parameters that can't be resolved without knowing the requested type.
				IsTransient = IsValueProvider;
				LazyInit = IsTransient || classWithAttribute.ContainsGenericParameters;
			}
		}

		private static bool AllDefiningTypesAreAssignableFrom(ServiceAttribute[] attributes, Type concreteType)
		{
			foreach(var attribute in attributes)
			{
				foreach(var definingType in attribute.definingTypes)
				{
					if(!IsAssignableFrom(definingType, concreteType))
					{
						return false;
					}
				}
			}

			return true;
		}

		private static bool IsAssignableFrom(Type definingType, Type assignableFrom)
		{
			if(!definingType.ContainsGenericParameters && !assignableFrom.ContainsGenericParameters)
			{
				return definingType.IsAssignableFrom(assignableFrom);
			}

			if(definingType == assignableFrom)
			{
				return true;
			}

			var definingTypeDefinition = definingType.IsGenericType ? definingType.GetGenericTypeDefinition() : definingType;
			var definingTypeIsInterface = definingType.IsInterface;
			var assignableFromIsInterface = assignableFrom.IsInterface;
			
			if(definingTypeIsInterface == assignableFromIsInterface)
			{
				for(var type = assignableFrom; type is not null; type = type.BaseType)
				{
					if(type.IsGenericType && definingTypeDefinition == (type.IsGenericType ? type.GetGenericTypeDefinition() : type))
					{
						return true;
					}
				}

				return false;
			}

			// assignableFrom is a class, definingType is an interface
			foreach(var type in assignableFrom.GetInterfaces())
			{
				if(type.IsGenericType && definingTypeDefinition == (type.IsGenericType ? type.GetGenericTypeDefinition() : type))
				{
					return true;
				}
			}

			return false;
		}

		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		private static Type[] GetDefiningTypes(Type concreteType, ServiceAttribute[] attributes)
		{
			if(attributes.Length == 1)
			{
				return attributes[0].definingTypes is { Length: > 0 } definingTypes ? definingTypes : new[] { concreteType };
			}
			else
			{
				var definingTypes = attributes.SelectMany(attribute => attribute.definingTypes).ToArray();
				return definingTypes.Length > 0 ? definingTypes : new[] { concreteType };
			}
		}

		public bool HasDefiningType(Type type) => Array.IndexOf(definingTypes, type) != -1;

		public bool IsInstanceOf([AllowNull] object instance)
		{
			if(instance is null)
			{
				return false;
			}

			if(concreteType is not null)
			{
				if(concreteType.ContainsGenericParameters)
				{
					var concreteTypeDefinition = concreteType.IsGenericTypeDefinition ? concreteType : concreteType.GetGenericTypeDefinition();
					var instanceType = instance.GetType();
					var instanceTypeDefinition = instanceType.IsGenericType ? instanceType.GetGenericTypeDefinition() : instanceType;
					if(concreteTypeDefinition != instanceTypeDefinition)
					{
						return false;
					}
				}
				else if(!concreteType.IsInstanceOfType(instance))
				{
					return false;
				}
			}

			foreach(var definingType in definingTypes)
			{
				if(definingType.ContainsGenericParameters)
				{
					var definingTypeDefinition = definingType.GetGenericTypeDefinition();
					bool matchFound = false;
					if(definingType.IsInterface)
					{
						foreach(var interfaceType in instance.GetType().GetInterfaces())
						{
							if(interfaceType.IsGenericType && interfaceType.GetGenericTypeDefinition() == definingTypeDefinition)
							{
								matchFound = true;
								break;
							}
						}
					}
					else
					{
						for(var type = instance.GetType(); type != null; type = type.BaseType)
						{
							if(type.IsGenericType && type.GetGenericTypeDefinition() == definingTypeDefinition)
							{
								matchFound = true;
								break;
							}
						}
					}

					if(!matchFound)
					{
						return false;
					}
				}
				else if(!definingType.IsInstanceOfType(instance))
				{
					return false;
				}
			}

			return true;
		}
		
		#if UNITY_EDITOR
		internal bool IsSceneIncludedInBuild => ScenePath is { Length: > 0 };
		#endif
	}
}