#define DEBUG_INIT_SERVICES
#define DEBUG_CREATE_SERVICES
#define DEBUG_LAZY_INIT
//#define DEBUG_INIT_TIME
//#define DEBUG_TEAR_DOWN
#define DEBUG_LOAD_SCENE

//#define DEBUG_INIT_SYSTEMS
//#define DEBUG_INIT_WORLDS

#if !INIT_ARGS_DISABLE_SERVICE_INJECTION
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Threading;
using System.Threading.Tasks;
using Sisus.Init.ValueProviders;
using UnityEngine;
using UnityEngine.SceneManagement;
using static Sisus.Init.Internal.InitializerUtility;
using static Sisus.Init.Internal.InitializableUtility;
using Debug = UnityEngine.Debug;
using Object = UnityEngine.Object;

#if UNITY_ADDRESSABLES_1_17_4_OR_NEWER
using UnityEngine.ResourceManagement.AsyncOperations;
using UnityEngine.AddressableAssets;
#endif

#if UNITY_ENTITIES && !INIT_ARGS_DISABLE_ECS_SUPPORT
using Unity.Entities;
#endif

#if UNITY_EDITOR
using UnityEditor.SceneManagement;
using UnityEditor;
using Sisus.Init.EditorOnly;
#endif

namespace Sisus.Init.Internal
{
	/// <summary>
	/// Class responsible for caching instances of all classes that have the <see cref="ServiceAttribute"/>,
	/// injecting dependencies for services that implement an <see cref="IInitializable{}"/>
	/// interface targeting only other services,
	/// and using <see cref="InitArgs.Set"/> to assign references to services ready to be retrieved
	/// for any other classes that implement an <see cref="IArgs{}"/> interface targeting only services.
	/// </summary>
	#if UNITY_EDITOR
	[InitializeOnLoad]
	#endif
	internal static class ServiceInjector
	{
		private const string RestoreActiveSceneKey = "InitArgs.RestoreActiveScene";

		#if UNITY_EDITOR
		internal static bool ServiceInitializationInProgressEditorOnly { get; private set; }
		#endif

		/// <summary>
		/// <see langword="true"/> if all shared services that are loaded synchronously during game initialization
		/// have been created and are ready to be used by clients; otherwise, <see langword="false"/>.
		/// <para>
		/// This only takes into consideration services that are initialized synchronously during game initialization.
		/// To determine if all asynchronously initialized services are also ready to be used,
		/// use <see cref="AsyncServicesAreReady"/> instead.
		/// </para>
		/// <para>
		/// This only takes into consideration services defined using the <see cref="ServiceAttribute"/>.
		/// Services set up in scenes and prefabs using <see cref="ServiceTag"/> and <see cref="Services"/>
		/// components are not guaranteed to be yet loaded even if this is <see langword="true"/>.
		/// Services that are registered manually using <see cref="Service.Set{TService}"/> are also not
		/// guaranteed to be loaded even if this is <see langword="true"/>.
		/// </para>
		/// </summary>
		public static bool ServicesAreReady { get; private set; }

		/// <summary>
		/// <see langword="true"/> if all shared services that are loaded asynchronously during game initialization
		/// have been created, initialized and are ready to be used by clients; otherwise, <see langword="false"/>.
		/// <para>
		/// This only takes into consideration services defined using the <see cref="ServiceAttribute"/>.
		/// </para>
		/// <para>
		/// Services set up in scenes and prefabs using <see cref="ServiceTag"/> and <see cref="Services"/>
		/// components are not guaranteed to be yet loaded even if this is <see langword="true"/>.
		/// Services that are registered manually using <see cref="Service.Set{TService}"/> are also not
		/// guaranteed to be loaded even if this is <see langword="true"/>.
		/// </para>
		/// </summary>
		public static bool AsyncServicesAreReady { get; private set; }

		/// <summary>
		/// Called when all services have been created,
		/// initialized and are ready to be used by clients.
		/// <para>
		/// This only takes into consideration services that are initialized synchronously and non-lazily
		/// during game initialization. To get a callback when all asynchronously initialized services are also
		/// ready to be used, use <see cref="AsyncServicesBecameReady"/> instead.
		/// </para>
		/// </summary>
		public static event Action ServicesBecameReady;

		/// <summary>
		/// Called when all services that are initialized asynchronously have been created,
		/// initialized and are ready to be used by clients.
		/// </summary>
		public static event Action AsyncServicesBecameReady;
		internal static Dictionary<Type, object> services = new();
		private static readonly Dictionary<Type, ServiceInfo> uninitializedServices = new(); // Lazy and transient
		private static GameObject container;
		#if DEV_MODE || DEBUG || INIT_ARGS_SAFE_MODE
		private static readonly HashSet<ServiceInfo> exceptionsLogged = new();
		#endif

		private static bool exitingApplicationOrPlayMode;
		private static readonly List<IDisposable> disposables = new(32);

		private static CancellationToken ExitCancellationToken
		{
			[MethodImpl(MethodImplOptions.AggressiveInlining)]
			get =>
			#if UNITY_2022_3_OR_NEWER
			Application.exitCancellationToken;
			#else
			CancellationToken.None;
			#endif
		}

		#if UNITY_EDITOR
		static ServiceInjector()
		{
			// Register to be called when entering Play Mode in the editor
			EditorApplication.playModeStateChanged -= OnPlayModeStateChanged;
			EditorApplication.playModeStateChanged += OnPlayModeStateChanged;

			static void OnPlayModeStateChanged(PlayModeStateChange state)
			{
				if(state is PlayModeStateChange.ExitingPlayMode)
				{
					ServicesAreReady = false;
					OnExitingApplicationOrPlayMode();
					return;
				}

				if(state is PlayModeStateChange.ExitingEditMode)
				{
					// If any non-lazy, non-async service registered using LoadScene exists in build settings,
					// then open the scene with the lowest index in build settings' scene list synchronously when exiting Edit Mode, because:
					// 1. In builds the game always starts from the first scene, so it's possible to make sure that scenes containing services are loaded before other scenes,
					//    but in Edit Mode the game could get started from any scene.
					// 2. SceneManager.LoadScene does not load scenes immediately, but EditorSceneManager.OpenScene does.
					// 3. EditorSceneManager.OpenScene is only supported in Edit Mode.

					SessionState.EraseString(RestoreActiveSceneKey);

					if(ServiceAttributeUtility.concreteTypes.Values
						.Where(x => x.SceneBuildIndex is not -1 && !x.LazyInit && !x.LoadAsync)
						// Treat scene with the lowest build index as the pre-load scene
						.OrderBy(x => x.SceneBuildIndex)
						.FirstOrDefault() is not { } preloadSceneService)
					{
						#if DEV_MODE
						Debug.Log("Found no pre-load scene service.");
						#endif
						return;
					}

					var preloadSceneBuildIndex = preloadSceneService.SceneBuildIndex;
					var preloadScenePath = SceneUtility.GetScenePathByBuildIndex(preloadSceneBuildIndex);
					var preloadScene = SceneManager.GetSceneByPath(preloadScenePath);
					if(preloadScene.IsValid())
					{
						#if DEV_MODE
						Debug.Log($"Pre-load scene '{preloadScene.name}' is already open.");
						#endif
						return;
					}

					preloadScene = EditorSceneManager.OpenScene(preloadScenePath, OpenSceneMode.Additive);
					EditorSceneManager.MoveSceneBefore(preloadScene, SceneManager.GetSceneAt(0));

					// Set the pre-load scene as the active scene, because Unity loads the active scene first.
					// This way Awake, OnEnable etc. are executed in the correct order based on dependencies.
					var activeSceneWas = SceneManager.GetActiveScene();
					if(activeSceneWas.IsValid())
					{
						SessionState.SetString(RestoreActiveSceneKey, activeSceneWas.path);
					}

					SceneManager.SetActiveScene(preloadScene);
					#if DEV_MODE
					Debug.Log($"Setting pre-load scene '{preloadScene.name}' as active scene.");
					#endif
				}
			}
		}
		#endif

		#if !UNITY_EDITOR
		[RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
		private static void OnEnterPlayMode()
		{
			// Reset state when entering play mode in the editor to support Enter Play Mode Settings.
			ServicesAreReady = false;
			AsyncServicesAreReady = false;
			services.Clear();
			uninitializedServices.Clear();

			// Handle disposing services
			UnityEngine.Application.quitting -= OnExitingApplicationOrPlayMode;
			UnityEngine.Application.quitting += OnExitingApplicationOrPlayMode;
		}
		#endif

		private static void OnExitingApplicationOrPlayMode()
		{
			exitingApplicationOrPlayMode = true;
			services.Clear();
			uninitializedServices.Clear();

			int i = disposables.Count - 1;
			ContinueLoop:
			try
			{
				while(i >= 0)
				{
					disposables[i].Dispose();
					i--;
				}
			}
			catch(Exception exception)
			{
				if(disposables[i] is { } disposable)
				{
					Debug.LogWarning($"Exception occurred while shutting down service {TypeUtility.ToString(disposable.GetType())}:  {exception}");
				}
				else
				{
					Debug.LogWarning($"Exception occurred while shutting down service: {exception}");
				}

				i--;
				goto ContinueLoop;
			}

			disposables.Clear();
		}

		private static bool ShouldInvokeUnityEvents(ServiceInfo serviceInfo, object service)
		{
			if(service is Component || serviceInfo.FindFromScene || Find.typesToWrapperTypes.ContainsKey(service.GetType()) || Find.WrapperOf(service) is not null)
			{
				return false;
			}

			return true;
		}

		/// <summary>
		/// Creates instances of all services,
		/// injects dependencies for services that implement an <see cref="IInitializable{T}"/>
		/// interface targeting only other services,
		/// and uses <see cref="InitArgs.Set{T}"/> to assign references to services ready to be retrieved
		/// for any other classes that implement an <see cref="IArgs{T}"/> interface targeting only services.
		/// </summary>
		[RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
		private static async void CreateAndInjectServices()
		{
			#if UNITY_EDITOR
			exitingApplicationOrPlayMode = false;
			ServiceInitializationInProgressEditorOnly = true;

			if(SessionState.GetString(RestoreActiveSceneKey, null) is { } pathOfSceneToSetActive)
			{
				SessionState.EraseString(RestoreActiveSceneKey);
				var sceneToSetActive = SceneManager.GetSceneByPath(pathOfSceneToSetActive);
				var activeSceneOnInit = SceneManager.GetActiveScene();
				if(sceneToSetActive.IsValid() && activeSceneOnInit != sceneToSetActive)
				{
					var activeSceneWas = SceneManager.GetSceneByPath(pathOfSceneToSetActive);

					if(sceneToSetActive.isLoaded)
					{
						SceneManager.SetActiveScene(sceneToSetActive);
					}
					else
					{
						SceneManager.sceneLoaded += OnSceneLoaded;

						void OnSceneLoaded(Scene scene, LoadSceneMode mode)
						{
							if(!sceneToSetActive.IsValid() || SceneManager.GetActiveScene() != activeSceneOnInit)
							{
								SceneManager.sceneLoaded -= OnSceneLoaded;
								return;
							}

							if(sceneToSetActive.isLoaded)
							{
								SceneManager.sceneLoaded -= OnSceneLoaded;

								#if DEV_MODE && DEBUG_LOAD_SCENE
								Debug.Log($"Restoring scene that was active when exiting Edit Mode: {activeSceneWas}");
								#endif

								SceneManager.SetActiveScene(activeSceneWas);
							}
						}
					}
				}
			}
			#endif

			#if DEV_MODE && DEBUG_INIT_TIME
			var timer = new System.Diagnostics.Stopwatch();
			timer.Start();
			#endif

			#if UNITY_EDITOR
			Service.BatchEditingServices = true;
			#endif

			CreateInstancesOfAllServices();

			#if UNITY_EDITOR
			ServiceInitializationInProgressEditorOnly = false;
			#endif

			ServicesAreReady = true;

			#if UNITY_EDITOR
			var scriptableObjects = Resources.FindObjectsOfTypeAll<ScriptableObject>();
			var uninitializedScriptableObjects = new Dictionary<Type, List<ScriptableObject>>(scriptableObjects.Length);
			foreach(var uninitializedScriptableObject in scriptableObjects)
			{
				var type = uninitializedScriptableObject.GetType();
				if(!uninitializedScriptableObjects.TryGetValue(type, out List<ScriptableObject> instances))
				{
					instances = new(1);
					uninitializedScriptableObjects.Add(type, instances);
				}

				instances.Add(uninitializedScriptableObject);
			}
			#endif

			#if UNITY_EDITOR
			InitializeAlreadyLoadedScriptableObjectsInTheEditor(uninitializedScriptableObjects);
			#endif

			ServicesBecameReady?.Invoke();
			ServicesBecameReady = null;

			#if UNITY_EDITOR
			Service.BatchEditingServices = false;
			#endif

			#if DEV_MODE && DEBUG_INIT_TIME
			Debug.Log($"Initialization of {services.Count} services took {timer.Elapsed.TotalSeconds} seconds.");
			int asyncServiceCount = services.Values.Count(s => s is Task { IsCompleted: false });
			#endif

			while(services.Values.FirstOrDefault(s => s is Task { IsCompleted: false } ) is Task task)
			{
				#if DEV_MODE || DEBUG || INIT_ARGS_SAFE_MODE
				try
				{
				#endif

				await task;

				#if DEV_MODE || DEBUG || INIT_ARGS_SAFE_MODE
				}
				catch(OperationCanceledException)
				{
					if(!Application.isPlaying)
					{
						// Operation canceled exceptions are normal when exiting play mode. 
						return;
					}
				}
				catch(Exception exception)
				{
					if(exception is not OperationCanceledException && exception is not AggregateException { InnerException: OperationCanceledException }) 
					{
						Debug.LogError($"Exception occurred while waiting for service: {exception}");
					}
				}
				#endif

				OnAfterAwait();
			}

			#if DEV_MODE && DEBUG_INIT_TIME
			timer.Stop();
			Debug.Log($"Initialization of {asyncServiceCount} async services took {timer.Elapsed.TotalSeconds} seconds.");
			#endif

			AsyncServicesAreReady = true;
			AsyncServicesBecameReady?.Invoke();
			AsyncServicesBecameReady = null;

			// Make InactiveInitializer.OnAfterDeserialize continue execution
			Until.OnUnitySafeContext();
		}

		private static void CreateInstancesOfAllServices()
		{
			var globalServices = GetServiceDefinitions();
			int definitionCount = globalServices.Count;
			if(definitionCount == 0)
			{
				#if DEV_MODE
				Debug.Log("Won't inject any services because ServiceAttributeUtility.definingTypes is empty.");
				#endif
				services = null;
				return;
			}

			services = new(definitionCount);

			// List of concrete service types that have already been initialized (instance created / retrieved)
			HashSet<Type> initialized = new(definitionCount);

			var localServices = new LocalServices();

			InitializeServices(globalServices, initialized, localServices);

			InjectCrossServiceDependencies(globalServices, initialized, localServices);

			#if UNITY_EDITOR
			CreateServicesDebugger().OnFailure(HandleLogException);
			#endif

			if(container)
			{
				container.SetActive(true);
			}

			HandleExecutingEventFunctionsForAll(initialized);

			static void HandleExecutingEventFunctionsForAll(HashSet<Type> initialized)
			{
				#if DEV_MODE
				var handled = new HashSet<object>();
				#endif

				foreach(var concreteType in initialized)
				{
					// TODO: add InitState to keep track of whether this has already been done or not
					// Or maybe bool shouldExecuteEventFunctions, which is only set to true for
					// objects created by ServiceInjector in the first place, and set to true
					// when ExecuteEventFunctions is called.

					if(ServiceAttributeUtility.concreteTypes.TryGetValue(concreteType, out var serviceInfo)
						&& serviceInfo.FindFromScene)
					{
						continue;
					}

					if(services.TryGetValue(concreteType, out object instance))
					{
						#if DEV_MODE
						Debug.Assert(handled.Add(instance));
						Debug.Assert(!concreteType.IsAbstract, concreteType.Name);
						if(concreteType != instance.GetType() && instance is not Task)
						{
							Debug.LogError($"{TypeUtility.ToString(concreteType)} != {TypeUtility.ToString(instance.GetType())}");
						}
						#endif

						#if !DEBUG
						_ =
						#endif
						HandleExecutingEventFunctionsFor(instance)
						#if DEBUG
						.OnFailure(HandleLogException)
						#endif
						;
					}
				}
			}
		}

		private static async Task HandleExecutingEventFunctionsFor(object instance)
		{
			if(instance is Task task)
			{
				instance = await task.GetResult();
				OnAfterAwait();
			}

			SubscribeToUpdateEvents(instance);
			ExecuteAwake(instance);
			ExecuteOnEnable(instance);
			ExecuteStartAtEndOfFrame(instance);

			if(instance is IOnDisable disableable)
			{
				disposables.Add(new OnDisableCaller(disableable));
			}

			if(instance is IOnDestroy destroyable)
			{
				disposables.Add(new OnDestroyCaller(destroyable));
			}
		}

		#if UNITY_EDITOR
		private static async Task CreateServicesDebugger()
		{
			if(!Application.isPlaying)
			{
				return;
			}

			if(!container)
			{
				CreateServicesContainer();
			}

			var debugger = container.AddComponent<ServicesDebugger>();
			await debugger.SetServices(services.Values.Distinct());
		}
		#endif

		private static void CreateServicesContainer()
		{
			container = new GameObject("Global Services");
			container.SetActive(false);
			Object.DontDestroyOnLoad(container);
		}

		internal static Task<object> Register(ServiceInfo serviceInfo) // TODO: Add overload accepting an array and support initializing services in optimal order automatically
		{
			if (!serviceInfo.LazyInit && !serviceInfo.IsTransient)
			{
				return LazyInit(serviceInfo, serviceInfo.definingTypes.FirstOrDefault() ?? serviceInfo.concreteType, client: null);
			}

			#if DEV_MODE && DEBUG_LAZY_INIT
			Debug.Log($"Will not initialize {TypeUtility.ToString(serviceInfo.ConcreteOrDefiningType)} yet because it LazyInit is True.");
			#endif

			if(serviceInfo.concreteType is { } concreteType)
			{
				var key = concreteType.ContainsGenericParameters ? concreteType.GetGenericTypeDefinition() : concreteType;
				uninitializedServices[key] = serviceInfo;
			}

			foreach(var definingType in serviceInfo.definingTypes)
			{
				var key = definingType.ContainsGenericParameters ? definingType.GetGenericTypeDefinition() : definingType;
				uninitializedServices[key] = serviceInfo;
			}

			return Task.FromResult(default(object));
		}

		private static void InitializeServices(List<ServiceInfo> globalServiceInfos, HashSet<Type> initialized, [DisallowNull] LocalServices localServices)
		{
			foreach(var serviceInfo in globalServiceInfos)
			{
				if(serviceInfo.LazyInit)
				{
					#if DEV_MODE && DEBUG_LAZY_INIT
					Debug.Log($"Will not initialize {TypeUtility.ToString(serviceInfo.ConcreteOrDefiningType)} yet because LazyInit is True.");
					#endif

					if(serviceInfo.concreteType is { } concreteType)
					{
						var key = concreteType.ContainsGenericParameters ? concreteType.GetGenericTypeDefinition() : concreteType;
						uninitializedServices[key] = serviceInfo;
					}

					foreach(var definingType in serviceInfo.definingTypes)
					{
						var key = definingType.ContainsGenericParameters ? definingType.GetGenericTypeDefinition() : definingType;
						uninitializedServices[key] = serviceInfo;
					}

					continue;
				}

				#if DEV_MODE || DEBUG || INIT_ARGS_SAFE_MODE
				var failedServiceInfo = serviceInfo;
				#endif

				#if !DEV_MODE && !DEBUG && !INIT_ARGS_SAFE_MODE
				_ =
				#endif
				GetOrInitializeService(serviceInfo, initialized, localServices, closedConcreteType: null, requestedServiceType: null, client: null)
				#if DEV_MODE || DEBUG || INIT_ARGS_SAFE_MODE
				.OnFailure(task => OnGetOrInitializeServiceFailed(task.Exception, serviceInfo))
				#endif
				;
			}
		}

		/// <param name="serviceInfo"></param>
		/// <param name="requestedServiceType"> The type of the initialization argument being requested for the client. Could be abstract. </param>
		/// <param name="client"></param>
		internal static async Task<object> LazyInit([DisallowNull] ServiceInfo serviceInfo, [DisallowNull] Type requestedServiceType, [MaybeNull] Component client)
		{
			#if DEV_MODE || DEBUG || INIT_ARGS_SAFE_MODE
			if(requestedServiceType.ContainsGenericParameters)
			{
				Debug.LogError($"LazyInit called with {nameof(requestedServiceType)} {TypeUtility.ToString(requestedServiceType)} that was a generic type definition or had open generic type arguments. This should not happen.");
				return null;
			}
			#endif

			// Maybe null if value provider / service initializer
			var closedConcreteType = GetConcreteAndClosedType(serviceInfo, requestedServiceType);

			#if DEV_MODE && UNITY_ASSERTIONS
			if(closedConcreteType is not null)
			{
				if(closedConcreteType.IsAbstract) { Debug.LogAssertion($"GetConcreteAndClosedType result {TypeUtility.ToString(closedConcreteType)} was abstract."); }
				if(closedConcreteType.ContainsGenericParameters) { Debug.LogAssertion($"GetConcreteAndClosedType result {TypeUtility.ToString(closedConcreteType)} contained open generic types."); }
			}
			else if(serviceInfo.serviceProviderType is ServiceProviderType.None) { Debug.LogWarning($"LazyInit({TypeUtility.ToString(serviceInfo.ConcreteOrDefiningType)} as {TypeUtility.ToString(requestedServiceType)}) called but could not determine {nameof(closedConcreteType)} for service.", GetContext(serviceInfo)); }
			#endif

			var initialized = new HashSet<Type>(0);
			var localServices = new LocalServices();

			// If service has already been initialized, no need to do anything.
			if(closedConcreteType is not null && services.TryGetValue(closedConcreteType, out var service))
			{
				#if DEV_MODE && UNITY_ASSERTIONS
				if(serviceInfo.IsTransient) { Debug.LogAssertion($"Service {TypeUtility.ToString(closedConcreteType)} is transient, yet an object of type {TypeUtility.ToString(service.GetType())} had been cached."); }
				#endif
				service = await GetServiceFromInstance(closedConcreteType: closedConcreteType, instance: service, serviceInfo, initialized, localServices, client: client);
				OnAfterAwait(serviceInfo.ConcreteOrDefiningType);
				return service;
			}

			#if DEV_MODE
			Debug.Assert(TryGetUninitializedServiceInfo(serviceInfo.ConcreteOrDefiningType, out _), closedConcreteType);
			#endif

			HandleRemoveFromUninitializedServices(serviceInfo);

			#if DEV_MODE && DEBUG_LAZY_INIT
			Debug.Log($"LazyInit serviceInfo.concreteType:{TypeUtility.ToString(serviceInfo.concreteType)} requestedServiceType:{TypeUtility.ToString(requestedServiceType)}, closedConcreteType:{TypeUtility.ToString(closedConcreteType)}, IsTransient:{serviceInfo.IsTransient}, serviceProviderType:{serviceInfo.serviceProviderType}");
			#endif

			#if DEV_MODE || DEBUG || INIT_ARGS_SAFE_MODE
			try 
			{
			#endif

			service = await GetOrInitializeService(serviceInfo, initialized, localServices, closedConcreteType: closedConcreteType, requestedServiceType: requestedServiceType, client: client);
			OnAfterAwait(serviceInfo);

			#if DEV_MODE || DEBUG || INIT_ARGS_SAFE_MODE
			}
			catch(ServiceInitFailedException)
			{
				throw;
			}
			catch(Exception exception) when (exception is not OperationCanceledException)
			{
				throw ServiceInitFailedException.Create(serviceInfo, ServiceInitFailReason.ExceptionWasThrown, exception: exception, concreteType: closedConcreteType, localServices: localServices);
			}
			#endif

			#if DEV_MODE
			Debug.Assert(service is not Task);
			#endif

			#if UNITY_EDITOR
			if(container && container.TryGetComponent(out ServicesDebugger debugger))
			{
				_ = debugger.SetServices(services.Values.Distinct());
			}
			#endif

			await InjectCrossServiceDependencies(service, serviceInfo, initialized, localServices);
			OnAfterAwait(serviceInfo);

			if(!serviceInfo.FindFromScene)
			{
				await HandleExecutingEventFunctionsFor(service);
			}

			#if DEV_MODE && DEBUG_LAZY_INIT
			Debug.Log($"LazyInit {TypeUtility.ToString(requestedServiceType)} result: {TypeUtility.ToString(service?.GetType())}");
			#endif

			return service;
		}

		[return: MaybeNull]
		private static Type GetConcreteAndClosedType([DisallowNull] ServiceInfo serviceInfo, [AllowNull] Type requestedServiceType)
		{
			#if DEV_MODE && UNITY_ASSERTIONS
			if(requestedServiceType is { ContainsGenericParameters: true }) { Debug.LogAssertion($"{nameof(requestedServiceType)} {TypeUtility.ToString(requestedServiceType)} contained open generic parameters."); }
			#endif

			if(serviceInfo.concreteType is not { } concreteType)
			{
				if(requestedServiceType is null)
				{
					return null;
				}

				if(!requestedServiceType.IsAbstract)
				{
					return requestedServiceType;
				}

				#if DEV_MODE
				Debug.Log($"Trying to determine ConcreteAndClosedType for {TypeUtility.ToString(requestedServiceType)} based on the types that implemented the requested interface type, because serviceInfo.concreteType was null...");
				#endif

				var result = TypeUtility.GetDerivedTypes(requestedServiceType)
						.SingleOrDefaultNoException(t => !t.IsAbstract);

				#if DEV_MODE
				Debug.Log($"ConcreteAndClosedType for {TypeUtility.ToString(requestedServiceType)} based on the types that implemented the requested interface type: {TypeUtility.ToString(result)}");
				#endif

				return result;
			}

			#if DEV_MODE && UNITY_ASSERTIONS
			if(concreteType.IsAbstract) { Debug.LogAssertion($"GetConcreteAndClosedType result {TypeUtility.ToString(concreteType)} was abstract."); }
			#endif
			
			if(!concreteType.ContainsGenericParameters || requestedServiceType is null)
			{
				return concreteType;
			}
			
			if(!requestedServiceType.IsAbstract)
			{
				return requestedServiceType;
			}

			var concreteTypeDefinition = concreteType.GetGenericTypeDefinition();

			// E.g. concreteType Logger<> + requested type ILogger<Player> => Logger<Player>
			// E.g. concreteType List<> + requested type IEnumerable<Player> => List<Player>
			int requiredGenericArgumentCount = concreteType.GetGenericArguments().Length;
			for(var requestedTypeOrBaseType = requestedServiceType; requestedTypeOrBaseType != null; requestedTypeOrBaseType = requestedTypeOrBaseType.BaseType)
			{
				if(!requestedTypeOrBaseType.IsGenericType)
				{
					continue;
				}

				var genericArguments = requestedTypeOrBaseType.GetGenericArguments();
				if(genericArguments.Length != requiredGenericArgumentCount)
				{
					continue;
				}

				var closedConcreteType = concreteTypeDefinition.MakeGenericType(genericArguments);
				if(requestedServiceType.IsAssignableFrom(closedConcreteType))
				{
					return closedConcreteType;
				}
			}

			if(!requestedServiceType.IsInterface)
			{
				foreach(var interfaceType in requestedServiceType.GetInterfaces())
				{
					if(!interfaceType.IsGenericType)
					{
						continue;
					}

					var genericArguments = interfaceType.GetGenericArguments();
					if(genericArguments.Length != requiredGenericArgumentCount)
					{
						continue;
					}

					var closedConcreteType = concreteTypeDefinition.MakeGenericType(genericArguments);
					if(requestedServiceType.IsAssignableFrom(closedConcreteType))
					{
						return closedConcreteType;
					}
				}
			}

			throw ServiceInitFailedException.Create(serviceInfo, ServiceInitFailReason.UnresolveableConcreteType);
		}

		/// <summary>
		/// Gets or creates, caches and finalizes a service.
		/// </summary>
		private static async Task<object> GetOrInitializeService(ServiceInfo serviceInfo, HashSet<Type> initialized, [DisallowNull] LocalServices localServices, [AllowNull] Type closedConcreteType, [AllowNull] Type requestedServiceType, [MaybeNull] Component client)
		{
			closedConcreteType ??= GetConcreteAndClosedType(serviceInfo, requestedServiceType);

			if(closedConcreteType is null)
			{
				if(serviceInfo.serviceProviderType is ServiceProviderType.None)
				{
					throw ServiceInitFailedException.Create(serviceInfo, ServiceInitFailReason.UnresolveableConcreteType);
				}
			}
			else if(services.TryGetValue(closedConcreteType, out var existingInstance))
			{
				if(closedConcreteType.IsInstanceOfType(existingInstance))
				{
					return existingInstance;
				}

				if(existingInstance is Task existingTask)
				{
					existingInstance = await existingTask.GetResult();
					OnAfterAwait(closedConcreteType);
					if(closedConcreteType.IsInstanceOfType(existingInstance))
					{
						return existingInstance;
					}
				}

				switch(serviceInfo.serviceProviderType)
				{
					case ServiceProviderType.ServiceInitializer:
					case ServiceProviderType.IValueProviderT:
					case ServiceProviderType.IValueByTypeProvider:
					case ServiceProviderType.IValueProvider:
						if(ValueProviderUtility.TryGetValueProviderValue(existingInstance, closedConcreteType, client, out var providedValue))
						{
							OnAfterGetValueFromValueProvider(existingInstance, client, providedValue);
							#if DEV_MODE && DEBUG_CREATE_SERVICES
							Debug.Log($"Service created via {serviceInfo.serviceProviderType} {TypeUtility.ToString(existingInstance.GetType())}: {TypeUtility.ToString(providedValue?.GetType())}");
							#endif
							return providedValue;
						}
						break;
					case ServiceProviderType.ServiceInitializerAsync:
					case ServiceProviderType.IValueProviderAsyncT:
					case ServiceProviderType.IValueByTypeProviderAsync:
					case ServiceProviderType.IValueProviderAsync:
						var asyncProvidedValue = await ValueProviderUtility.GetValueProviderValueAsync(existingInstance, closedConcreteType, client);
						OnAfterGetValueFromAsyncValueProvider(existingInstance, client, asyncProvidedValue);
						return asyncProvidedValue;
				}
			}

			foreach(var definingType in serviceInfo.definingTypes)
			{
				if(!services.TryGetValue(definingType, out var existingInstance))
				{
					continue;
				}

				if(definingType.IsInstanceOfType(existingInstance))
				{
					return existingInstance;
				}

				if(existingInstance is Task existingTask)
				{
					existingInstance = await existingTask.GetResult();
					OnAfterAwait(closedConcreteType);

					if(definingType.IsInstanceOfType(existingInstance))
					{
						return existingInstance;
					}
				}

				return existingInstance;
			}

			Task<object> task;
			#if DEV_MODE || DEBUG || INIT_ARGS_SAFE_MODE
			try
			{
			#endif

			task = InitializeServiceAsync(closedConcreteType, serviceInfo.serviceProviderType, serviceInfo.loadMethod,  serviceInfo.referenceType, serviceInfo, initialized, localServices, requestedServiceType: requestedServiceType, client: client);

			#if DEV_MODE || DEBUG || INIT_ARGS_SAFE_MODE
			}
			catch(ServiceInitFailedException)
			{
				throw;
			}
			catch(Exception e)
			{
				throw ServiceInitFailedException.Create(serviceInfo, ServiceInitFailReason.ExceptionWasThrown, exception: e, concreteType: closedConcreteType, localServices: localServices);
			}
			#endif

			if(!serviceInfo.IsTransient)
			{
				if(closedConcreteType is not null)
				{
					services[closedConcreteType] = task;
				}

				foreach(var definingType in serviceInfo.definingTypes)
				{
					if(!definingType.ContainsGenericParameters)
					{
						services[definingType] = task;
					}
				}
			}

			var result = await task;
			OnAfterAwait(closedConcreteType);
			
			if(result is Task chainedTask)
			{
				result = await chainedTask.GetResult();
				OnAfterAwait(closedConcreteType);
			}

			#if DEV_MODE || DEBUG || INIT_ARGS_SAFE_MODE
			if(result is null)
			{
				#if DEV_MODE
				Debug.LogError($"GetOrCreateInstance(concreteType:{TypeUtility.ToString(closedConcreteType)}, definingTypes:{TypeUtility.ToString(serviceInfo.definingTypes)}) returned instance was null.", GetContext(serviceInfo));
				#endif
				return null;
			}
			#endif

			#if DEV_MODE
			if(!serviceInfo.IsInstanceOf(result) && !serviceInfo.IsValueProvider) { Debug.LogWarning($"!serviceInfo.IsValueProvider && !{TypeUtility.ToString(serviceInfo.definingTypes.FirstOrDefault())}.IsInstanceOf({TypeUtility.ToString(result.GetType())}", GetContext(serviceInfo)); }
			#endif

			FinalizeServiceImmediate(serviceInfo, result);
			return result;
		}

		private static async Task<object> InitializeServiceAsync(Type closedConcreteType, ServiceProviderType serviceProviderType, LoadMethod loadMethod, ReferenceType referenceType, [DisallowNull] ServiceInfo serviceInfo, [DisallowNull] HashSet<Type> initialized, [DisallowNull] LocalServices localServices, [MaybeNull] Type requestedServiceType, [MaybeNull] Component client)
		{
			object result;

			switch(serviceProviderType)
			{
				case ServiceProviderType.ServiceInitializer:
					if(initialized.Contains(closedConcreteType))
					{
						throw ServiceInitFailedException.Create(serviceInfo, ServiceInitFailReason.CircularDependencies);
					}

					var serviceInitializer = (IServiceInitializer) await GetOrCreateServiceProvider(serviceInfo, initialized, localServices, requestedServiceType: requestedServiceType, serviceClosedConcreteType: closedConcreteType, client: client);
					OnAfterAwait(closedConcreteType);

					result = await CreateUsingServiceInitializer(closedConcreteType, serviceInfo, initialized, localServices, serviceInitializer, loadMethod, referenceType, requestedServiceType, client: client);
					OnAfterAwait(closedConcreteType);

					if(result is not null)
					{
						return result;
					}

					break;
				case ServiceProviderType.ServiceInitializerAsync:
					#if DEV_MODE || DEBUG || INIT_ARGS_SAFE_MODE
					if(initialized.Contains(closedConcreteType))
					{
						throw ServiceInitFailedException.Create(serviceInfo, ServiceInitFailReason.CircularDependencies);
					}
					#endif

					var serviceInitializerAsync = (IServiceInitializerAsync) await GetOrCreateServiceProvider(serviceInfo, initialized, localServices, requestedServiceType: requestedServiceType, serviceClosedConcreteType: closedConcreteType, client: client);
					OnAfterAwait(closedConcreteType);

					result = await CreateUsingServiceInitializerAsync(closedConcreteType, serviceInfo, initialized, localServices, serviceInitializerAsync, client: client);
					OnAfterAwait(closedConcreteType);

					if(result is not null)
					{
						return result;
					}

					break;
				case ServiceProviderType.IValueProvider:
				case ServiceProviderType.IValueProviderAsync:
				case ServiceProviderType.IValueProviderT:
				case ServiceProviderType.IValueProviderAsyncT:
				case ServiceProviderType.IValueByTypeProvider:
				case ServiceProviderType.IValueByTypeProviderAsync:
					#if DEV_MODE || DEBUG || INIT_ARGS_SAFE_MODE
					if(initialized.Contains(closedConcreteType))
					{
						throw ServiceInitFailedException.Create(serviceInfo, ServiceInitFailReason.CircularDependencies);
					}
					#endif

					var serviceProvider = await GetOrCreateServiceProvider(serviceInfo, initialized, localServices, requestedServiceType: requestedServiceType, serviceClosedConcreteType: closedConcreteType, client: client);
					OnAfterAwait(closedConcreteType);

					result = await GetServiceFromInstance(closedConcreteType: closedConcreteType, instance:serviceProvider, serviceInfo, initialized, localServices, client: client);
					OnAfterAwait(closedConcreteType);

					#if DEV_MODE && DEBUG_CREATE_SERVICES
					Debug.Log($"Service {TypeUtility.ToString(result.GetType())} of type {TypeUtility.ToString(serviceInfo.definingTypes.FirstOrDefault())} successfully created using service provider {TypeUtility.ToString(serviceProvider.GetType())}.", GetContext(serviceInfo));
					#endif

					#if DEV_MODE && UNITY_ASSERTIONS
					if(result is null)
					{
						Debug.LogAssertion($"Service provider {TypeUtility.ToString(serviceProvider.GetType())} ({serviceInfo.serviceProviderType}) returned a null value for client {TypeUtility.ToString(client ? client.GetType() : null)}.", GetContext(serviceInfo));
					}
					else if(!serviceInfo.IsInstanceOf(result))
					{
						Debug.LogAssertion($"Service provider {TypeUtility.ToString(serviceProvider.GetType())} returned value of type {TypeUtility.ToString(result.GetType())} which was not assignable to all defining types of the service: {TypeUtility.ToString(serviceInfo.definingTypes)}.", GetContext(serviceInfo));
					}
					#endif

					return result;
			}

			if(loadMethod is LoadMethod.FindFromScene)
			{
				if(localServices.TryGet(null, closedConcreteType, out var serviceOrProvider))
				{
					if(closedConcreteType.IsInstanceOfType(serviceOrProvider))
					{
						return serviceOrProvider;
					}

					if(serviceOrProvider is Task existingTask)
					{
						serviceOrProvider = await existingTask.GetResult();
						OnAfterAwait(closedConcreteType);

						if(closedConcreteType.IsInstanceOfType(serviceOrProvider))
						{
							return serviceOrProvider;
						}
					}

					// Handle situation where [Service] attribute is attached to the service class directly,
					// but localServices.TryGet finds an Initializer, Wrapper etc., rather than the service instance directly.
					if(serviceInfo.serviceProviderType is ServiceProviderType.None)
					{
						if(serviceOrProvider is IInitializer initializer)
						{
							if(serviceInfo.LoadAsync)
							{
								result = await initializer.InitTargetAsync();
								OnAfterAwait(closedConcreteType);
							}
							else
							{
								result = initializer.InitTarget();
							}

							if(result is not null)
							{
								return result;
							}
						}
						else if(serviceOrProvider is IWrapper wrapper)
						{
							result = wrapper.WrappedObject;
							if(result is not null)
							{
								return result;
							}
						}
						else
						{
							result = await ValueProviderUtility.GetValueProviderValueAsync(serviceOrProvider, closedConcreteType, client: client);
							OnAfterGetValueFromValueProvider(serviceOrProvider, client, result);
							if(result is not null)
							{
								return result;
							}
						}
					}

					// NOTE: This is different from the serviceProviderType argument that is checked above.
					switch(serviceInfo.serviceProviderType)
					{
						case ServiceProviderType.ServiceInitializer:
							if(serviceOrProvider is IServiceInitializer serviceInitializer)
							{
								result = await CreateUsingServiceInitializer(closedConcreteType, serviceInfo, initialized, localServices, serviceInitializer, loadMethod, referenceType, requestedServiceType: requestedServiceType, client: client);
								OnAfterAwait(closedConcreteType);

								if(result is not null)
								{
									return result;
								}
							}
							
							break;
						case ServiceProviderType.ServiceInitializerAsync:
							if(serviceOrProvider is IServiceInitializerAsync serviceInitializerAsync)
							{
								result = await CreateUsingServiceInitializerAsync(closedConcreteType, serviceInfo, initialized, localServices, serviceInitializerAsync, client: client);
								OnAfterAwait(closedConcreteType);

								if(result is not null)
								{
									return result;
								}
							}
							
							break;
						case ServiceProviderType.IValueProviderT:
						case ServiceProviderType.IValueByTypeProvider:
						case ServiceProviderType.IValueProvider:
							if(ValueProviderUtility.TryGetValueProviderValue(serviceOrProvider, closedConcreteType, client: client, out var providedValue))
							{
								OnAfterGetValueFromValueProvider(serviceOrProvider, client, providedValue);
								return providedValue;
							}
							break;
						case ServiceProviderType.IValueProviderAsyncT:
						case ServiceProviderType.IValueByTypeProviderAsync:
						case ServiceProviderType.IValueProviderAsync:
							var asyncProvidedValue = await ValueProviderUtility.GetValueProviderValueAsync(serviceOrProvider, closedConcreteType, client: client);
							OnAfterGetValueFromAsyncValueProvider(serviceOrProvider, client, asyncProvidedValue);
							return asyncProvidedValue;
						case ServiceProviderType.Initializer:
							if(serviceOrProvider is IInitializer initializer)
							{
								if(serviceInfo.LoadAsync)
								{
									result = await initializer.InitTargetAsync();
									OnAfterAwait(closedConcreteType);
								}
								else
								{
									result = initializer.InitTarget();
								}

								if(result is not null)
								{
									return result;
								}
							}
							break;
						case ServiceProviderType.Wrapper:
							if(serviceOrProvider is IWrapper wrapper)
							{
								result = wrapper.WrappedObject;
								if(result is not null)
								{
									return result;
								}
							}
							break;
					}

					if(!serviceInfo.IsInstanceOf(serviceOrProvider))
					{
						throw ServiceInitFailedException.Create(serviceInfo, ServiceInitFailReason.ServiceProviderResultNotConvertible, asset: null, initializerOrWrapper: serviceOrProvider, concreteType: closedConcreteType, localServices: localServices);
					}

					return serviceOrProvider;
				}

				foreach(var definingType in serviceInfo.definingTypes)
				{
					if(localServices.TryGet(null, definingType, out result))
					{
						return result;
					}
				}

				if(serviceInfo.SceneName is { Length: > 0 } sceneName)
				{
					#if !DEV_MODE && !DEBUG && !INIT_ARGS_SAFE_MODE
					_ =
					#endif
					LoadDependenciesOfServicesInScene(sceneName, initialized, localServices)
					#if DEV_MODE || DEBUG || INIT_ARGS_SAFE_MODE
					.OnFailure(task => OnGetOrInitializeServiceFailed(task.Exception, serviceInfo))
					#endif
					;

					var scene = SceneManager.GetSceneByName(sceneName);
					if(!scene.IsValid())
					{
						if(serviceInfo.LoadAsync)
						{
							#if DEV_MODE && DEBUG_LOAD_SCENE
							Debug.Log($"Loading scene '{sceneName}' asynchronously to initialize service {TypeUtility.ToString(closedConcreteType)}...");
							#endif

							var asyncOperation = SceneManager.LoadSceneAsync(sceneName, LoadSceneMode.Additive); 
							#if UNITY_6000_0_OR_NEWER
							await asyncOperation;
							#else
							while(!asyncOperation.isDone)
							{
								await Task.Yield();
							}
							#endif
							OnAfterAwait(closedConcreteType);
						}
						else
						{
							#if DEV_MODE && DEBUG_LOAD_SCENE
							Debug.Log($"Loading scene '{sceneName}' to initialize service {TypeUtility.ToString(closedConcreteType)}...");
							#endif

							#if UNITY_EDITOR
							if(!serviceInfo.IsSceneIncludedInBuild
								&& AssetDatabase.FindAssets($"t:scene {sceneName}")
												.Select(AssetDatabase.GUIDToAssetPath)
												.FirstOrDefault(x => string.Equals(sceneName, Path.GetFileNameWithoutExtension(x))) is { Length: > 0 } scenePath)
							{
								Debug.LogWarning($"The scene '{sceneName}' containing the global service {TypeUtility.ToString(closedConcreteType ?? serviceInfo.ConcreteOrDefiningType)} is not included in the active build profile or shared scene list. Add the scene to the scene list if you want it to be included in builds.", GetContext(serviceInfo));
								EditorSceneManager.LoadSceneInPlayMode(scenePath, new(LoadSceneMode.Additive));
							}
							else
							#endif
							{
								SceneManager.LoadScene(sceneName, LoadSceneMode.Additive);
							}

							scene = SceneManager.GetSceneByName(sceneName);

							#if DEV_MODE && DEBUG_LOAD_SCENE
							Debug.Log($"scene '{scene.name}' IsValid:{scene.IsValid()}, isLoaded:{scene.isLoaded}...");
							#endif
						}
					}

					#if DEV_MODE && DEBUG_LOAD_SCENE
					var needToLoadScene = !scene.isLoaded; 
					if(needToLoadScene) Debug.Log($"Waiting for scene '{scene.name}' to finish loading before initializing service {TypeUtility.ToString(closedConcreteType ?? serviceInfo.ConcreteOrDefiningType)}...");
					else Debug.Log($"Scene '{scene.name}' already loaded. Can continue initializing service {TypeUtility.ToString(closedConcreteType ?? serviceInfo.ConcreteOrDefiningType)}...");
					#endif

					await WaitForSceneToLoad(scene);

					#if DEV_MODE && DEBUG_LOAD_SCENE
					if(needToLoadScene) Debug.Log($"Finished waiting for scene '{scene.name}' to finish loading. Moving on to initialize service {TypeUtility.ToString(closedConcreteType ?? serviceInfo.ConcreteOrDefiningType)}...");
					#endif

					OnAfterAwait(closedConcreteType);
				}
				else if(serviceInfo.SceneBuildIndex is var sceneBuildIndex and not -1)
				{
					#if DEV_MODE && DEBUG_LOAD_SCENE
					Debug.Log($"Executing LoadDependenciesOfServicesInScene({sceneBuildIndex})...");
					#endif

					#if !DEV_MODE && !DEBUG && !INIT_ARGS_SAFE_MODE
					_ =
					#endif
					LoadDependenciesOfServicesInScene(sceneBuildIndex, initialized, localServices)
					#if DEV_MODE || DEBUG || INIT_ARGS_SAFE_MODE
					.OnFailure(task => OnGetOrInitializeServiceFailed(task.Exception, serviceInfo))
					#endif
					;

					var scene = SceneManager.GetSceneByBuildIndex(sceneBuildIndex);
					if(!scene.IsValid())
					{
						if(serviceInfo.LoadAsync)
						{
							#if DEV_MODE && DEBUG_LOAD_SCENE
							Debug.Log($"Loading scene #{sceneBuildIndex} asynchronously to initialize service {TypeUtility.ToString(closedConcreteType)}...");
							#endif

							var asyncOperation = SceneManager.LoadSceneAsync(sceneBuildIndex, LoadSceneMode.Additive);
							#if UNITY_6000_0_OR_NEWER
							await asyncOperation;
							#else
							while(!asyncOperation.isDone)
							{
								await Task.Yield();
							}
							#endif
							OnAfterAwait(closedConcreteType);
						}
						else
						{
							#if DEV_MODE && DEBUG_LOAD_SCENE
							Debug.Log($"Loading scene #{sceneBuildIndex} to initialize service {TypeUtility.ToString(closedConcreteType)}...");
							#endif

							SceneManager.LoadScene(sceneBuildIndex, LoadSceneMode.Additive);
							scene = SceneManager.GetSceneByBuildIndex(sceneBuildIndex);
						}
					}

					#if DEV_MODE && DEBUG_LOAD_SCENE
					var needToLoadScene = !scene.isLoaded;
					if(needToLoadScene) Debug.Log($"Waiting for scene '{scene.name}' to finish loading before initializing service {TypeUtility.ToString(closedConcreteType ?? serviceInfo.ConcreteOrDefiningType)}...");
					else Debug.Log($"Scene '{scene.name}' already loaded. Can continue initializing service {TypeUtility.ToString(closedConcreteType ?? serviceInfo.ConcreteOrDefiningType)}...");
					#endif

					await WaitForSceneToLoad(scene);

					#if DEV_MODE && DEBUG_LOAD_SCENE
					if(needToLoadScene) Debug.Log($"Finished waiting for scene '{scene.name}' to finish loading. Moving on to initialize service {TypeUtility.ToString(closedConcreteType ?? serviceInfo.ConcreteOrDefiningType)}...");
					#endif
					
					OnAfterAwait(closedConcreteType);
				}

				if(typeof(Component).IsAssignableFrom(closedConcreteType))
				{
					result =
					#if UNITY_2023_1_OR_NEWER
					Object.FindAnyObjectByType(closedConcreteType, FindObjectsInactive.Include);
					#else
					Object.FindObjectOfType(closedConcreteType, true);
					#endif

					#if DEV_MODE && DEBUG_CREATE_SERVICES
					if(result is Object unityObject && unityObject) { Debug.Log($"Service {TypeUtility.ToString(result.GetType())} of type {TypeUtility.ToString(serviceInfo.definingTypes.FirstOrDefault())} retrieved from scene successfully.", result as Object); }
					#endif
				}
				else if(typeof(Component).IsAssignableFrom(serviceInfo.classWithAttribute))
				{
					result =
					#if UNITY_2023_1_OR_NEWER
					Object.FindAnyObjectByType(serviceInfo.classWithAttribute, FindObjectsInactive.Include);
					#else
					Object.FindObjectOfType(serviceInfo.classWithAttribute, true);
					#endif

					#if DEV_MODE && DEBUG_CREATE_SERVICES
					if(result is Object unityObject && unityObject) { Debug.Log($"Service {TypeUtility.ToString(result.GetType())} of type {TypeUtility.ToString(serviceInfo.definingTypes.FirstOrDefault())} retrieved from scene successfully.", result as Object); }
					#endif
				}
				else if(!Find.Any(closedConcreteType, out result, includeInactive: true))
				{
					result = Find.Any(serviceInfo.classWithAttribute, true);

					#if DEV_MODE && DEBUG_CREATE_SERVICES
					if(result is not null) { Debug.Log($"Service {TypeUtility.ToString(result.GetType())} of type {TypeUtility.ToString(serviceInfo.definingTypes.FirstOrDefault())} retrieved from scene successfully.", result as Object); }
					#endif
				}

				if(result is not null)
				{
					if(!serviceInfo.IsInstanceOf(result)
						&& result is IInitializer initializerWithAttribute
						&& TargetIsAssignableOrConvertibleToType(initializerWithAttribute, serviceInfo))
					{
						#if DEV_MODE || DEBUG || INIT_ARGS_SAFE_MODE
						try
						{
						#endif

						#if DEV_MODE && DEBUG_CREATE_SERVICES
						Debug.Log($"Initializing service {TypeUtility.ToString(closedConcreteType)} using initializer {TypeUtility.ToString(initializerWithAttribute.GetType())} found in scene.", GetContext(serviceInfo));
						#endif

						result = await initializerWithAttribute.InitTargetAsync();

						#if DEV_MODE || DEBUG || INIT_ARGS_SAFE_MODE
						}
						catch(Exception exception) when (exception is not OperationCanceledException)
						{
							throw ServiceInitFailedException.Create(serviceInfo, ServiceInitFailReason.InitializerThrewException, exception: exception, initializerOrWrapper: initializerWithAttribute, concreteType: closedConcreteType, localServices: localServices);
						}
						#endif

						OnAfterAwait(closedConcreteType);

						#if UNITY_EDITOR
						if(!Application.isPlaying)
						{
							throw new TaskCanceledException($"Abort async initialization of async service {TypeUtility.ToString(closedConcreteType)} via {TypeUtility.ToString(initializerWithAttribute.GetType())} because no longer in Play Mode.");
						}
						#endif

						if(serviceInfo.IsInstanceOf(result))
						{
							#if DEV_MODE && DEBUG_CREATE_SERVICES
							Debug.Log($"Service {TypeUtility.ToString(result.GetType())} of type {TypeUtility.ToString(serviceInfo.definingTypes.FirstOrDefault())} retrieved from scene successfully.", result as Object);
							#endif
							return result;
						}
					}

					if(serviceInfo.DontDestroyOnLoad)
					{
						TryMakeDontDestroyOnLoad(result);
					}

					return result;
				}

				#if DEV_MODE || DEBUG || INIT_ARGS_SAFE_MODE
				if(typeof(ScriptableObject).IsAssignableFrom(closedConcreteType))
				{
					throw ServiceInitFailedException.Create(serviceInfo, ServiceInitFailReason.ScriptableObjectWithFindFromScene, asset: null, concreteType: closedConcreteType, localServices: localServices);
				}
				#endif

				#if UNITY_EDITOR
				if(!IsFirstSceneInBuildSettingsLoaded() && serviceInfo is not { LazyInit : true }) { return null; }
				#endif

				Debug.LogWarning($"Service Not Found: There is no '{TypeUtility.ToString(closedConcreteType)}' found in the active scene {SceneManager.GetActiveScene().name}, but the service class has the {nameof(ServiceAttribute)} with {nameof(ServiceAttribute.FindFromScene)} set to true. Either add an instance to the scene or don't set {nameof(ServiceAttribute.FindFromScene)} true to have a new instance be created automatically.", GetContext(serviceInfo));
				return null;
			}

			if(referenceType is ReferenceType.ResourcePath)
			{
				var resourcePath = serviceInfo.ResourcePath;
				Object asset;
				if(serviceInfo.LoadAsync)
				{
  					ResourceRequest resourceRequest = Resources.LoadAsync<Object>(resourcePath);
					#if UNITY_2023_2_OR_NEWER
					await resourceRequest;
					#else
					while(!resourceRequest.isDone)
					{
						await Task.Yield();
					}
					#endif
					OnAfterAwait(closedConcreteType);

					asset = resourceRequest.asset;
				}
				else
				{
					asset = Resources.Load<Object>(resourcePath);
				}

				#if DEBUG || INIT_ARGS_SAFE_MODE
				if(!asset)
				{
					throw ServiceInitFailedException.Create(serviceInfo, ServiceInitFailReason.MissingResource, asset: null, concreteType: closedConcreteType, localServices: localServices);
				}
				#endif

				if(asset is GameObject gameObject)
				{
					if(serviceInfo.ShouldInstantiate(true))
					{
						result = await InstantiateFromAsset(closedConcreteType, gameObject, serviceInfo, initialized, localServices, client: client);

						#if DEBUG || INIT_ARGS_SAFE_MODE
						if(result is null)
						{
							throw ServiceInitFailedException.Create(serviceInfo, ServiceInitFailReason.MissingComponent, asset: asset, concreteType: closedConcreteType, localServices: localServices);
						}
						#endif

						#if DEV_MODE && DEBUG_CREATE_SERVICES
						Debug.Log($"Service {TypeUtility.ToString(serviceInfo.ConcreteOrDefiningType)} instantiated from prefab at path 'Resources/{resourcePath}' successfully.", asset);
						#endif
					}
					else
					{
						result = await GetServiceFromInstance(closedConcreteType, gameObject, serviceInfo, client: client);

						#if DEBUG || INIT_ARGS_SAFE_MODE
						if(result is null)
						{
							Debug.LogWarning($"Service Not Found: No service of type {TypeUtility.ToString(serviceInfo.ConcreteOrDefiningType)} was found on the resource at path 'Resources/{resourcePath}'.", asset);
							return null;
						}
						#endif

						#if DEV_MODE && DEBUG_CREATE_SERVICES
						Debug.Log($"Service {TypeUtility.ToString(serviceInfo.ConcreteOrDefiningType)} loaded from prefab at path 'Resources/{resourcePath}' successfully.", asset);
						#endif
					}
				}
				else if(asset is ScriptableObject scriptableObject)
				{
					if(serviceInfo.ShouldInstantiate(false))
					{
						result = await InstantiateFromAsset(closedConcreteType, scriptableObject, serviceInfo, initialized, localServices, client: client);

						#if DEBUG || INIT_ARGS_SAFE_MODE
						if(result is null)
						{
							Debug.LogWarning($"Service Not Found: No service of type {TypeUtility.ToString(serviceInfo.ConcreteOrDefiningType)} was found on the clone created from the resource at path 'Resources/{resourcePath}'.", asset);
							return null;
						}
						#endif

						#if DEV_MODE && DEBUG_CREATE_SERVICES
						Debug.Log($"Service {TypeUtility.ToString(serviceInfo.ConcreteOrDefiningType)} instantiated from scriptable object at path 'Resources/{resourcePath}' successfully.", asset);
						#endif
					}
					else
					{
						result = await GetServiceAsync(closedConcreteType, scriptableObject, serviceInfo, client: client);

						#if DEBUG || INIT_ARGS_SAFE_MODE
						if(result is null)
						{
							Debug.LogWarning($"Service Not Found: No service of type {TypeUtility.ToString(serviceInfo.ConcreteOrDefiningType)} was found on the resource at path 'Resources/{resourcePath}'.", asset);
							return null;
						}
						#endif

						#if DEV_MODE && DEBUG_CREATE_SERVICES
						Debug.Log($"Service {TypeUtility.ToString(closedConcreteType)} loaded from scriptable object at path 'Resources/{resourcePath}' successfully.", asset);
						#endif
					}
				}
				else if(serviceInfo.IsInstanceOf(asset))
				{
					result = asset;

					#if DEV_MODE && DEBUG_CREATE_SERVICES
					Debug.Log($"Service {TypeUtility.ToString(serviceInfo.ConcreteOrDefiningType)} loaded from asset at path 'Resources/{resourcePath}' successfully.", asset);
					#endif
				}
				else
				{
					Debug.LogWarning($"Service Not Found: Resource at path 'Resources/{resourcePath}' could not be converted to type {TypeUtility.ToString(serviceInfo.definingTypes.FirstOrDefault())}.", asset);
					return null;
				}

				#if DEBUG || INIT_ARGS_SAFE_MODE
				if(result is null)
				{
					Debug.LogWarning($"Service Not Found: No service of type {TypeUtility.ToString(serviceInfo.ConcreteOrDefiningType)} was found on the clone created from the resource at path 'Resources/{resourcePath}'.", asset);
					return null;
				}
				#endif

				return result;
			}

			#if UNITY_ADDRESSABLES_1_17_4_OR_NEWER
			if(referenceType is ReferenceType.AddressableKey)
			{
				return await InitializeAddressableAsset(closedConcreteType, serviceInfo.AddressableKey, serviceInfo, initialized, localServices, client: client);
			}
			#endif

			if(typeof(Component).IsAssignableFrom(closedConcreteType))
			{
				if(!container)
				{
					CreateServicesContainer();
				}

				if(ShouldInitialize(closedConcreteType))
				{
					result = await AddComponent(serviceInfo, initialized, localServices, client: client);

					#if DEBUG || INIT_ARGS_SAFE_MODE
					if(result is null)
					{
						Debug.LogWarning($"Service Initialization Failed: Failed to attach service of type {TypeUtility.ToString(serviceInfo.ConcreteOrDefiningType)} to '{container.name}'.", GetContext(serviceInfo));
						return null;
					}
					#endif
					
					#if DEV_MODE && DEBUG_CREATE_SERVICES
					Debug.Log($"Service {TypeUtility.ToString(serviceInfo.ConcreteOrDefiningType)} attached to '{container.name}' successfully.", container);
					#endif
				}
				else
				{
					result = container.AddComponent(closedConcreteType);

					#if DEBUG || INIT_ARGS_SAFE_MODE
					if(result is null)
					{
						Debug.LogWarning($"Service Initialization Failed: Failed to attach service of type {TypeUtility.ToString(serviceInfo.ConcreteOrDefiningType)} to '{container.name}'.", GetContext(serviceInfo));
						return null;
					}
					#endif

					#if DEV_MODE && DEBUG_CREATE_SERVICES
					Debug.Log($"Service {TypeUtility.ToString(serviceInfo.ConcreteOrDefiningType)} attached to '{container.name}' successfully.", container);
					#endif
				}

				return result;
			}

			if(typeof(ScriptableObject).IsAssignableFrom(closedConcreteType))
			{
				var scriptableObject = ScriptableObject.CreateInstance(closedConcreteType);
				#if UNITY_EDITOR
				disposables.Add(new ScriptableObjectDestroyer(scriptableObject));
				#endif

				#if DEV_MODE && DEBUG_CREATE_SERVICES
				Debug.Log($"ScriptableObject service {TypeUtility.ToString(closedConcreteType)} created successfully.");
				#endif

				return scriptableObject;
			}

			#if DEV_MODE || DEBUG || INIT_ARGS_SAFE_MODE
			if(initialized.Contains(closedConcreteType))
			{
				throw ServiceInitFailedException.Create(serviceInfo, ServiceInitFailReason.CircularDependencies, asset: null, sceneObject:client, concreteType: closedConcreteType, localServices: localServices);
			}
			#endif

			#if UNITY_ENTITIES && !INIT_ARGS_DISABLE_ECS_SUPPORT
			if(serviceInfo.IsSystem)
			{
				if(typeof(ISystem).IsAssignableFrom(closedConcreteType))
				{
					return await InitializeManagedSystemAsync(serviceInfo, initialized, localServices, closedConcreteType, client: client);
				}

				if(typeof(ComponentSystemBase).IsAssignableFrom(closedConcreteType))
				{
					return await InitializeUnmanagedSystemAsync(serviceInfo, initialized, localServices, closedConcreteType, client: client);
				}
			}

			if(serviceInfo.IsWorld && closedConcreteType == typeof(World))
			{
				#if DEV_MODE && DEBUG_INIT_WORLDS
				Debug.Log($"Creating World with name: {(serviceInfo.WorldName is { Length: > 0 } x ? x : closedConcreteType.Name)}");
				#endif
				var world =  new World(serviceInfo.WorldName is { Length: > 0 } worldName ? worldName : closedConcreteType.Name);
				world.CreateSystem<InitializationSystemGroup>();
				world.CreateSystem<SimulationSystemGroup>();
				world.CreateSystem<PresentationSystemGroup>();
				ScriptBehaviourUpdateOrder.AppendWorldToCurrentPlayerLoop(world);
				return world;
			}
			#endif

			{
				initialized.Add(closedConcreteType);

				var (constructor, arguments) = await GetConstructorAndArguments(closedConcreteType, serviceInfo, initialized, localServices, client: client);

				// If there are no constructor arguments, then the object still needs cross-scene dependencies to be injected to it.
				if(arguments.Length is 0)
				{
					initialized.Remove(closedConcreteType);
				}

				return Construct(constructor, null, closedConcreteType, arguments, serviceInfo, client: client);
			}
		}

		private static Task WaitForSceneToLoad(Scene scene)
		{
			if(scene.isLoaded)
			{
				return Task.CompletedTask;
			}

			var taskCompletionSource = new TaskCompletionSource<bool>();
			SceneManager.sceneLoaded += OnSceneLoaded;
			return taskCompletionSource.Task;

			void OnSceneLoaded(Scene loadedScene, LoadSceneMode loadSceneMode)
			{
				if(loadedScene == scene)
				{
					#if DEV_MODE && DEBUG_LOAD_SCENE
					Debug.Log($"Scene '{scene.name}' has finished loading.");
					#endif

					SceneManager.sceneLoaded -= OnSceneLoaded;
					taskCompletionSource.SetResult(true);
				}
				else if(!scene.IsValid())
				{
					#if DEV_MODE && DEBUG_LOAD_SCENE
					Debug.Log($"Scene '{scene.name}' is no longer loading.");
					#endif

					SceneManager.sceneLoaded -= OnSceneLoaded;
					taskCompletionSource.SetResult(true);
				}
			}
		}

		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		private static Object GetContext(ServiceInfo serviceInfo)
			#if UNITY_EDITOR
			=> Find.Script(serviceInfo.classWithAttribute);
			#else
			=> Object.FindAnyObjectByType(serviceInfo.classWithAttribute, FindObjectsInactive.Include);
			#endif

		#if UNITY_ENTITIES && !INIT_ARGS_DISABLE_ECS_SUPPORT
		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		private static async Task<object> InitializeUnmanagedSystemAsync(ServiceInfo serviceInfo, HashSet<Type> initialized, LocalServices localServices, Type closedConcreteType, [MaybeNull] Component client)
		{
			var world = await GetOrCreateWorldForService(serviceInfo, initialized, localServices, closedConcreteType, client: client);
			var (constructor, arguments) = await GetConstructorAndArguments(closedConcreteType, serviceInfo, initialized, localServices, client: client);
			if(constructor is not null && constructor.GetParameters().Length > 0)
			{
				initialized.Add(closedConcreteType);
				var existingSystem = world.GetExistingSystemManaged(closedConcreteType);

				if(existingSystem is null)
				{
					await OnBeforeCreateSystem(closedConcreteType, initialized, localServices, client: client);
				}

				if(Construct(constructor, existingSystem, closedConcreteType, arguments, serviceInfo, client: client) is ComponentSystemBase system && existingSystem is null)
				{
					AddSystemManaged(world, system, closedConcreteType);
					// Handles [UpdateInGroup] etc.
					DefaultWorldInitialization.AddSystemsToRootLevelSystemGroups(world, closedConcreteType);
					return system;
				}

				return existingSystem;
			}

			return world.GetOrCreateSystemManaged(closedConcreteType);
		}
		#endif

		#if UNITY_ENTITIES && !INIT_ARGS_DISABLE_ECS_SUPPORT
		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		private static Task<object> InitializeManagedSystemAsync(ServiceInfo serviceInfo, HashSet<Type> initialized, LocalServices localServices, Type closedConcreteType, [MaybeNull] Component client)
		{
			var initSystemUnmanagedMethodDefinition = typeof(ServiceInjector).GetMethod(nameof(InitSystemUnmanaged), BindingFlags.Static | BindingFlags.NonPublic);
			var initSystemUnmanagedMethod = initSystemUnmanagedMethodDefinition.MakeGenericMethod(closedConcreteType);
			return (Task<object>)initSystemUnmanagedMethod.Invoke(null, new object[] { serviceInfo, initialized, localServices, client });
		}
		#endif

		#if UNITY_ENTITIES && !INIT_ARGS_DISABLE_ECS_SUPPORT
		[MaybeNull] private static Dictionary<Type, HashSet<Type>> systemDependenciesCache;

		/// <summary>
		/// Key is the type of a System service; values are types of System and World services that must be created before said System service.
		/// </summary>
		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		[return: NotNull]
		private static Dictionary<Type, HashSet<Type>> GetOrCreateSystemDependenciesCache()
		{
			if(systemDependenciesCache is not null)
			{
				return systemDependenciesCache;
			}

			systemDependenciesCache = new(0);

			foreach(var info in ServiceAttributeUtility.concreteTypes)
			{
				if(!info.Value.IsSystem)
				{
					continue;
				}

				var serviceType = info.Key;
				AddInitAfterDependencies(serviceType.GetCustomAttributes<InitAfterAttribute>(), serviceType);
				AddCreateAfterDependencies(serviceType.GetCustomAttributes<CreateAfterAttribute>(), serviceType);
				AddCreateBeforeDependencies(serviceType, serviceType.GetCustomAttributes<CreateBeforeAttribute>());
				
				static void AddInitAfterDependencies(IEnumerable<InitAfterAttribute> befores, Type after)
				{
					foreach(var afterAttribute in befores)
					{
						foreach(var before in afterAttribute.types)
						{
							if(before == after)
							{
								continue;
							}

							if(ServiceAttributeUtility.concreteTypes.ContainsKey(before))
							{
								Add(before: before, after: after);
							}
							else
							{
								AddCreateAfterDependencies(before.GetCustomAttributes<CreateAfterAttribute>(), after);
							}
						}
					}
				}

				static void AddCreateAfterDependencies(IEnumerable<CreateAfterAttribute> befores, Type after)
				{
					foreach(var afterAttribute in befores)
					{
						var before = afterAttribute.SystemType;
						if(before == after)
						{
							continue;
						}

						if(ServiceAttributeUtility.concreteTypes.ContainsKey(before))
						{
							Add(before: before, after: after);
						}
						else
						{
							AddCreateAfterDependencies(before.GetCustomAttributes<CreateAfterAttribute>(), after);
						}
					}
				}

				static void AddCreateBeforeDependencies(Type before, IEnumerable<CreateBeforeAttribute> afters)
				{
					foreach(var beforeAttribute in afters)
					{
						var after = beforeAttribute.SystemType;
						if(before == after)
						{
							continue;
						}

						if(ServiceAttributeUtility.concreteTypes.ContainsKey(after))
						{
							Add(before: before, after: after);
						}
						else
						{
							AddCreateBeforeDependencies(before, after.GetCustomAttributes<CreateBeforeAttribute>());
						}
					}
				}
			}

			// Make sure that all world services are created before any system services.
			// This is because we might not know the name of a world service until after it has been created.
			// For example, if the world service is created by a ServiceInitializer.
			if(systemDependenciesCache.Count > 0 && ServiceAttributeUtility.concreteTypes
																		  .Where(x => x.Value.IsWorld)
																		  .Select(x => x.Key)
																		  .ToArray() is { Length: > 0 } worldServicesTypes)
			{
				foreach(var dependencies in systemDependenciesCache.Values)
				{
					foreach(var worldServicesType in worldServicesTypes)
					{
						dependencies.Add(worldServicesType);
					}
				}
			}

			return systemDependenciesCache;

			static void Add(Type before, Type after)
			{
				if(systemDependenciesCache.TryGetValue(after, out var dependencies))
				{
					dependencies.Add(before);
					return;
				}

				systemDependenciesCache[after] = new(1) { before };
			}
		}
		#endif

		#if UNITY_ENTITIES && !INIT_ARGS_DISABLE_ECS_SUPPORT
		private static async Task<World> GetOrCreateWorldForService(ServiceInfo serviceInfo, HashSet<Type> initialized, LocalServices localServices, Type serviceClosedConcreteType, [MaybeNull] Component client)
		{
			if(serviceInfo.WorldName is not { Length: > 0 } worldName)
			{
				return World.DefaultGameObjectInjectionWorld;
			}

			foreach(var world in World.All)
			{
				if(string.Equals(world.Name, worldName))
				{
					return world;
				}
			}

			if(typeof(World).IsAssignableFrom(serviceClosedConcreteType))
			{
				if(await GetOrInitializeService(serviceInfo, initialized, localServices, closedConcreteType: serviceClosedConcreteType, requestedServiceType: null, client: client) is World worldService)
				{
					return worldService;
				}

				return new(worldName);
			}

			foreach(var someServiceInfo in ServiceAttributeUtility.definingTypes.Values)
			{
				if(someServiceInfo.referenceType is ReferenceType.World
				   && string.Equals(someServiceInfo.WorldName, worldName)
				   && typeof(World).IsAssignableFrom(someServiceInfo.ConcreteOrDefiningType))
				{
					return await GetOrCreateWorldForService(someServiceInfo, initialized, localServices, someServiceInfo.ConcreteOrDefiningType, client: client);
				}
			}

			return new(worldName);
		}
		#endif
		
		#if UNITY_ENTITIES && !INIT_ARGS_DISABLE_ECS_SUPPORT
		[UnityEngine.Scripting.Preserve, JetBrains.Annotations.UsedImplicitly]
		private static void AddSystemManaged(World world, ComponentSystemBase system, Type concreteType)
		{
			typeof(World).GetMethod(nameof(World.AddSystemManaged), BindingFlags.Instance | BindingFlags.Public)
				.MakeGenericMethod(concreteType)
				.Invoke(world, new object[] { system });
		}
		#endif

		#if UNITY_ENTITIES && !INIT_ARGS_DISABLE_ECS_SUPPORT
		[UnityEngine.Scripting.Preserve, JetBrains.Annotations.UsedImplicitly]
		private static async Task<object> InitSystemUnmanaged<TSystem>(ServiceInfo serviceInfo, HashSet<Type> initialized, LocalServices localServices, [MaybeNull] Component client) where TSystem : unmanaged, ISystem
		{
			var (constructor, arguments) = await GetConstructorAndArguments(typeof(TSystem), serviceInfo, initialized, localServices, client: client);
			var world = await GetOrCreateWorldForService(serviceInfo, initialized, localServices, typeof(TSystem), client: client);
			
			#if UNITY_ENTITIES && !INIT_ARGS_DISABLE_ECS_SUPPORT
			await OnBeforeCreateSystem(typeof(TSystem), initialized, localServices, client: client);
			#endif

			var systemHandle = world.GetOrCreateSystem<TSystem>();
			object systemCopy = world.Unmanaged.GetUnsafeSystemRef<TSystem>(systemHandle);
			if(constructor is not null && constructor.GetParameters().Length > 0)
			{
				initialized.Add(typeof(TSystem));
				systemCopy = Construct(constructor, systemCopy, typeof(TSystem), arguments, serviceInfo, client: client);
			}
			else
			{
				systemCopy = await InjectCrossServiceDependencies(systemCopy, serviceInfo, initialized, localServices);
			}

			#if !UNITY_DISABLE_AUTOMATIC_SYSTEM_BOOTSTRAP
			if(typeof(TSystem).IsDefined(typeof(DisableAutoCreationAttribute), false) || typeof(TSystem).Assembly.IsDefined(typeof(DisableAutoCreationAttribute)))
			#endif
			{
				// Handles [UpdateInGroup] etc.
				DefaultWorldInitialization.AddSystemsToRootLevelSystemGroups(world, typeof(TSystem));
			}

			UpdateInWorld(world, systemHandle, (TSystem)systemCopy);
			return systemCopy;
		}
		#endif

		#if UNITY_ENTITIES && !INIT_ARGS_DISABLE_ECS_SUPPORT
		[UnityEngine.Scripting.Preserve, JetBrains.Annotations.UsedImplicitlyAttribute]
		private static void AddSystemUnmanaged<TSystem>(World world, TSystem system) where TSystem : unmanaged, ISystem
		{
			var systemHandle = world.GetOrCreateSystem<TSystem>();
			UpdateInWorld(world, systemHandle, system);
		}
		#endif

		#if UNITY_ENTITIES && !INIT_ARGS_DISABLE_ECS_SUPPORT
		[UnityEngine.Scripting.Preserve, MethodImpl(MethodImplOptions.AggressiveInlining)]
		private static void UpdateInWorld<TSystem>(World world, SystemHandle systemHandle, TSystem system) where TSystem : unmanaged, ISystem
		{
			ref var systemRef = ref world.Unmanaged.GetUnsafeSystemRef<TSystem>(systemHandle);
			systemRef = system;
		}
		#endif

		private static async Task<(ConstructorInfo constructor, object[] arguments)> GetConstructorAndArguments(Type closedConcreteType, ServiceInfo serviceInfo, HashSet<Type> initialized, LocalServices localServices, [MaybeNull] Component client)
		{
			#if DEV_MODE && UNITY_ASSERTIONS
			if(closedConcreteType.IsAbstract) { Debug.LogAssertion($"GetConstructorAndArguments {nameof(closedConcreteType)} {TypeUtility.ToString(closedConcreteType)} was abstract."); }
			if(closedConcreteType.ContainsGenericParameters) { Debug.LogAssertion($"GetConstructorAndArguments {nameof(closedConcreteType)} {TypeUtility.ToString(closedConcreteType)} contained open generic types."); }
			#endif

			var constructors = closedConcreteType.GetConstructors(BindingFlags.Instance | BindingFlags.Public);
			if(constructors.Length == 0)
			{
				constructors = closedConcreteType.GetConstructors(BindingFlags.Instance | BindingFlags.NonPublic);
			}

			IEnumerable<ConstructorInfo> constructorsByParameterCount = constructors.Length <= 1 ? constructors : constructors.OrderByDescending(c => c.GetParameters().Length);
			foreach(var constructor in constructorsByParameterCount)
			{
				var parameters = constructor.GetParameters();
				int parameterCount = parameters.Length;
				if(parameterCount == 0)
				{
					continue;
				}
				
				#if DEV_MODE && UNITY_ASSERTIONS
				if(parameters.Any(x => x.ParameterType.ContainsGenericParameters)) { Debug.LogAssertion($"constructor {TypeUtility.ToString(closedConcreteType)} parameter contained open generic types."); }
				#endif

				object[] arguments = new object[parameterCount];
				bool allArgumentsAvailable = true;

				for(int i = 0; i < parameterCount; i++)
				{
					var parameterType = parameters[i].ParameterType;
					if(!TryGetOrInitializeService(parameterType, out arguments[i], initialized, localServices, client: client))
					{
						allArgumentsAvailable = false;
						break;
					}
				}

				if(!allArgumentsAvailable)
				{
					#if UNITY_ENTITIES && !INIT_ARGS_DISABLE_ECS_SUPPORT
					if(typeof(World).IsAssignableFrom(closedConcreteType) && parameters[0].ParameterType == typeof(string))
					{
						if(parameterCount is 1)
						{
							arguments[0] = serviceInfo.WorldName is { Length: > 0 } worldName ? worldName : closedConcreteType.Name;

							#if DEV_MODE && DEBUG_INIT_WORLDS
							Debug.Log($"Creating World with name: {arguments[0]}");
							#endif

							return (constructor, arguments);
						}

						if(parameterCount is 2 && parameters[1].ParameterType == typeof(WorldFlags))
						{
							arguments[0] = serviceInfo.WorldName is { Length: > 0 } worldName ? worldName : closedConcreteType.Name;
							arguments[1] = WorldFlags.Simulation;

							#if DEV_MODE && DEBUG_INIT_WORLDS
							Debug.Log($"Creating World with name: {arguments[0]}, flags:{arguments[1]}");
							#endif

							return (constructor, arguments);
						}
					}
					#endif
					
					continue;
				}

				for(int parameterIndex = 0; parameterIndex < parameterCount; parameterIndex++)
				{
					var parameterType = parameters[parameterIndex].ParameterType;
					var argument = arguments[parameterIndex];

					#if DEV_MODE || DEBUG || INIT_ARGS_SAFE_MODE
					int maxAttempts = 3;
					#endif

					while(!parameterType.IsInstanceOfType(argument)
					#if DEV_MODE || DEBUG || INIT_ARGS_SAFE_MODE
					&& --maxAttempts >= 0
					#endif
					)
					{
						if(argument is Task loadArgumentTask)
						{
							#if DEV_MODE || DEBUG || INIT_ARGS_SAFE_MODE
							try
							{
							#endif

								await loadArgumentTask;

							#if DEV_MODE || DEBUG || INIT_ARGS_SAFE_MODE
							}
							catch(Exception exception) when (exception is not OperationCanceledException)
							{
								if(TryFindContainedExceptionForService(exception, serviceInfo, out var exceptionForService))
								{
									// Intentionally using "throw exceptionForService" instead of just "throw" to remove bloat from the stack trace.
									throw exceptionForService;
								}

								var failReason = exception is CircularDependenciesException ? ServiceInitFailReason.CircularDependencies : ServiceInitFailReason.MissingDependency;
								throw CreateAggregateException(exception, ServiceInitFailedException.Create(serviceInfo, failReason, missingDependencyType:parameterType, concreteType: closedConcreteType, localServices:localServices));
							}
							#endif

							argument = await loadArgumentTask.GetResult();
							arguments[parameterIndex] = argument;
						}
						else if(ServiceAttributeUtility.TryGetInfoForDefiningType(parameterType, out var argumentServiceInfo))
						{
							#if DEV_MODE
							Debug.LogWarning($"ServiceInjector.GetConstructorAndArguments: Service {TypeUtility.ToString(closedConcreteType)} parameter {parameterIndex} of type {TypeUtility.ToString(parameterType)} is a service provider, but the argument is not convertible to the parameter type. Attempting to get value from service provider {argumentServiceInfo.serviceProviderType}.", GetContext(serviceInfo));
							#endif

							switch(argumentServiceInfo.serviceProviderType)
							{
								case ServiceProviderType.Initializer:
									if(argument is IInitializer initializer)
									{
										if(serviceInfo.LoadAsync)
										{
											var target = await initializer.InitTargetAsync();
											OnAfterAwait(closedConcreteType);
											arguments[parameterIndex] = target;
										}
										else
										{
											var target = initializer.InitTarget();
											arguments[parameterIndex] = target;
										}
									}
									break;
								case ServiceProviderType.Wrapper when argument is IWrapper { WrappedObject: { } wrappedObject }:
									argument = wrappedObject;
									arguments[parameterIndex] = wrappedObject;
									break;
								case ServiceProviderType.IValueProviderT:
								case ServiceProviderType.IValueByTypeProvider:
								case ServiceProviderType.IValueProvider:
									if(ValueProviderUtility.TryGetValueProviderValue(argument, parameterType, client: client, out var providedValue))
									{
										OnAfterGetValueFromValueProvider(argument, client, providedValue);
										argument = providedValue;
										arguments[parameterIndex] = providedValue;
									}
									else
									{
										throw ServiceInitFailedException.Create(serviceInfo, ServiceInitFailReason.MissingDependency, initializerOrWrapper: argument, concreteType: closedConcreteType, missingDependencyType: parameterType, localServices: localServices);
									}
									break;
								case ServiceProviderType.IValueProviderAsyncT:
								case ServiceProviderType.IValueByTypeProviderAsync:
								case ServiceProviderType.IValueProviderAsync:
									var asyncProvidedValue = await ValueProviderUtility.GetValueProviderValueAsync(argument, closedConcreteType, client: client);
									OnAfterGetValueFromAsyncValueProvider(argument, client, asyncProvidedValue);
									argument = asyncProvidedValue;
									arguments[parameterIndex] = asyncProvidedValue;
									break;
								default:
									throw ServiceInitFailedException.Create(serviceInfo, ServiceInitFailReason.MissingDependency, argument as Object, argument as Component, initializerOrWrapper: argument, concreteType: closedConcreteType, missingDependencyType: parameterType, localServices: localServices);
							}
						}
					}
				}

				for(int i = 0; i < parameterCount; i++)
				{
					arguments[i] = await InjectCrossServiceDependencies(arguments[i], serviceInfo: null, initialized, localServices);
				}

				return (constructor, arguments);
			}

			return (constructors.FirstOrDefault(c => c.GetParameters().Length is 0), Array.Empty<object>());
		}

		private static object Construct([MaybeNull] ConstructorInfo constructor, [MaybeNull] object uninitializedInstance, Type concreteType, object[] arguments, ServiceInfo serviceInfo, [MaybeNull] Component client)
		{
			object result;
			if(uninitializedInstance is null)
			{
				if(constructor is not null)
				{
					#if DEV_MODE || DEBUG || INIT_ARGS_SAFE_MODE
					try
					{
					#endif

						result = constructor.Invoke(arguments);

						#if DEV_MODE && DEBUG_CREATE_SERVICES
						Debug.Log($"Service {TypeUtility.ToString(result.GetType())} created via constructor {constructor} successfully.");
						Debug.Assert(result.GetType() == concreteType, result.GetType().Name);
						#endif

					#if DEV_MODE || DEBUG || INIT_ARGS_SAFE_MODE
					}
					catch(Exception exception) when (exception is not OperationCanceledException)
					{
						throw ServiceInitFailedException.Create(serviceInfo, ServiceInitFailReason.ExceptionWasThrown, exception: exception, concreteType: concreteType);
					}
					#endif
				}
				else
				{
					result = Activator.CreateInstance(concreteType);

					#if DEV_MODE && DEBUG_CREATE_SERVICES
					Debug.Log($"Service {TypeUtility.ToString(concreteType)} created via default constructor successfully.");
					Debug.Assert(result.GetType() == concreteType, result.GetType().Name);
					#endif
				}

				if(result is IDisposable disposable)
				{
					disposables.Add(disposable);
				}
				else if(result is IAsyncDisposable asyncDisposable)
				{
					disposables.Add(new AsyncDisposableDisposer(asyncDisposable));
				}
			}
			else
			{
				constructor?.Invoke(uninitializedInstance, arguments);
				result = uninitializedInstance;

				#if DEV_MODE && DEBUG_CREATE_SERVICES
				if(constructor is not null)
				{
					Debug.Log($"Service {TypeUtility.ToString(concreteType)} initialized using constructor {constructor} successfully.");
					Debug.Assert(result.GetType() == concreteType, result.GetType().Name);
				}
				#endif
			}

			return result;
		}
		
		private static object Construct(ConstructorInfo constructor, object[] arguments)
		{
			var result = constructor.Invoke(arguments);

			#if DEV_MODE && DEBUG_CREATE_SERVICES
			Debug.Log($"Service {{TypeUtility.ToString(result.GetType())}} created via constructor {constructor} successfully.");
			#endif

			if(result is IDisposable disposable)
			{
				disposables.Add(disposable);
			}
			else if(result is IAsyncDisposable asyncDisposable)
			{
				disposables.Add(new AsyncDisposableDisposer(asyncDisposable));
			}

			return result;
		}

		#if UNITY_ENTITIES && !INIT_ARGS_DISABLE_ECS_SUPPORT
		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		private static async Task OnBeforeCreateSystem(Type systemType, HashSet<Type> initialized, LocalServices localServices, [MaybeNull] Component client)
		{
			#if UNITY_ENTITIES && !INIT_ARGS_DISABLE_ECS_SUPPORT
			if(GetOrCreateSystemDependenciesCache().TryGetValue(systemType, out var dependencies))
			{
				#if DEV_MODE && DEBUG_INIT_SYSTEMS
				Debug.Log($"Initializing system {TypeUtility.ToString(systemType)} only after services: {TypeUtility.ToString(dependencies)}");
				#endif

				foreach(var afterSystem in dependencies)
				{
					if(!initialized.Contains(afterSystem) && ServiceAttributeUtility.concreteTypes.TryGetValue(afterSystem, out var afterSystemInfo))
					{
						await GetOrInitializeService(afterSystemInfo, initialized, localServices, closedConcreteType: afterSystem, requestedServiceType: null, client: client);
					}
				}
			}
			#endif
		}
		#endif

		private static async Task<object> CreateUsingServiceInitializer([MaybeNull] Type closedConcreteType, ServiceInfo serviceInfo, HashSet<Type> initialized, LocalServices localServices, IServiceInitializer serviceInitializer, LoadMethod loadMethod, ReferenceType referenceType, [MaybeNull] Type requestedServiceType, [MaybeNull] Component client)
		{
			var interfaceTypes = serviceInfo.classWithAttribute.GetInterfaces();
			int parameterCount = 0;
			for(int interfaceIndex = interfaceTypes.Length - 1; interfaceIndex >= 0; interfaceIndex--)
			{
				var interfaceType = interfaceTypes[interfaceIndex];
				if(!interfaceType.IsGenericType)
				{
					continue;
				}

				var typeDefinition = interfaceType.GetGenericTypeDefinition();
				if(!argumentCountsByIServiceInitializerTypeDefinition.TryGetValue(typeDefinition, out parameterCount))
				{
					continue;
				}

				if(closedConcreteType is not null)
				{
					#if DEV_MODE && (DEBUG || INIT_ARGS_SAFE_MODE)
					Debug.Assert(!initialized.Contains(closedConcreteType));
					#endif
					initialized.Add(closedConcreteType);
				}

				var parameterTypes = interfaceType.GetGenericArguments().Skip(1).ToArray();
				var arguments = new object[parameterCount];
				int failedToGetArgumentAtIndex;
				
				#if DEV_MODE || DEBUG || INIT_ARGS_SAFE_MODE
				try
				{
				#endif

				failedToGetArgumentAtIndex = await GetOrInitializeServices(parameterTypes, initialized, localServices, arguments, client: client);

				#if DEV_MODE || DEBUG || INIT_ARGS_SAFE_MODE
				}
				catch(Exception exception) when (exception is not OperationCanceledException and not ServiceInitFailedException)
				{
					throw ServiceInitFailedException.Create(serviceInfo, ServiceInitFailReason.ExceptionWasThrown, sceneObject: client, exception: exception, concreteType: closedConcreteType, localServices: localServices);
				}
				#endif

				#if DEV_MODE || DEBUG || INIT_ARGS_SAFE_MODE
				if(failedToGetArgumentAtIndex is not -1)
				{
					throw ServiceInitFailedException.Create(serviceInfo, ServiceInitFailReason.MissingDependency, asset: serviceInitializer as Object, sceneObject: client, initializerOrWrapper: serviceInitializer, concreteType: closedConcreteType, missingDependencyType: parameterTypes[failedToGetArgumentAtIndex], localServices: localServices);
				}
				#endif

				for(int parameterIndex = 0; parameterIndex < parameterCount; parameterIndex++)
				{
					var parameterType = parameterTypes[parameterIndex];
					var argument = arguments[parameterIndex];

					if(argument is Task loadArgumentTask && !parameterType.IsInstanceOfType(argument))
					{
						await loadArgumentTask;
						argument = await loadArgumentTask.GetResult();
						arguments[parameterIndex] = argument;
					}
				}

				object result;
				#if DEV_MODE || DEBUG || INIT_ARGS_SAFE_MODE
				try
				{
				#endif

				// if service initializer does not implement InitTarget, then InitTarget will just return the default value of closedConcreteType.
				// With reference types the default value is null, which we can use to determine if we should disregard the value and continue
				// onwards to call InitializeServiceAsync. With value types, the default value is not null, so we can't do the same thing.
				// What we do instead is check that service initializer provides a custom implementation of InitTarget, instead of using
				// the default implementation of ServiceInitializer<T>.InitTarget, which we know just returns the default value of T.
				if(parameterCount > 0 || closedConcreteType is not { IsValueType: true } || ImplementsInitTarget(serviceInitializer.GetType()))
				{
					#if UNITY_ENTITIES && !INIT_ARGS_DISABLE_ECS_SUPPORT
					if(closedConcreteType is not null && (typeof(SystemBase).IsAssignableFrom(closedConcreteType) || typeof(ISystem).IsAssignableFrom(closedConcreteType)))
					{
						await OnBeforeCreateSystem(closedConcreteType, initialized, localServices, client: client);
					}
					#endif

					result = serviceInitializer.InitTarget(arguments);
				}
				else
				{
					#if DEV_MODE && DEBUG_CREATE_SERVICES
					Debug.Log($"Will not execute {TypeUtility.ToString(serviceInitializer.GetType())}.InitTarget because it does not contain a custom InitTarget implementation, and should just return the default value of {TypeUtility.ToString(closedConcreteType)}.");
					#endif
					result = null;
				}

				static bool ImplementsInitTarget(Type serviceInitializerType)
				{
					// return false if the type derives from ServiceInitializer<T> and does not provide a custom implementation of InitTarget.
					var type = serviceInitializerType;
					while(type.BaseType is { } baseType)
					{
						if(type.GetMethods(BindingFlags.Instance | BindingFlags.DeclaredOnly | BindingFlags.Public)
								.FirstOrDefault(x => string.Equals(x.Name, nameof(ServiceInitializer<object>.InitTarget))
									&& x.GetParameters().Length is 0) is { } initTargetMethod)
						{
							return initTargetMethod.DeclaringType is not { IsGenericType: true } declaringType
								|| !serviceInitializerTypeDefinitions.Contains(declaringType.GetGenericTypeDefinition());
						}

						type = baseType;
					}

					return true;
				}

				#if DEV_MODE || DEBUG || INIT_ARGS_SAFE_MODE
				}
				catch(Exception exception) when (exception is not OperationCanceledException)
				{
					throw ServiceInitFailedException.Create(serviceInfo, ServiceInitFailReason.ServiceProviderThrewException, initializerOrWrapper: serviceInitializer, exception: exception, concreteType: closedConcreteType, localServices: localServices);
				}
				#endif

				// TODO: If IsTransient is true, don't dispose, but cache instead, and subscribe for IUpdate.Update etc.
				if(serviceInitializer is IDisposable disposableInitializer)
				{
					disposableInitializer.Dispose();
				}
				else if(serviceInitializer is IAsyncDisposable asyncDisposableInitializer)
				{
					#if !DEBUG
					_ =
					#endif
					asyncDisposableInitializer.DisposeAsync()
					#if DEBUG
					.OnFailure(HandleLogException)
					#endif
						;
				}

				if(result is not null)
				{
					#if DEV_MODE && DEBUG_CREATE_SERVICES
					Debug.Log($"Service {TypeUtility.ToString(closedConcreteType ?? result.GetType())} created via service initializer {TypeUtility.ToString(serviceInitializer.GetType())} successfully.");
					#endif

					if(result is Task task)
					{
						result = await task.GetResult();
						OnAfterAwait(closedConcreteType);
					}

					#if UNITY_ENTITIES && !INIT_ARGS_DISABLE_ECS_SUPPORT
					if(serviceInfo.IsSystem)
					{
						if(result is ComponentSystemBase systemBase)
						{
							var systemType = systemBase.GetType();
							#if !UNITY_DISABLE_AUTOMATIC_SYSTEM_BOOTSTRAP
							if(systemType.IsDefined(typeof(DisableAutoCreationAttribute), false) || systemType.Assembly.IsDefined(typeof(DisableAutoCreationAttribute)))
							#endif
							{
								var world = await GetOrCreateWorldForService(serviceInfo, initialized, localServices, closedConcreteType, client: client);
								AddSystemManaged(world, systemBase, closedConcreteType);
								// Handles [UpdateInGroup] etc.
								DefaultWorldInitialization.AddSystemsToRootLevelSystemGroups(world, systemType);
							}
						}
						else if(result is ISystem system)
						{
							var systemType = system.GetType();
							#if !UNITY_DISABLE_AUTOMATIC_SYSTEM_BOOTSTRAP
							if(systemType.IsDefined(typeof(DisableAutoCreationAttribute), false) || systemType.Assembly.IsDefined(typeof(DisableAutoCreationAttribute)))
							#endif
							{
								var world = await GetOrCreateWorldForService(serviceInfo, initialized, localServices, closedConcreteType, client: client);
								var addSystemUnmanagedMethodDefinition = typeof(ServiceInjector).GetMethod(nameof(AddSystemUnmanaged), BindingFlags.Static | BindingFlags.NonPublic);
								var addSystemUnmanagedMethod = addSystemUnmanagedMethodDefinition.MakeGenericMethod(systemType);
								addSystemUnmanagedMethod.Invoke(null, new object[] { world, system });
								// Handles [UpdateInGroup] etc.
								DefaultWorldInitialization.AddSystemsToRootLevelSystemGroups(world, systemType);
							}
						}
					}
					#endif

					return result;
				}

				#if DEV_MODE || DEBUG || INIT_ARGS_SAFE_MODE
				// InitTarget methods that accept arguments should never return null.
				if(parameterCount > 0)
				{
					throw ServiceInitFailedException.Create(serviceInfo, ServiceInitFailReason.ServiceProviderResultNull, initializerOrWrapper: serviceInitializer, concreteType: closedConcreteType, localServices: localServices);
				}
				#endif
			}

			if(parameterCount is 0)
			{
				if(FilterForServiceProvider(serviceInfo.classWithAttribute, loadMethod, referenceType) != (LoadMethod.Default, ReferenceType.None))
				{
					loadMethod = LoadMethod.Default;
					referenceType = ReferenceType.None;
				}

				// InitTarget methods that accept zero arguments may return null, and it is not an error,
				// but just means that Init(args) should take care of creating the service...
				initialized.Remove(closedConcreteType);
				return await InitializeServiceAsync(closedConcreteType, ServiceProviderType.None, loadMethod, referenceType, serviceInfo, initialized, localServices, requestedServiceType, client: client);
			}

			return null;
		}

		private static async Task<object> CreateUsingServiceInitializerAsync([MaybeNull] Type closedConcreteType, ServiceInfo serviceInfo, HashSet<Type> initialized, LocalServices localServices, IServiceInitializerAsync serviceInitializerAsync, [MaybeNull] Component client)
		{
			Type[] interfaceTypes = serviceInfo.classWithAttribute.GetInterfaces();
			int parameterCount = 0;
			for(int interfaceIndex = interfaceTypes.Length - 1; interfaceIndex >= 0; interfaceIndex--)
			{
				var interfaceType = interfaceTypes[interfaceIndex];
				if(!interfaceType.IsGenericType)
				{
					continue;
				}

				var typeDefinition = interfaceType.GetGenericTypeDefinition();
				if(argumentCountsByIServiceInitializerTypeDefinition.TryGetValue(typeDefinition, out parameterCount))
				{
					break;
				}

				if(closedConcreteType is not null)
				{
					#if DEV_MODE && (DEBUG || INIT_ARGS_SAFE_MODE)
					Debug.Assert(!initialized.Contains(closedConcreteType));
					#endif
					initialized.Add(closedConcreteType);
				}

				var parameterTypes = interfaceType.GetGenericArguments().Skip(1).ToArray();
				object[] arguments = new object[parameterCount];
				int failedToGetArgumentAtIndex = await GetOrInitializeServices(parameterTypes, initialized, localServices, arguments, client: client);

				#if DEV_MODE || DEBUG || INIT_ARGS_SAFE_MODE
				if(failedToGetArgumentAtIndex != -1)
				{
					throw ServiceInitFailedException.Create(serviceInfo, ServiceInitFailReason.MissingDependency, initializerOrWrapper: serviceInitializerAsync, concreteType: closedConcreteType, missingDependencyType: parameterTypes[failedToGetArgumentAtIndex], localServices: localServices);
				}
				#endif

				for(int parameterIndex = 0; parameterIndex < parameterCount; parameterIndex++)
				{
					var parameterType = parameterTypes[parameterIndex];
					var argument = arguments[parameterIndex];

					if(argument is Task loadArgumentTask && !parameterType.IsInstanceOfType(argument))
					{
						await loadArgumentTask;
						argument = await loadArgumentTask.GetResult();
						arguments[parameterIndex] = argument;
					}
				}

				#if UNITY_ENTITIES && !INIT_ARGS_DISABLE_ECS_SUPPORT
				if(closedConcreteType is not null && (typeof(SystemBase).IsAssignableFrom(closedConcreteType) || typeof(ISystem).IsAssignableFrom(closedConcreteType)))
				{
					await OnBeforeCreateSystem(closedConcreteType, initialized, localServices, client: client);
				}
				#endif

				Task task = serviceInitializerAsync.InitTargetAsync(arguments.Append(ExitCancellationToken).ToArray());

				#if DEV_MODE || DEBUG || INIT_ARGS_SAFE_MODE
				// InitTargetAsync methods that accept arguments should never return null.
				if(task is null)
				{
					throw ServiceInitFailedException.Create(serviceInfo, ServiceInitFailReason.ServiceProviderResultNull, initializerOrWrapper: serviceInitializerAsync, concreteType: closedConcreteType, localServices: localServices);
				}
				#endif

				object result;

				try
				{
					result = await task.GetResult();
					OnAfterAwait(closedConcreteType);
				}
				catch(Exception exception) when (exception is not OperationCanceledException and not ServiceInitFailedException)
				{
					throw ServiceInitFailedException.Create(serviceInfo, ServiceInitFailReason.ServiceProviderThrewException, initializerOrWrapper: serviceInitializerAsync, exception: exception, concreteType: closedConcreteType, localServices: localServices);
				}

				#if DEV_MODE || DEBUG || INIT_ARGS_SAFE_MODE
				// InitTargetAsync methods that accept arguments should never return null.
				if(result is null)
				{
					throw ServiceInitFailedException.Create(serviceInfo, ServiceInitFailReason.ServiceProviderResultNull, initializerOrWrapper: serviceInitializerAsync, concreteType: closedConcreteType, localServices: localServices);
				}
				#endif

				#if DEV_MODE && DEBUG_CREATE_SERVICES
				Debug.Log($"Service {TypeUtility.ToString(closedConcreteType)} created via async service initializer {TypeUtility.ToString(serviceInitializerAsync.GetType())} successfully.");
				Debug.Assert(result.GetType() == closedConcreteType, result.GetType().Name);
				#endif

				return result;
			}

			if(parameterCount == 0)
			{
				Task<object> task;
				#if DEV_MODE || DEBUG || INIT_ARGS_SAFE_MODE
				try
				{
				#endif

					task = serviceInitializerAsync.InitTargetAsync(ExitCancellationToken);

				#if DEV_MODE || DEBUG || INIT_ARGS_SAFE_MODE
				}
				catch(ServiceInitFailedException)
				{
					throw;
				}
				catch(Exception exception) when (exception is not OperationCanceledException and not ServiceInitFailedException)
				{
					throw ServiceInitFailedException.Create(serviceInfo, ServiceInitFailReason.ServiceProviderThrewException, initializerOrWrapper: serviceInitializerAsync, exception: exception, concreteType: closedConcreteType, localServices: localServices);
				}
				#endif

				#if DEV_MODE || DEBUG || INIT_ARGS_SAFE_MODE
				if(task is null)
				{
					throw ServiceInitFailedException.Create(serviceInfo, ServiceInitFailReason.ServiceProviderResultNull, initializerOrWrapper: serviceInitializerAsync, concreteType: closedConcreteType, localServices: localServices);
				}
				#endif

				object result;

				#if DEV_MODE || DEBUG || INIT_ARGS_SAFE_MODE
				try
				{
				#endif

					result = await task;
					OnAfterAwait(closedConcreteType);

				#if DEV_MODE || DEBUG || INIT_ARGS_SAFE_MODE
				}
				catch(Exception exception) when (exception is not OperationCanceledException and not ServiceInitFailedException)
				{
					throw ServiceInitFailedException.Create(serviceInfo, ServiceInitFailReason.ServiceProviderThrewException, initializerOrWrapper: serviceInitializerAsync, exception: exception, concreteType: closedConcreteType, localServices: localServices);
				}
				#endif

				#if DEV_MODE || DEBUG || INIT_ARGS_SAFE_MODE
				// InitTargetAsync should never return null.
				if(result is null)
				{
					throw ServiceInitFailedException.Create(serviceInfo, ServiceInitFailReason.ServiceProviderResultNull, initializerOrWrapper: serviceInitializerAsync, concreteType: closedConcreteType, localServices: localServices);
				}
				#endif

				#if DEV_MODE && DEBUG_CREATE_SERVICES
				Debug.Log($"Service {TypeUtility.ToString(closedConcreteType)} created via async service initializer {TypeUtility.ToString(serviceInitializerAsync.GetType())} successfully.");
				Debug.Assert(result.GetType() == closedConcreteType, result.GetType().Name);
				#endif

				return result;
			}

			return null;
		}

		private static async Task LoadDependenciesOfServicesInScene(string sceneName, [DisallowNull] HashSet<Type> initialized, [DisallowNull] LocalServices localServices)
		{
			var servicesInScene = GetServiceDefinitions().Where(s => s.SceneName == sceneName).ToArray();
			var dependencies = GetExternalDependenciesOfServices(servicesInScene);
			
			#if DEV_MODE && DEBUG_LOAD_SCENE
			Debug.Log($"Loading dependencies of services in scene '{sceneName}':\n{TypeUtility.ToString(dependencies, "\n")}");
			#endif

			var loadTasks = new List<Task>(dependencies.Count);
			foreach(var dependencyType in dependencies)
			{
				if(TryGetOrInitializeService(dependencyType, out var service, initialized, localServices, client: null) && service is Task task)
				{
					loadTasks.Add(task);
				}
			}

			await Task.WhenAll(loadTasks);
		}
		
		private static async Task LoadDependenciesOfServicesInScene(int sceneBuildIndex, [DisallowNull] HashSet<Type> initialized, [DisallowNull] LocalServices localServices)
		{
			var servicesInScene = GetServiceDefinitions().Where(s => s.SceneBuildIndex == sceneBuildIndex).ToArray();
			var dependencies = GetExternalDependenciesOfServices(servicesInScene);
			
			#if DEV_MODE && DEBUG_LOAD_SCENE
			Debug.Log($"Loading dependencies of services in scene with build index {sceneBuildIndex}:\n{TypeUtility.ToString(dependencies, "\n")}");
			#endif
			
			var loadTasks = new List<Task>(0);
			
			foreach(var dependencyType in dependencies)
			{
				if(TryGetOrInitializeService(dependencyType, out var service, initialized, localServices, client: null) && service is Task task)
				{
					loadTasks.Add(task);
				}
			}
			
			await Task.WhenAll(loadTasks);
		}

		/// <summary>
		/// Gets types of all dependencies that services have, excluding dependencies between the services themselves.
		/// </summary>
		private static HashSet<Type> GetExternalDependenciesOfServices(ServiceInfo[] serviceInfos)
		{
			HashSet<Type> dependencyTypes = new();

			foreach(var serviceInfo in serviceInfos)
			{
				GetAllDependencies(serviceInfo.ConcreteOrDefiningType, dependencyTypes);
			}

			// Exclude the services themselves from being their own dependencies.
			foreach(var serviceInfo in serviceInfos)
			{
				dependencyTypes.Remove(serviceInfo.concreteType);
				foreach(var definingType in serviceInfo.definingTypes)
				{
					dependencyTypes.Remove(definingType);
				}
			}

			// Also exclude transient and lazy services. They should only be initialized when they are requested by a client.
			foreach(var serviceInfo in GetServiceDefinitions())
			{
				if(serviceInfo.IsTransient || serviceInfo.LazyInit)
				{
					dependencyTypes.Remove(serviceInfo.concreteType);
					foreach(var definingType in serviceInfo.definingTypes)
					{
						dependencyTypes.Remove(definingType);
					}
				}
			}

			return dependencyTypes;
		}

		/// <summary>
		/// Gets names of all scenes containing dependencies for the services.
		/// </summary>
		private static HashSet<ServiceInfo> GetSceneServiceDependenciesOfService(ServiceInfo serviceInfo)
		{
			HashSet<ServiceInfo> dependencyInfos = new();

			foreach(var interfaceType in serviceInfo.ConcreteOrDefiningType.GetInterfaces())
			{
				if(!interfaceType.IsGenericType)
				{
					continue;
				}

				var genericTypeDefinition = interfaceType.IsGenericTypeDefinition ? interfaceType : interfaceType.GetGenericTypeDefinition();
				if(!argumentCountsByIArgsTypeDefinition.ContainsKey(genericTypeDefinition))
				{
					continue;
				}
				
				foreach(var argumentType in interfaceType.GetGenericArguments())
				{
					if(!ServiceAttributeUtility.TryGetInfoForDefiningType(argumentType, out var dependencyInfo)
						|| dependencyInfo.LazyInit
						|| dependencyInfo.SceneName is not { Length: > 0 })
					{
						continue;
					}

					dependencyInfos.Add(dependencyInfo);
				}
			}

			return dependencyInfos;
		}

		/// <summary>
		/// Gets or creates, caches and finalizes a service provider.
		/// </summary>
		private static async Task<object> GetOrCreateServiceProvider(ServiceInfo serviceInfo, HashSet<Type> initialized, LocalServices localServices, Type requestedServiceType, [AllowNull] Type serviceClosedConcreteType, [MaybeNull] Component client)
		{
			serviceClosedConcreteType ??= GetConcreteAndClosedType(serviceInfo, requestedServiceType);

			var serviceProviderClosedType = GetServiceProviderClosedConcreteType(serviceInfo, serviceClosedConcreteType, requestedServiceType);
			if(serviceProviderClosedType is null)
			{
				#if DEV_MODE
				Debug.LogError($"Could not determine concrete and closed type for {serviceInfo.serviceProviderType} {TypeUtility.ToString(serviceInfo.classWithAttribute)}. Service will not be initialized.");
				#endif
				throw ServiceInitFailedException.Create(serviceInfo, ServiceInitFailReason.CreatingServiceProviderFailed, asset:null, concreteType:serviceProviderClosedType, localServices:localServices);
			}

			if(services.TryGetValue(serviceProviderClosedType, out var serviceProvider))
			{
				#if DEV_MODE && UNITY_ASSERTIONS
				if(serviceProvider is not IServiceInitializer and not IServiceInitializerAsync && !ValueProviderUtility.IsValueProvider(serviceProvider)) { Debug.LogAssertion(TypeUtility.ToString(serviceProvider?.GetType())); }
				#endif

				return serviceProvider;
			}

			var (loadMethod, referenceType) = FilterForServiceProvider(serviceProviderClosedType, serviceInfo.loadMethod, serviceInfo.referenceType);

			#if DEV_MODE || DEBUG || INIT_ARGS_SAFE_MODE
			try
			{
			#endif

			serviceProvider = await InitializeServiceAsync(serviceProviderClosedType, ServiceProviderType.None, loadMethod, referenceType, serviceInfo, initialized, localServices, requestedServiceType, client: client);
			OnAfterAwait(serviceInfo);

			#if DEV_MODE || DEBUG || INIT_ARGS_SAFE_MODE
			}
			catch(ServiceInitFailedException)
			{
				throw;
			}
			catch(Exception ex)
			{
				throw ServiceInitFailedException.Create(serviceInfo, ServiceInitFailReason.CreatingServiceProviderFailed, asset:null, exception:ex, concreteType:serviceProviderClosedType, localServices:localServices);
			}
			#endif

			#if DEV_MODE || DEBUG || INIT_ARGS_SAFE_MODE
			if(serviceProvider is null)
			{
				throw ServiceInitFailedException.Create(serviceInfo, ServiceInitFailReason.CreatingServiceProviderFailed, asset:null, concreteType:serviceProviderClosedType, localServices:localServices);
			}
			#endif

			#if DEV_MODE && UNITY_ASSERTIONS
			if(serviceProvider is not IServiceInitializer and not IServiceInitializerAsync && !ValueProviderUtility.IsValueProvider(serviceProvider)) { Debug.LogAssertion($"InitializeServiceAsync({TypeUtility.ToString(serviceProviderClosedType)}) => {TypeUtility.ToString(serviceProvider.GetType())}"); }
			#endif

			FinalizeServiceProviderImmediate(serviceInfo, serviceProvider);

			return serviceProvider;

			[return: MaybeNull]
			static Type GetServiceProviderClosedConcreteType([DisallowNull] ServiceInfo serviceInfo, [MaybeNull] Type serviceClosedConcreteType, [MaybeNull] Type requestedServiceType)
			{
				var serviceProviderType = serviceInfo.classWithAttribute;

				#if DEV_MODE && UNITY_ASSERTIONS
				if(serviceProviderType is null) { Debug.LogAssertion("GetServiceProviderClosedType classWithAttribute was null."); }
				if(serviceProviderType.IsAbstract) { Debug.LogAssertion($"GetServiceProviderClosedType classWithAttribute {TypeUtility.ToString(serviceProviderType)} was abstract."); }
				if(serviceClosedConcreteType is not null && serviceClosedConcreteType.ContainsGenericParameters) { Debug.LogAssertion($"GetServiceProviderClosedType {nameof(serviceClosedConcreteType)} {TypeUtility.ToString(serviceClosedConcreteType)} contained open generic types."); }
				if(serviceClosedConcreteType is not null && serviceClosedConcreteType.IsAbstract) { Debug.LogAssertion($"GetServiceProviderClosedType {nameof(serviceClosedConcreteType)} {TypeUtility.ToString(serviceClosedConcreteType)} was abstract."); }
				#endif

				if(!serviceProviderType.ContainsGenericParameters)
				{
					return serviceProviderType;
				}

				var serviceProviderBaseInterface = serviceInfo.serviceProviderType switch
				{
					ServiceProviderType.IValueProviderT => typeof(IValueProvider),
					ServiceProviderType.IValueProviderAsyncT => typeof(IValueProviderAsync),
					ServiceProviderType.ServiceInitializer => typeof(IServiceInitializer),
					ServiceProviderType.ServiceInitializerAsync => typeof(IServiceInitializerAsync),
					_ => typeof(void) 
				};

				var interfaces = serviceProviderType.GetInterfaces();
				if(interfaces.FirstOrDefault(i => i.IsGenericType && i.GetInterfaces().Contains(serviceProviderBaseInterface)) is not { } serviceProviderInterfaceType)
				{
					Debug.LogWarning("Failed to find " + serviceInfo.serviceProviderType switch
					{
						ServiceProviderType.IValueProviderT => "IValueProvider<T>",
						ServiceProviderType.IValueProviderAsyncT => "IValueProviderAsync<T>",
						ServiceProviderType.ServiceInitializer => "IServiceInitializer<T...>",
						ServiceProviderType.ServiceInitializerAsync => "IServiceInitializerAsync<T...>",
						_ => "value provider" 
					} + $" interface type for {TypeUtility.ToString(serviceProviderType)} among interface {string.Join(", ", interfaces.Select(x => TypeUtility.ToString(x)))}.", GetContext(serviceInfo));
					return null;
				}

				if(serviceProviderInterfaceType.GetGenericArguments().FirstOrDefault() is not { } serviceProviderReturnedValueType)
				{
					#if DEV_MODE
					Debug.LogWarning($"Failed to find service concrete type for {TypeUtility.ToString(serviceProviderType)}.", GetContext(serviceInfo));
					#endif
					return null;
				}

				var closedServiceType = serviceClosedConcreteType ?? requestedServiceType;
				if(closedServiceType is null)
				{
					#if DEV_MODE
					Debug.LogWarning($"Failed to find service concrete type for {TypeUtility.ToString(serviceProviderType)}.", GetContext(serviceInfo));
					#endif
					return null;
				}

				// E.g. class ActorInitializer<TActor> : ServiceInitializer<TActor>
				if(serviceProviderReturnedValueType == serviceProviderType.GetGenericArguments().FirstOrDefault())
				{
					return serviceProviderType.MakeGenericType(closedServiceType);
				}

				// E.g. class ActorInitializer<TActor> : ServiceInitializer<TActor>
				if(serviceProviderReturnedValueType == serviceProviderType.GetGenericArguments().FirstOrDefault())
				{
					return serviceProviderType.MakeGenericType(closedServiceType);
				}

				// E.g. class LoggerInitializer<T> : ServiceInitializer<Logger<T>> { }
				return serviceProviderType.MakeGenericType(closedServiceType.GetGenericArguments());
			}
		}

		#if UNITY_ADDRESSABLES_1_17_4_OR_NEWER
		private static async Task<Object> LoadAddressableAsset(string addressableKey, ServiceInfo serviceInfo, Component client)
		{
			var asyncOperation = Addressables.LoadAssetAsync<Object>(addressableKey);
			disposables.Add(new AddressableAssetReleaser(asyncOperation, serviceInfo.IsTransient ? client : null));
			Object asset;

			if(serviceInfo.LoadAsync)
			{
				asset = await asyncOperation.Task;
				OnAfterAwait(serviceInfo);
			}
			else
			{
				asset = asyncOperation.WaitForCompletion();
			}

			#if DEBUG || INIT_ARGS_SAFE_MODE
			if(!asset)
			{
				throw ServiceInitFailedException.Create(serviceInfo, ServiceInitFailReason.MissingAddressable, asset);
			}
			#endif

			return asset;
		}
		#endif

		#if UNITY_ADDRESSABLES_1_17_4_OR_NEWER
		private static async Task<object> InitializeAddressableAsset(Type closedConcreteType, string addressableKey, ServiceInfo serviceInfo, HashSet<Type> initialized, LocalServices localServices, [MaybeNull] Component client)
		{
			var asset = await LoadAddressableAsset(addressableKey, serviceInfo, client);

			object result;
			if(asset is GameObject gameObject)
			{
				if(serviceInfo.ShouldInstantiate(true))
				{
					result = await InstantiateFromAsset(closedConcreteType, gameObject, serviceInfo, initialized, localServices, client: client);

					#if DEBUG || INIT_ARGS_SAFE_MODE
					if(result is null) throw ServiceInitFailedException.Create(serviceInfo, ServiceInitFailReason.MissingComponent, asset: asset, sceneObject: client, localServices: localServices);
					#endif

					#if DEV_MODE && DEBUG_CREATE_SERVICES
					Debug.Log($"Service {TypeUtility.ToString(closedConcreteType)} Instantiated from addressable asset \"{addressableKey}\" successfully.", asset);
					Debug.Assert(result.GetType() == closedConcreteType, result.GetType().Name);
					#endif

					return result;
				}

				result = await GetServiceFromInstance(closedConcreteType, gameObject, serviceInfo, client: client);

				#if DEBUG || INIT_ARGS_SAFE_MODE
				if(result is null) throw ServiceInitFailedException.Create(serviceInfo, ServiceInitFailReason.MissingComponent, asset:asset, sceneObject: client, localServices: localServices);
				#endif

				#if DEV_MODE && DEBUG_CREATE_SERVICES
				Debug.Log($"Service {TypeUtility.ToString(closedConcreteType)} loaded from addressable asset \"{addressableKey}\" successfully.", asset);
				Debug.Assert(result.GetType() == closedConcreteType, result.GetType().Name);
				#endif

				return result;
			}

			if(asset is ScriptableObject scriptableObject)
			{
				if(serviceInfo.ShouldInstantiate(false))
				{
					result = await InstantiateFromAsset(closedConcreteType, scriptableObject, serviceInfo, initialized, localServices, client: client);

					#if DEBUG || INIT_ARGS_SAFE_MODE
					if(result is null) throw ServiceInitFailedException.Create(serviceInfo, ServiceInitFailReason.MissingComponent, asset, sceneObject: client, localServices: localServices);
					#endif
					
					#if UNITY_EDITOR
					disposables.Add(new ScriptableObjectDestroyer(result as ScriptableObject));
					#endif

					#if DEV_MODE && DEBUG_CREATE_SERVICES
					Debug.Log($"Service {TypeUtility.ToString(serviceInfo.concreteType)} Instantiated from addressable asset \"{addressableKey}\" successfully.", asset);
					Debug.Assert(result.GetType() == closedConcreteType, result.GetType().Name);
					#endif

					return result;
				}
				else
				{
					result = await GetServiceAsync(closedConcreteType, scriptableObject, serviceInfo, client: client);

					#if DEBUG || INIT_ARGS_SAFE_MODE
					if(result is null) throw ServiceInitFailedException.Create(serviceInfo, ServiceInitFailReason.MissingComponent, asset, sceneObject: client, localServices: localServices);
					#endif

					#if DEV_MODE && DEBUG_CREATE_SERVICES
					Debug.Log($"Service {TypeUtility.ToString(closedConcreteType)} loaded from addressable asset \"{addressableKey}\" successfully.", asset);
					Debug.Assert(result.GetType() == closedConcreteType, result.GetType().Name);
					#endif

					return result;
				}
			}

			#if DEBUG || INIT_ARGS_SAFE_MODE
			if(!serviceInfo.IsInstanceOf(asset))
			{
				throw ServiceInitFailedException.Create(serviceInfo, ServiceInitFailReason.AssetNotConvertible, asset, null, null, null, concreteType:asset.GetType(), null, localServices);
			}
			#endif

			#if DEV_MODE && DEBUG_CREATE_SERVICES
			Debug.Log($"Service {TypeUtility.ToString(closedConcreteType)} loaded from addressable asset \"{addressableKey}\" successfully.", asset);
			Debug.Assert(asset.GetType() == closedConcreteType, asset.GetType().Name);
			#endif

			return asset;
		}
		#endif

		private static Exception CreateAggregateException(Exception exiting, Exception addition)
		{
			if(exiting is AggregateException aggregateException)
			{
				var innerExceptions = aggregateException.InnerExceptions.ToList();
				innerExceptions.Add(addition);
				throw new AggregateException(innerExceptions);
			}

			return new AggregateException(exiting, addition);
		}

		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		private static void LogException(Exception exception)
		{
			if(exception is AggregateException aggregateException)
			{
				LogException(aggregateException);
				return;
			}

			// Avoid same exception being logged twice for the same service.
			#if DEV_MODE || DEBUG || INIT_ARGS_SAFE_MODE
			if(exception is not ServiceInitFailedException serviceInitFailedException || exceptionsLogged.Add(serviceInitFailedException.ServiceInfo))
			#endif
			{
				Debug.LogException(exception);
			}
		}

		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		private static void LogException(AggregateException aggregateException)
		{
			foreach(var innerException in aggregateException.InnerExceptions)
			{
				LogException(innerException);
			}
		}

		private static bool TryFindContainedExceptionForService(Exception exceptionToCheck, ServiceInfo serviceInfo, out Exception exceptionForService)
		{
			if(exceptionToCheck is AggregateException aggregateException)
			{
				return TryFindContainedExceptionForService(aggregateException, serviceInfo, out exceptionForService);
			}

			if(exceptionToCheck is CircularDependenciesException circularDependenciesException)
			{
				return TryFindContainedExceptionForService(circularDependenciesException, serviceInfo, out exceptionForService);
			}

			if(exceptionToCheck.InnerException is { } innerException)
			{
				return TryFindContainedExceptionForService(innerException, serviceInfo, out exceptionForService);
			}

			exceptionForService = null;
			return false;
		}

		private static bool TryFindContainedExceptionForService(CircularDependenciesException exceptionToCheck, ServiceInfo serviceInfo, out Exception exceptionForService)
		{
			if(exceptionToCheck.ServiceInfo == serviceInfo)
			{
				exceptionForService = exceptionToCheck;
				return true;
			}

			if(exceptionToCheck.InnerException is { } innerException)
			{
				return TryFindContainedExceptionForService(innerException, serviceInfo, out exceptionForService);
			}

			exceptionForService = null;
			return false;
		}

		private static bool TryFindContainedExceptionForService(AggregateException exceptionsToCheck, ServiceInfo serviceInfo, out Exception exceptionForService)
		{
			foreach(var innerException in exceptionsToCheck.InnerExceptions)
			{
				if(TryFindContainedExceptionForService(innerException, serviceInfo, out exceptionForService))
				{
					return true;
				}
			}

			exceptionForService = null;
			return false;
		}

		internal static ParameterInfo[] GetConstructorParameters(Type closedConcreteType)
		{
			var constructors = closedConcreteType.GetConstructors(BindingFlags.Instance | BindingFlags.Public);
			if(constructors.Length == 0)
			{
				constructors = closedConcreteType.GetConstructors(BindingFlags.Instance | BindingFlags.NonPublic);
			}

			return constructors.OrderByDescending(c => c.GetParameters().Length).FirstOrDefault()?.GetParameters() ?? Array.Empty<ParameterInfo>();
		}

		internal static async Task<(object[] arguments, int failedToGetArgumentAtIndex)> GetOrInitializeServices([DisallowNull] Type[] serviceTypes, [DisallowNull] HashSet<Type> initialized, [DisallowNull] LocalServices localServices, [MaybeNull] Component client)
		{
			int argumentCount = serviceTypes.Length;
			object[] arguments = new object[argumentCount];

			for(int argumentIndex = 0; argumentIndex < argumentCount; argumentIndex++)
			{
				if(!TryGetOrInitializeService(serviceTypes[argumentIndex], out arguments[argumentIndex], initialized, localServices, client:client))
				{
					return (Array.Empty<object>(), argumentIndex);
				}
			}

			for(int argumentIndex = 0; argumentIndex < argumentCount; argumentIndex++)
			{
				var parameterType = serviceTypes[argumentIndex];
				var argument = arguments[argumentIndex];

				if(argument is Task loadArgumentTask && !parameterType.IsInstanceOfType(argument))
				{
					await loadArgumentTask;
					argument = await loadArgumentTask.GetResult();
					arguments[argumentIndex] = argument;
				}
			}

			for(int argumentIndex = 0; argumentIndex < argumentCount; argumentIndex++)
			{
				arguments[argumentIndex] = await InjectCrossServiceDependencies(arguments[argumentIndex], serviceInfo: null, initialized, localServices);
			}

			return (arguments, -1);
		}

		internal static async Task<int> GetOrInitializeServices([DisallowNull] Type[] serviceTypes, [DisallowNull] HashSet<Type> initialized, [DisallowNull] LocalServices localServices, object[] arguments, [MaybeNull] Component client, int argumentsFirstServiceIndex = 0)
		{
			int serviceCount = serviceTypes.Length;
			for(int i = 0; i < serviceCount; i++)
			{
				if(!TryGetOrInitializeService(serviceTypes[i], out arguments[argumentsFirstServiceIndex + i], initialized, localServices, client: client))
				{
					return i;
				}
			}

			for(int parameterIndex = 0; parameterIndex < serviceCount; parameterIndex++)
			{
				var parameterType = serviceTypes[parameterIndex];
				var argument = arguments[parameterIndex];

				if(argument is Task loadArgumentTask && !parameterType.IsInstanceOfType(argument))
				{
					await loadArgumentTask;
					argument = await loadArgumentTask.GetResult();
					arguments[parameterIndex] = argument;
				}
			}

			for(int i = 0; i < serviceCount; i++)
			{
				arguments[i] = await InjectCrossServiceDependencies(arguments[i], serviceInfo: null, initialized, localServices);
			}

			return -1;
		}

		private static bool TargetIsAssignableOrConvertibleToType(IInitializer initializer, ServiceInfo serviceInfo)
		{
			if(serviceInfo.concreteType != null)
			{
				return initializer.TargetIsAssignableOrConvertibleToType(serviceInfo.concreteType);
			}

			foreach(var definingType in serviceInfo.definingTypes)
			{
				if(initializer.TargetIsAssignableOrConvertibleToType(definingType))
				{
					return true;
				}
			}

			return false;
		}

		/// <param name="service"> The service itself or a <see cref="Task{Object}"/> to get the service, if found; otherwise, null. </param>
		private static bool TryGetOrInitializeService(Type requestedServiceType, out object service, HashSet<Type> initialized, [DisallowNull] LocalServices localServices, [MaybeNull] Component client)
		{
			if(TryGetServiceFor(client, requestedServiceType, out service, initialized, localServices))
			{
				return true;
			}

			if(TryGetServiceInfo(requestedServiceType, out var serviceInfo))
			{
				#if DEV_MODE || DEBUG || INIT_ARGS_SAFE_MODE
				try
				{
				#endif

				var task = GetOrInitializeService(serviceInfo, initialized, localServices, closedConcreteType: null, requestedServiceType: requestedServiceType, client:client);
				service = task.IsCompleted ? task.GetResult() : task;

				#if DEV_MODE || DEBUG || INIT_ARGS_SAFE_MODE
				}
				catch(Exception exception) when (exception is not OperationCanceledException and not ServiceInitFailedException)
				{
					OnGetOrInitializeServiceFailed(exception, serviceInfo);
				}
				#endif

				return service is not null;
			}

			return false;
		}

		private static async
		#if UNITY_2023_1_OR_NEWER
		Awaitable<object>
		#else
		System.Threading.Tasks.Task<object>
		#endif
		InstantiateFromAsset(Type closedConcreteType, [DisallowNull] GameObject gameObject, ServiceInfo serviceInfo, HashSet<Type> initialized, [DisallowNull] LocalServices localServices, [MaybeNull] Component client)
		{
			if(TryGetServiceOrServiceProviderComponent(closedConcreteType, gameObject, serviceInfo, out Component component))
			{
				return await InstantiateFromAsset(closedConcreteType: closedConcreteType, serviceOrProvider: component, serviceInfo, initialized, localServices, client: client);
			}

			#if UNITY_6000_0_OR_NEWER
			if(serviceInfo.LoadAsync)
			{
				var instances = await Object.InstantiateAsync(gameObject);
				if(serviceInfo.DontDestroyOnLoad)
				{
					foreach(var instance in instances)
					{
						Object.DontDestroyOnLoad(instance);
					}
				}

				gameObject = instances[0];
			}
			else
			#endif
			{
				gameObject = Object.Instantiate(gameObject);
				if(serviceInfo.DontDestroyOnLoad)
				{
					Object.DontDestroyOnLoad(gameObject);
				}
			}

			return await GetServiceFromInstance(closedConcreteType:closedConcreteType, gameObject: gameObject, serviceInfo, client: client);
		}

		private static async Task<object> InstantiateFromAsset(Type closedConcreteType, Component serviceOrProvider, ServiceInfo serviceInfo, HashSet<Type> initialized, [DisallowNull] LocalServices localServices, [MaybeNull] Component client)
		{
			bool serviceIsAssignableFromAsset = serviceInfo.IsInstanceOf(serviceOrProvider);

			closedConcreteType ??= serviceInfo.concreteType;
			if(closedConcreteType is null)
			{
				if(serviceIsAssignableFromAsset)
				{
					closedConcreteType = serviceOrProvider.GetType();
				}
				else if(serviceOrProvider is IValueProvider valueProvider && valueProvider.TryGetFor(serviceOrProvider, out object value) && serviceInfo.IsInstanceOf(value))
				{
					closedConcreteType = value.GetType();
				}
				else if(serviceInfo.LoadAsync && serviceOrProvider is IValueProviderAsync valueProviderAsync)
				{
					value = await valueProviderAsync.GetForAsync(client);
					closedConcreteType = value?.GetType();
				}
				
				#if DEBUG || INIT_ARGS_SAFE_MODE
				if(closedConcreteType is null)
				{
					Debug.LogWarning($"Unable to determine concrete type of service {TypeUtility.ToString(serviceInfo.ConcreteOrDefiningType)} on '{serviceOrProvider.name}'.", serviceOrProvider);
					return null;
				}
				#endif
			}

			if(!initialized.Add(closedConcreteType))
			{
				if(services.TryGetValue(closedConcreteType, out object result))
				{
					return await GetServiceFromInstance(closedConcreteType, result, serviceInfo, initialized, localServices, client:client);
				}

				return null;
			}

			if(serviceIsAssignableFromAsset ? !ShouldInitialize(serviceOrProvider) : serviceOrProvider is IInitializer)
			{
				#if DEV_MODE && DEBUG_INIT_SERVICES
				Debug.Log($"Will not inject dependencies to service {TypeUtility.ToString(closedConcreteType)} because it should be able to acquire them independently.");
				#endif

				if(serviceInfo.ShouldInstantiate(true))
				{
					#if UNITY_6000_0_OR_NEWER
					if(serviceInfo.LoadAsync)
					{
						Object[] instances = await Object.InstantiateAsync(serviceOrProvider);
						if(serviceInfo.DontDestroyOnLoad)
						{
							foreach(var instance in instances)
							{
								TryMakeDontDestroyOnLoad(instance);
							}
						}
						
						return await GetServiceFromInstance(closedConcreteType, instances[0], serviceInfo, initialized, localServices, client:client);
					}
					#endif

					var componentInstance = Object.Instantiate(serviceOrProvider);
					if(serviceInfo.DontDestroyOnLoad)
					{
						TryMakeDontDestroyOnLoad(componentInstance);
					}
					return await GetServiceFromInstance(closedConcreteType, componentInstance, serviceInfo, initialized, localServices, client:client);
				}

				return await GetServiceFromInstance(closedConcreteType, serviceOrProvider, serviceInfo, initialized, localServices, client:client);
			}

			foreach(var parameterTypes in GetParameterTypesForAllInitMethods(closedConcreteType))
			{
				int parameterCount = parameterTypes.Length;
				object[] arguments = new object[parameterCount + 1];
				int failedToGetArgumentAtIndex = await GetOrInitializeServices(parameterTypes, initialized, localServices, arguments, client:client, 1);
				if(failedToGetArgumentAtIndex != -1)
				{
					LogMissingDependencyWarning(closedConcreteType, parameterTypes[failedToGetArgumentAtIndex], serviceOrProvider, localServices);
					continue;
				}

				arguments[0] = serviceOrProvider;

				var instantiateGenericArgumentTypes = new Type[parameterCount + 1];
				Array.Copy(parameterTypes, 0, instantiateGenericArgumentTypes, 1, parameterCount);
				instantiateGenericArgumentTypes[0] = closedConcreteType;

				for(int parameterIndex = 0; parameterIndex < parameterCount; parameterIndex++)
				{
					var parameterType = parameterTypes[parameterIndex];
					var argument = arguments[parameterIndex + 1];

					if(argument is Task loadArgumentTask && !parameterType.IsInstanceOfType(argument))
					{
						await loadArgumentTask;
						argument = await loadArgumentTask.GetResult();
						arguments[parameterIndex + 1] = argument;
					}
				}

				#if UNITY_6000_0_OR_NEWER
				if(serviceInfo.LoadAsync)
				{
					MethodInfo instantiateMethod =
						typeof(InstantiateExtensions).GetMember(nameof(InstantiateExtensions.InstantiateAsync), BindingFlags.Static | BindingFlags.Public)
							.Select(member => (MethodInfo)member)
							.FirstOrDefault(method => method.GetGenericArguments().Length == parameterCount + 1)
							.MakeGenericMethod(instantiateGenericArgumentTypes);

					#if DEV_MODE && DEBUG_INIT_SERVICES
					Debug.Log($"Injecting {parameterCount} dependencies to {TypeUtility.ToString(closedConcreteType)}.");
					#endif

					var unityObjectOrAsyncOperation = (AsyncInstantiateOperation)instantiateMethod.Invoke(null, arguments);
					await unityObjectOrAsyncOperation;
					var instance = unityObjectOrAsyncOperation.Result[0];
					if(serviceInfo.DontDestroyOnLoad)
					{
						TryMakeDontDestroyOnLoad(instance);
					}

					return await GetServiceFromInstance(closedConcreteType: closedConcreteType, instance: instance, serviceInfo, initialized, localServices, client:client);
				}
				else
				#endif
				{
					MethodInfo instantiateMethod =
						typeof(InstantiateExtensions).GetMember(nameof(InstantiateExtensions.Instantiate), BindingFlags.Static | BindingFlags.Public)
																	.Select(member => (MethodInfo)member)
																	.FirstOrDefault(method => method.GetGenericArguments().Length == parameterCount + 1 && method.GetParameters().Length == parameterCount + 1)
																	.MakeGenericMethod(instantiateGenericArgumentTypes);

					#if DEV_MODE && DEBUG_INIT_SERVICES
					Debug.Log($"Injecting {parameterCount} dependencies to {TypeUtility.ToString(closedConcreteType)}.");
					#endif

					var instance = (Object)instantiateMethod.Invoke(null, arguments);
					if(serviceInfo.DontDestroyOnLoad)
					{
						TryMakeDontDestroyOnLoad(instance);
					}

					return await GetServiceFromInstance(closedConcreteType: closedConcreteType, instance: instance, serviceInfo, initialized, localServices, client:client);
				}
			}

			#if UNITY_6000_0_OR_NEWER
			if(serviceInfo.LoadAsync)
			{
				Object[] instances = await Object.InstantiateAsync(serviceOrProvider);
				if(serviceInfo.DontDestroyOnLoad)
				{
					foreach(var instance in instances)
					{
						TryMakeDontDestroyOnLoad(instance);
					}
				}
				return await GetServiceFromInstance(closedConcreteType: closedConcreteType, instance: instances[0], serviceInfo, initialized, localServices, client:client);
			}
			#endif

			var componentInstance2 = Object.Instantiate(serviceOrProvider);
			if(serviceInfo.DontDestroyOnLoad)
			{
				TryMakeDontDestroyOnLoad(componentInstance2);
			}
			return await GetServiceFromInstance(closedConcreteType: closedConcreteType, instance: componentInstance2, serviceInfo, initialized, localServices, client:client);
		}

		private static void TryMakeDontDestroyOnLoad(object instance)
		{
			if(Find.GameObjectOf(instance, out GameObject gameObject) && !gameObject.transform.parent)
			{
				Object.DontDestroyOnLoad(gameObject);
			}
		}

		private static async Task<object> AddComponent(ServiceInfo serviceInfo, HashSet<Type> initialized, [DisallowNull] LocalServices localServices, [MaybeNull] Component client)
		{
			var concreteType = serviceInfo.concreteType;

			if(!initialized.Add(concreteType))
			{
				return services.TryGetValue(concreteType, out object result) ? result : null;
			}

			if(!ShouldInitialize(concreteType))
			{
				#if DEV_MODE && DEBUG_INIT_SERVICES
				Debug.Log($"Will not inject dependencies to service {TypeUtility.ToString(concreteType)} because it should be able to acquire them independently.");
				#endif

				return container.AddComponent(concreteType);
			}

			foreach(var parameterTypes in GetParameterTypesForAllInitMethods(concreteType))
			{
				int parameterCount = parameterTypes.Length;
				object[] arguments = new object[parameterCount + 1];
				int failedToGetArgumentAtIndex = await GetOrInitializeServices(parameterTypes, initialized, localServices, arguments, client:client, 1);
				if(failedToGetArgumentAtIndex != -1)
				{
					LogMissingDependencyWarning(concreteType, parameterTypes[failedToGetArgumentAtIndex], container, localServices);
					continue;
				}

				if(!container)
				{
					CreateServicesContainer();
				}

				arguments[0] = container;

				for(int parameterIndex = 0; parameterIndex < parameterCount; parameterIndex++)
				{
					var parameterType = parameterTypes[parameterIndex];
					var argument = arguments[parameterIndex + 1];

					if(argument is Task loadArgumentTask && !parameterType.IsInstanceOfType(argument))
					{
						await loadArgumentTask;
						argument = await loadArgumentTask.GetResult();
						arguments[parameterIndex + 1] = argument;
					}
				}

				var genericArgumentTypes = new Type[parameterCount + 1];
				Array.Copy(parameterTypes, 0, genericArgumentTypes, 1, parameterCount);
				genericArgumentTypes[0] = concreteType;

				MethodInfo method;
				method = typeof(AddComponentExtensions).GetMember(nameof(AddComponentExtensions.AddComponent), BindingFlags.Static | BindingFlags.Public)
																.Select(member => (MethodInfo)member)
																.FirstOrDefault(method => method.GetGenericArguments().Length == parameterCount + 1 && method.GetParameters().Length == parameterCount + 1)
																.MakeGenericMethod(genericArgumentTypes);

				#if DEV_MODE && DEBUG_CREATE_SERVICES
				Debug.Log($"Service {TypeUtility.ToString(serviceInfo.ConcreteOrDefiningType)} attached to '{container.name}' and initialized with {parameterCount} dependencies successfully.", container);
				#endif

				return method.Invoke(null, arguments);
			}

			return container.AddComponent(concreteType);
		}

		internal static IEnumerable<Type[]> GetParameterTypesForAllInitMethods([DisallowNull] Type clientConcreteType)
		{
			#if DEV_MODE
			Debug.Assert(clientConcreteType is not null);
			#endif

			var interfaceTypes = clientConcreteType.GetInterfaces();
			for(int i = interfaceTypes.Length - 1; i >= 0; i--)
			{
				var interfaceType = interfaceTypes[i];
				if(interfaceType.IsGenericType && argumentCountsByIArgsTypeDefinition.ContainsKey(interfaceType.GetGenericTypeDefinition()))
				{
					yield return interfaceType.GetGenericArguments();
				}
			}

			var parameters = GetConstructorParameters(clientConcreteType);
			if(parameters.Length > 0)
			{
				yield return parameters.Select(p => p.ParameterType).ToArray();
			}
		}

		private static async Task<object> InstantiateFromAsset(Type closedConcreteType, ScriptableObject serviceOrProvider, ServiceInfo serviceInfo, HashSet<Type> initialized, [DisallowNull] LocalServices localServices, [MaybeNull] Component client)
		{
			closedConcreteType ??= serviceInfo.concreteType;
			bool serviceIsAssignableFromAsset = closedConcreteType?.IsInstanceOfType(serviceOrProvider) ?? serviceInfo.IsInstanceOf(serviceOrProvider);
			
			if(closedConcreteType is null)
			{
				if(serviceIsAssignableFromAsset)
				{
					closedConcreteType = serviceOrProvider.GetType();
				}
				else if(serviceOrProvider is IValueProvider valueProvider && valueProvider.Value is object value && serviceInfo.IsInstanceOf(value))
				{
					closedConcreteType = value.GetType();
				}
				else
				{
					Debug.LogWarning($"Unable to determine concrete type of service {TypeUtility.ToString(serviceInfo.ConcreteOrDefiningType)} on '{serviceOrProvider.name}'.", serviceOrProvider);
					return null;
				}
			}

			if(!initialized.Add(closedConcreteType))
			{
				return services.TryGetValue(closedConcreteType, out object result) ? result : null;
			}

			if(serviceIsAssignableFromAsset ? !ShouldInitialize(serviceOrProvider) : serviceOrProvider is IInitializer)
			{
				#if DEV_MODE && DEBUG_INIT_SERVICES
				Debug.Log($"Will not inject dependencies to service {TypeUtility.ToString(closedConcreteType)} because it should be able to acquire them independently.");
				#endif

				if(serviceInfo.ShouldInstantiate(false))
				{
					#if UNITY_6000_0_OR_NEWER
					if(serviceInfo.LoadAsync)
					{
						Object[] instances = await Object.InstantiateAsync(serviceOrProvider);
						return instances[0];
					}
					#endif

					return Object.Instantiate(serviceOrProvider);
				}

				return serviceOrProvider;
			}

			foreach(var parameterTypes in GetParameterTypesForAllInitMethods(closedConcreteType))
			{
				int parameterCount = parameterTypes.Length;
				var arguments = new object[parameterCount + 1];
				int failedToGetArgumentAtIndex = await GetOrInitializeServices(parameterTypes, initialized, localServices, arguments, client:client, 1);
				if(failedToGetArgumentAtIndex != -1)
				{
					LogMissingDependencyWarning(closedConcreteType, parameterTypes[failedToGetArgumentAtIndex], serviceOrProvider, localServices);
					continue;
				}

				arguments[0] = serviceOrProvider;

				var instantiateGenericArgumentTypes = new Type[parameterCount + 1];
				Array.Copy(parameterTypes, 0, instantiateGenericArgumentTypes, 1, parameterCount);
				instantiateGenericArgumentTypes[0] = closedConcreteType;

				for(int parameterIndex = 0; parameterIndex < parameterCount; parameterIndex++)
				{
					var parameterType = parameterTypes[parameterIndex];
					var argument = arguments[parameterIndex + 1];

					if(argument is Task loadArgumentTask && !parameterType.IsInstanceOfType(argument))
					{
						await loadArgumentTask;
						argument = await loadArgumentTask.GetResult();
						arguments[parameterIndex + 1] = argument;
					}
				}

				MethodInfo instantiateMethod =
					#if UNITY_6000_0_OR_NEWER
					serviceInfo.LoadAsync
					? typeof(InstantiateExtensions).GetMember(nameof(InstantiateExtensions.InstantiateAsync), BindingFlags.Static | BindingFlags.Public)
																.Select(member => (MethodInfo)member)
																.FirstOrDefault(method => method.GetGenericArguments().Length == parameterCount + 1)
																.MakeGenericMethod(instantiateGenericArgumentTypes) :
					#endif
					typeof(InstantiateExtensions).GetMember(nameof(InstantiateExtensions.Instantiate), BindingFlags.Static | BindingFlags.Public)
																.Select(member => (MethodInfo)member)
																.FirstOrDefault(method => method.GetGenericArguments().Length == parameterCount + 1 && method.GetParameters().Length == parameterCount + 1)
																.MakeGenericMethod(instantiateGenericArgumentTypes);

				#if DEV_MODE && DEBUG_INIT_SERVICES
				Debug.Log($"Injecting {parameterCount} dependencies to {TypeUtility.ToString(closedConcreteType)}.");
				#endif

				return instantiateMethod.Invoke(null, arguments);
			}

			#if UNITY_6000_0_OR_NEWER
			if(serviceInfo.LoadAsync)
			{
				Object[] instances = await Object.InstantiateAsync(serviceOrProvider);
				return instances[0];
			}
			#endif

			return Object.Instantiate(serviceOrProvider);
		}

		/// <summary>
		/// Tries to get a component from the game object that matches the service info.
		/// <para>
		/// Failing that, tries to get a component that can provide a value that matches the service info,
		/// such as a Wrapper or an Initializer.
		/// </para>
		/// </summary>
		private static bool TryGetServiceOrServiceProviderComponent(Type closedConcreteType, [DisallowNull] GameObject gameObject, [DisallowNull] ServiceInfo serviceInfo, [NotNullWhen(true), MaybeNullWhen(false)] out Component result)
		{
			closedConcreteType ??= serviceInfo.concreteType;
			if(closedConcreteType is not null && Find.typesToComponentTypes.TryGetValue(closedConcreteType, out var componentTypes))
			{
				for(int i = componentTypes.Length - 1; i >= 0; i--)
				{
					if(gameObject.TryGetComponent(componentTypes[i], out result))
					{
						return true;
					}
				}
			}
			else
			{
				foreach(var definingType in serviceInfo.definingTypes)
				{
					if(definingType != closedConcreteType && Find.typesToComponentTypes.TryGetValue(definingType, out componentTypes))
					{
						for(int i = componentTypes.Length - 1; i >= 0; i--)
						{
							if(gameObject.TryGetComponent(componentTypes[i], out Component component)
								&& Array.TrueForAll(serviceInfo.definingTypes, t => t.IsInstanceOfType(component)))
							{
								result = component;
								return true;
							}
						}
					}
				}
			}

			if(serviceInfo.classWithAttribute != closedConcreteType && Find.typesToComponentTypes.TryGetValue(serviceInfo.classWithAttribute, out componentTypes))
			{
				for(int i = componentTypes.Length - 1; i >= 0; i--)
				{
					if(gameObject.TryGetComponent(componentTypes[i], out result))
					{
						return true;
					}
				}
			}

			var valueProviders = gameObject.GetComponents<IValueProvider>();
			foreach(var valueProvider in valueProviders)
			{
				var value = valueProvider.Value;
				if(value != null && serviceInfo.IsInstanceOf(value))
				{
					if(value is Component component)
					{
						result = component;
						return true;
					}

					if(Find.WrapperOf(value, out IWrapper wrapper))
					{
						result = wrapper as Component;
						return result;
					}
				}
			}

			foreach(var valueProvider in valueProviders)
			{
				if(valueProvider is IInitializer initializer && TargetIsAssignableOrConvertibleToType(initializer, serviceInfo))
				{
					result = initializer as Component;
					return true;
				}
			}

			result = null;
			return false;
		}

		private static async Task<object> GetServiceFromInstance([AllowNull] Type closedConcreteType, [DisallowNull] object instance, [DisallowNull] ServiceInfo serviceInfo, HashSet<Type> initialized, LocalServices localServices, [MaybeNull] Component client)
		{
			if(instance is Task task)
			{
				instance = await task.GetResult();
			}

			if(closedConcreteType is not null && closedConcreteType.IsInstanceOfType(instance))
			{
				return instance;
			}

			if(serviceInfo.IsInstanceOf(instance))
			{
				return instance;
			}

			if(instance is GameObject gameObject)
			{
				return await GetServiceFromInstance(closedConcreteType, gameObject, serviceInfo, client: client);
			}

			switch(serviceInfo.serviceProviderType)
			{
				case ServiceProviderType.Initializer:
					if(instance is IInitializer initializer && TargetIsAssignableOrConvertibleToType(initializer, serviceInfo))
					{
						object result;

						#if DEV_MODE || DEBUG || INIT_ARGS_SAFE_MODE
						try
						{
						#endif

						result = await initializer.InitTargetAsync();

						#if DEV_MODE || DEBUG || INIT_ARGS_SAFE_MODE
						}
						catch(Exception exception) when (exception is not OperationCanceledException)
						{
							throw ServiceInitFailedException.Create(serviceInfo, ServiceInitFailReason.InitializerThrewException, exception: exception, initializerOrWrapper: initializer, concreteType: closedConcreteType);
						}
						#endif

						OnAfterAwait(closedConcreteType);

						return result;
					}

					#if DEV_MODE
					Debug.LogWarning($"serviceProviderType was Initializer but instance was {TypeUtility.ToString(instance?.GetType())}.", GetContext(serviceInfo));
					#endif
					break;
				case ServiceProviderType.ServiceInitializer:
					if(instance is IServiceInitializer serviceInitializer)
					{
						if(serviceInitializer.InitTarget() is { } result)
						{
							return result;
						}
					}
					break;
				case ServiceProviderType.ServiceInitializerAsync:
					if(instance is IServiceInitializerAsync serviceInitializerAsync)
					{
						if(await CreateUsingServiceInitializerAsync(closedConcreteType, serviceInfo, initialized, localServices, serviceInitializerAsync, client: client) is { } result)
						{
							return result;
						}
					}

					#if DEV_MODE
					Debug.LogWarning($"serviceProviderType was ServiceInitializerAsync but instance was {TypeUtility.ToString(instance?.GetType())}.", GetContext(serviceInfo));
					#endif
					break;
				case ServiceProviderType.Wrapper:
					if(instance is IWrapper { WrappedObject: { } wrappedObject } && serviceInfo.IsInstanceOf(wrappedObject))
					{
						return wrappedObject;
					}
					break;
				case ServiceProviderType.IValueProviderT:
				case ServiceProviderType.IValueByTypeProvider:
					if(ValueProviderUtility.TryGetValueProviderValue(instance, closedConcreteType ?? serviceInfo.ConcreteOrDefiningType, client:client, out var providedValue))
					{
						OnAfterGetValueFromValueProvider(instance, client, providedValue);
						return providedValue;
					}
					break;
				case ServiceProviderType.IValueProvider:
					if(ValueProviderUtility.TryGetValueProviderValue(instance, closedConcreteType ?? serviceInfo.ConcreteOrDefiningType, client:client, out providedValue))
					{
						OnAfterGetValueFromValueProvider(instance, client, providedValue);

						if(serviceInfo.IsInstanceOf(providedValue))
						{
							return providedValue;
						}

						#if DEV_MODE
						Debug.Log($"IValueProvider {TypeUtility.ToString(instance?.GetType())} provided value of type {TypeUtility.ToString(providedValue?.GetType())} which was not assignable to defining type(s) {TypeUtility.ToString(serviceInfo.definingTypes)}. Attempting to acquire value from it...");
						#endif

						#if DEV_MODE || DEBUG || INIT_ARGS_SAFE_MODE
						if(providedValue is null)
						{
							throw ServiceInitFailedException.Create(serviceInfo, ServiceInitFailReason.ServiceProviderResultNull, initializerOrWrapper: instance, concreteType: closedConcreteType, localServices: localServices);
						}
						#endif

						var serviceFromProvidedValue = await GetServiceFromInstance(closedConcreteType, providedValue, serviceInfo, initialized, localServices, client);

						#if DEV_MODE || DEBUG || INIT_ARGS_SAFE_MODE
						if(!serviceInfo.IsInstanceOf(serviceFromProvidedValue))
						{
							throw ServiceInitFailedException.Create(serviceInfo, ServiceInitFailReason.ServiceProviderResultNotConvertible, initializerOrWrapper: instance, concreteType: closedConcreteType, localServices: localServices);
						}
						#endif

						return serviceFromProvidedValue;
					}
					break;
				case ServiceProviderType.IValueProviderAsyncT:
				case ServiceProviderType.IValueByTypeProviderAsync:
					providedValue = await ValueProviderUtility.GetValueProviderValueAsync(instance, closedConcreteType ?? serviceInfo.ConcreteOrDefiningType, client:client);
					OnAfterGetValueFromAsyncValueProvider(instance, client, providedValue);
					if(providedValue is not null)
					{
						return providedValue;
					}
					break;
				case ServiceProviderType.IValueProviderAsync:
					providedValue = await ValueProviderUtility.GetValueProviderValueAsync(instance, closedConcreteType ?? serviceInfo.ConcreteOrDefiningType, client:client);
					OnAfterGetValueFromAsyncValueProvider(instance, client, providedValue);
					if(serviceInfo.IsInstanceOf(providedValue))
					{
						return instance;
					}

					#if DEV_MODE
					Debug.Log($"IValueProviderAsync {TypeUtility.ToString(instance?.GetType())} provided value of type {TypeUtility.ToString(providedValue?.GetType())} which was not assignable to defining type(s) {TypeUtility.ToString(serviceInfo.definingTypes)}. Attempting to acquire value from it...");
					#endif

					#if DEV_MODE || DEBUG || INIT_ARGS_SAFE_MODE
					if(providedValue is null)
					{
						throw ServiceInitFailedException.Create(serviceInfo, ServiceInitFailReason.ServiceProviderResultNull, initializerOrWrapper: instance, concreteType: closedConcreteType, localServices: localServices);
					}
					#endif

					var serviceFromAsyncProvidedValue = await GetServiceFromInstance(closedConcreteType, providedValue, serviceInfo, initialized, localServices, client);

					#if DEV_MODE || DEBUG || INIT_ARGS_SAFE_MODE
					if(!serviceInfo.IsInstanceOf(serviceFromAsyncProvidedValue))
					{
						throw ServiceInitFailedException.Create(serviceInfo, ServiceInitFailReason.ServiceProviderResultNotConvertible, initializerOrWrapper: instance, concreteType: closedConcreteType, localServices: localServices);
					}
					#endif

					return serviceFromAsyncProvidedValue;
			}

			if(instance is IInitializer initializer2 && TargetIsAssignableOrConvertibleToType(initializer2, serviceInfo))
			{
				object result;

				#if DEV_MODE || DEBUG || INIT_ARGS_SAFE_MODE
				try
				{
				#endif

				result = await initializer2.InitTargetAsync();

				#if DEV_MODE || DEBUG || INIT_ARGS_SAFE_MODE
				}
				catch(Exception exception) when (exception is not OperationCanceledException)
				{
					throw ServiceInitFailedException.Create(serviceInfo, ServiceInitFailReason.InitializerThrewException, exception: exception, initializerOrWrapper: initializer2, concreteType: closedConcreteType);
				}
				#endif

				OnAfterAwait(closedConcreteType);

				return result;
			}

			if(instance is IWrapper { WrappedObject: { } wrappedObject2 } && serviceInfo.IsInstanceOf(wrappedObject2))
			{
				return wrappedObject2;
			}

			return null;
		}

		/// <summary>
		/// Handles disposing/releasing value provided by an async value provider
		/// and throwing a <see cref="TaskCanceledException"/> if we have exited Play Mode while awaiting the value.
		/// </summary>
		/// <param name="valueProvider"></param>
		/// <param name="client"></param>
		/// <param name="value"></param>
		/// <exception cref="TaskCanceledException">
		/// Thrown in the Editor if we have exited Play Mode while awaiting the value.
		/// </exception>
		private static void OnAfterGetValueFromAsyncValueProvider(object valueProvider, Component client, object value)
		{
			#if UNITY_EDITOR
			if(!Application.isPlaying)
			{
				if(valueProvider is IValueReleaser valueReleaser)
				{
					valueReleaser.Release(client, value);
				}
				else if(value is IDisposable disposable)
				{
					disposable.Dispose();
				}
				else if(value is IAsyncDisposable asyncDisposable)
				{
					asyncDisposable.DisposeAsync().AsTask().ContinueWith(t =>
					{
						Debug.LogError($"Failed to dispose async value provided by {TypeUtility.ToString(valueProvider.GetType())}: {t.Exception}");
					}, TaskContinuationOptions.OnlyOnFaulted);
				}

				throw new TaskCanceledException($"Abort initialization of service {TypeUtility.ToString(value?.GetType())} acquired asynchronously via {TypeUtility.ToString(valueProvider?.GetType())} because no longer in Play Mode.");
			}
			#endif
			OnAfterGetValueFromValueProvider(valueProvider, client, value);
		}

		private static void OnAfterGetValueFromValueProvider(object valueProvider, Component client, object value)
		{
			if(valueProvider is IValueReleaser valueReleaser)
			{
				disposables.Add(new ValueReleaser(valueReleaser, client, value));
			}
			else if(valueProvider is IValueByTypeReleaser valueByTypeReleaser)
			{
				disposables.Add(new ValueByTypeReleaser(valueByTypeReleaser, client, value));
			}
			else if(value is IDisposable disposable)
			{
				disposables.Add(disposable);
			}
			else if(value is IAsyncDisposable asyncDisposable)
			{
				disposables.Add(new AsyncDisposableDisposer(asyncDisposable));
			}
		}

		[Conditional("UNITY_EDITOR"), MethodImpl(MethodImplOptions.AggressiveInlining)]
		private static void OnAfterAwait(Type serviceType)
		{
			#if UNITY_EDITOR
			if(!Application.isPlaying)
			{
				throw new TaskCanceledException($"Abort async initialization of service {TypeUtility.ToString(serviceType)} because no longer in Play Mode.");
			}
			#endif
		}
		
		[Conditional("UNITY_EDITOR"), MethodImpl(MethodImplOptions.AggressiveInlining)]
		private static void OnAfterAwait(ServiceInfo service)
		{
			#if UNITY_EDITOR
			if(!Application.isPlaying)
			{
				throw new TaskCanceledException($"Abort async initialization of service {TypeUtility.ToString(service.ConcreteOrDefiningType)} because no longer in Play Mode.");
			}
			#endif
		}

		[Conditional("UNITY_EDITOR"), MethodImpl(MethodImplOptions.AggressiveInlining)]
		private static void OnAfterAwait()
		{
			#if UNITY_EDITOR
			if(!Application.isPlaying)
			{
				throw new TaskCanceledException();
			}
			#endif
		}

		/// <summary>
		/// Tries to get a component from the game object that matches the service info.
		/// <para>
		/// Failing that, tries to get a component that can provide a value that matches the service info,
		/// such as a Wrapper or an Initializer.
		/// </para>
		/// </summary>
		private static async Task<object> GetServiceFromInstance([AllowNull] Type closedConcreteType, [DisallowNull] GameObject gameObject, [DisallowNull] ServiceInfo serviceInfo, [MaybeNull] Component client)
		{
			closedConcreteType ??= serviceInfo.concreteType;
			if(closedConcreteType is not null && Find.In(gameObject, serviceInfo.concreteType, out var found))
			{
				return found;
			}

			foreach(var definingType in serviceInfo.definingTypes)
			{
				if(definingType != closedConcreteType && Find.In(gameObject, definingType, out found) && serviceInfo.IsInstanceOf(found))
				{
					return found;
				}
			}

			if(serviceInfo.classWithAttribute != closedConcreteType && Find.In(gameObject, serviceInfo.classWithAttribute, out var provider))
			{
				if(provider is IInitializer initializer && TargetIsAssignableOrConvertibleToType(initializer, serviceInfo))
				{
					object result;

					#if DEV_MODE || DEBUG || INIT_ARGS_SAFE_MODE
					try
					{
					#endif

					result = await initializer.InitTargetAsync();

					#if DEV_MODE || DEBUG || INIT_ARGS_SAFE_MODE
					}
					catch(Exception exception) when (exception is not OperationCanceledException)
					{
						throw ServiceInitFailedException.Create(serviceInfo, ServiceInitFailReason.InitializerThrewException, exception: exception, initializerOrWrapper: initializer, concreteType: closedConcreteType);
					}
					#endif

					OnAfterAwait(closedConcreteType);

					return result;
				}

				if(provider is IWrapper { WrappedObject: { } wrappedObject } && serviceInfo.IsInstanceOf(wrappedObject))
				{
					return wrappedObject;
				}

				if(serviceInfo.LoadAsync && provider is IValueProviderAsync valueProviderAsync)
				{
					return await valueProviderAsync.GetForAsync(client);
				}

				if(provider is IValueProvider valueProvider && valueProvider.TryGetFor(client, out var value) && serviceInfo.IsInstanceOf(value))
				{
					return value;
				}
			}

			var valueProviders = gameObject.GetComponents<IValueProvider>();
			foreach(var valueProvider in valueProviders)
			{
				if(valueProvider is IInitializer initializer && TargetIsAssignableOrConvertibleToType(initializer, serviceInfo))
				{
					object result;

					#if DEV_MODE || DEBUG || INIT_ARGS_SAFE_MODE
					try
					{
					#endif

					result = await initializer.InitTargetAsync();

					#if DEV_MODE || DEBUG || INIT_ARGS_SAFE_MODE
					}
					catch(Exception exception) when (exception is not OperationCanceledException)
					{
						throw ServiceInitFailedException.Create(serviceInfo, ServiceInitFailReason.InitializerThrewException, exception: exception, initializerOrWrapper: initializer, concreteType: closedConcreteType);
					}
					#endif

					OnAfterAwait(closedConcreteType);

					return result;
				}

				if(valueProvider is IWrapper { WrappedObject: { } wrappedObject } && serviceInfo.IsInstanceOf(wrappedObject))
				{
					return wrappedObject;
				}
			}

			foreach(var valueProvider in valueProviders)
			{
				if(valueProvider.TryGetFor(client, out var value) && serviceInfo.IsInstanceOf(value))
				{
					return value;
				}
			}

			return null;
		}

		/// <summary>
		/// Tries to get a component from the game object that matches the service info.
		/// <para>
		/// Failing that, tries to get a component that can provide a value that matches the service info,
		/// such as a Wrapper or an Initializer.
		/// </para>
		/// </summary>
		private static async Task<object> GetServiceAsync([AllowNull] Type concreteType, [DisallowNull] ScriptableObject scriptableObject, [DisallowNull] ServiceInfo serviceInfo, [MaybeNull] Component client)
		{
			concreteType ??= serviceInfo.concreteType;
			if(concreteType is not null && Find.In(scriptableObject, concreteType, out object found))
			{
				return found;
			}

			foreach(var definingType in serviceInfo.definingTypes)
			{
				if(definingType != concreteType && Find.In(scriptableObject, definingType, out found) && serviceInfo.IsInstanceOf(found))
				{
					return found;
				}
			}

			if(serviceInfo.classWithAttribute != concreteType && Find.In(scriptableObject, serviceInfo.classWithAttribute, out var provider))
			{
				if(provider is IInitializer initializer && TargetIsAssignableOrConvertibleToType(initializer, serviceInfo))
				{
					object result;

					#if DEV_MODE || DEBUG || INIT_ARGS_SAFE_MODE
					try
					{
					#endif

					result = await initializer.InitTargetAsync();

					#if DEV_MODE || DEBUG || INIT_ARGS_SAFE_MODE
					}
					catch(Exception exception) when (exception is not OperationCanceledException)
					{
						throw ServiceInitFailedException.Create(serviceInfo, ServiceInitFailReason.InitializerThrewException, exception: exception, initializerOrWrapper: initializer, concreteType: concreteType);
					}
					#endif

					OnAfterAwait(concreteType);

					return result;
				}

				if(provider is IWrapper { WrappedObject: { } wrappedObject } && serviceInfo.IsInstanceOf(wrappedObject))
				{
					return wrappedObject;
				}

				if(provider is IValueProvider valueProvider && valueProvider.TryGetFor(client, out var value) && serviceInfo.IsInstanceOf(value))
				{
					return value;
				}

				if(serviceInfo.LoadAsync && provider is IValueProviderAsync valueProviderAsync)
				{
					return await valueProviderAsync.GetForAsync(client);
				}
			}

			return null;
		}

		private static bool TryGetServiceFor([AllowNull] Component client, Type requestedServiceType, out object service, HashSet<Type> initialized, [DisallowNull] LocalServices localServices)
		{
			if(services.TryGetValue(requestedServiceType, out service))
			{
				return true;
			}

			if(!uninitializedServices.TryGetValue(requestedServiceType, out var serviceInfo)
				|| Array.IndexOf(serviceInfo.definingTypes, requestedServiceType) == -1)
			{
				// Also try to find scene from ServiceTag and Services components in the scene.
				if(localServices.TryGetInfo(requestedServiceType, out var localServiceInfo)
				&& (localServiceInfo.toClients == Clients.Everywhere
				|| (localServiceInfo.serviceOrProvider is Component serviceOrProviderComponent
				&& serviceOrProviderComponent
				&& Service.IsAccessibleTo(client as Transform, serviceOrProviderComponent.transform, localServiceInfo.toClients))))
				{
					service = localServiceInfo.serviceOrProvider;
					if(service is IWrapper { WrappedObject: { } wrappedObject } && requestedServiceType.IsInstanceOfType(wrappedObject))
					{
						service = wrappedObject;
					}

					return true;
				}

				if(!requestedServiceType.IsGenericType)
				{
					return false;
				}

				// Handle open generic types.
				// E.g. with attribute [Service(typeof(ILogger<>))] on class Logger<T>,
				// when ILogger<Client> is requested, should return new Logger<Client>.
				var definingTypeDefinition = requestedServiceType.GetGenericTypeDefinition();
				if(!uninitializedServices.TryGetValue(definingTypeDefinition, out serviceInfo))
				{
					return false;
				}
			}

			#if DEV_MODE && DEBUG_LAZY_INIT
			Debug.Log($"Initializing service {TypeUtility.ToString(requestedServiceType)} with LazyInit=true because it is a dependency of another service.");
			#endif

			HandleRemoveFromUninitializedServices(serviceInfo);

			var initializeServiceTask = InitializeServiceAsync(GetConcreteAndClosedType(serviceInfo, requestedServiceType), serviceInfo.serviceProviderType, serviceInfo.loadMethod, serviceInfo.referenceType, serviceInfo, initialized, localServices, requestedServiceType, client: client);
			if(initializeServiceTask.IsCompleted)
			{
				service = initializeServiceTask.Result;
				if(service is null)
				{
					return false;
				}

				FinalizeServiceImmediate(serviceInfo, service);
				return true;
			}

			var task = FinalizeServiceAsync(serviceInfo, initializeServiceTask);
			service = task.IsCompleted ? task.Result : task;
			return true;
		}

		private static void HandleRemoveFromUninitializedServices(ServiceInfo serviceInfo)
		{
			if(serviceInfo is { IsTransient: false, HasClosedConcreteType: true })
			{
				#if DEV_MODE && DEBUG_LAZY_INIT
				Debug.Log($"uninitializedServices.Remove({TypeUtility.ToString(serviceInfo.ConcreteOrDefiningType)}) because serviceInfo.IsTransient is false and HasClosedConcreteType is true");
				#endif

				uninitializedServices.Remove(serviceInfo.concreteType);

				foreach(var serviceInfoDefiningType in serviceInfo.definingTypes)
				{
					#if DEV_MODE && DEBUG_LAZY_INIT
					Debug.Log($"uninitializedServices.Remove({TypeUtility.ToString(serviceInfoDefiningType)}) because serviceInfo.IsTransient is false and HasClosedConcreteType is true");
					#endif

					uninitializedServices.Remove(serviceInfoDefiningType);
				}
			}
		}

		private static async Task<object> FinalizeServiceAsync(ServiceInfo serviceInfo, Task<object> initializeServiceTask)
		{
			object service;
			#if DEV_MODE || DEBUG || INIT_ARGS_SAFE_MODE
			try
			{
			#endif

			service = await initializeServiceTask;
			OnAfterAwait(serviceInfo);

			#if DEV_MODE || DEBUG || INIT_ARGS_SAFE_MODE
			}
			catch(Exception exception) when (exception is not OperationCanceledException and not ServiceInitFailedException)
			{
				throw ServiceInitFailedException.Create(serviceInfo, ServiceInitFailReason.ExceptionWasThrown, exception: exception);
			}
			#endif
			
			#if UNITY_EDITOR
			if(!Application.isPlaying)
			{
				throw new TaskCanceledException("Aborted initialization of async service because no longer in Play Mode.");
			}
			#endif

			FinalizeServiceImmediate(serviceInfo, service);
			return service;
		}

		/// <exception cref="TargetInvocationException">
		/// Thrown if an exception is thrown during execution of <see cref="Service.Set{TInstance}"/>.
		/// This can happen if an exception occurs in an event handler listening to the
		/// <see cref="ServiceChanged{T}.listeners"/> event.
		/// </exception>
		private static object FinalizeServiceImmediate(ServiceInfo serviceInfo, object service)
		{
			if(!serviceInfo.IsTransient)
			{
				#if DEV_MODE || DEBUG || INIT_ARGS_SAFE_MODE
				if(serviceInfo.serviceProviderType is ServiceProviderType.None && !serviceInfo.IsInstanceOf(service))
				{
					Debug.LogWarning($"Instance {TypeUtility.ToString(service.GetType())} is not assignable to all the configured defining types of the service, and is not a service provider." +
					$"Concrete type: {TypeUtility.ToString(serviceInfo.concreteType)}\n" +
					$"Defining types: {TypeUtility.ToString(serviceInfo.definingTypes)}", service is Object unityObject && unityObject ? unityObject : GetContext(serviceInfo));
				}
				#endif

				SetInstanceSync(serviceInfo, service);
			}

			if(ServicesAreReady && !serviceInfo.FindFromScene)
			{
				SubscribeToUpdateEvents(service);
				ExecuteAwake(service);
				ExecuteOnEnable(service);
				ExecuteStartAtEndOfFrame(service);
			}

			return service;
		}

		/// <exception cref="TargetInvocationException">
		/// Thrown if an exception is thrown during execution of <see cref="Service.Set{TInstance}"/>.
		/// This can happen if an exception occurs in an event handler listening to the
		/// <see cref="ServiceChanged{T}.listeners"/> event.
		/// </exception>
		private static void FinalizeServiceProviderImmediate(ServiceInfo serviceInfo, object serviceProvider)
		{
			var serviceProviderClosedConcreteType = serviceProvider.GetType();
			services[serviceProviderClosedConcreteType] = serviceProvider;

			if(serviceInfo.classWithAttribute == serviceProviderClosedConcreteType)
			{
				// Even if IsTransient is true, we can still cache the service provider
				foreach(var definingType in serviceInfo.definingTypes)
				{
					if(!definingType.ContainsGenericParameters)
					{
						services[definingType] = serviceProvider;
					}
				}
				
				foreach(var definingType in serviceInfo.definingTypes)
				{
					if(definingType.ContainsGenericParameters)
					{
						continue;
					}

					if(serviceInfo.serviceProviderType is ServiceProviderType.ServiceInitializer or ServiceProviderType.Initializer or ServiceProviderType.ServiceInitializerAsync)
					{
						continue;
					}

					if(!container)
					{
						CreateServicesContainer();
					}

					ServiceUtility.AddFor(serviceProvider, serviceInfo.serviceProviderType, definingType, Clients.Everywhere, container.transform);
				}
			}

			SubscribeToUpdateEvents(serviceProvider);
			ExecuteAwake(serviceProvider);
			ExecuteOnEnable(serviceProvider);
			ExecuteStartAtEndOfFrame(serviceProvider);
		}

		/// <exception cref="TargetInvocationException">
		/// Thrown if an exception is thrown during execution of <see cref="Service.Set{TInstance}"/>.
		/// This can happen if an exception occurs in an event handler listening to the
		/// <see cref="ServiceChanged{T}.listeners"/> event.
		/// </exception>
		private static void SetInstanceSync(ServiceInfo serviceInfo, object service)
		{
			#if DEV_MODE && UNITY_ASSERTIONS
			if(serviceInfo.IsTransient)
			{
				Debug.LogError(TypeUtility.ToString(serviceInfo.ConcreteOrDefiningType));
				return;
			}
			#endif

			var concreteType = service.GetType();
			services[concreteType] = service;

			foreach(var definingType in serviceInfo.definingTypes)
			{
				if(!definingType.ContainsGenericParameters)
				{
					services[definingType] = service;
				}
			}

			foreach(var definingType in serviceInfo.definingTypes)
			{
				if(definingType.ContainsGenericParameters)
				{
					continue;
				}

				if(concreteType.IsValueType)
				{
					if(!container)
					{
						CreateServicesContainer();
					}

					object serviceProvider;
					#if UNITY_ENTITIES && !INIT_ARGS_DISABLE_ECS_SUPPORT
					if(serviceInfo.IsSystem && service is ISystem)
					{
						// It should be okay to pass null initialized and localServices here, because the World should already exist if the ISystem already exists,
						// and initialized and localServices should only be needed if the world doesn't exist yet.
						var getWorld = GetOrCreateWorldForService(serviceInfo, initialized: null, localServices: null, concreteType, client: null); 

						#if DEV_MODE && (DEBUG || INIT_ARGS_SAFE_MODE)
						if(!getWorld.IsCompleted)
						{
							Debug.LogError($"Trying to register ISystem {TypeUtility.ToString(concreteType)} but somehow GetOrCreateWorld returned a Task that was not yet completed. This should be impossible.");
							return;
						}
						#endif

						var world = getWorld.Result;
						serviceProvider = CreateISystemProvider(world, service, concreteType, definingType);
					}
					else
					#endif
					{
						serviceProvider = CreateConstantValueProvider(definingType, service);
					}

					#if DEV_MODE && DEBUG_CREATE_SERVICES
					Debug.Log($"Service {TypeUtility.ToString(concreteType)} registered via {TypeUtility.ToString(serviceProvider.GetType())}.", container);
					#endif

					#if DEV_MODE
					Debug.Assert(typeof(IValueProvider<>).MakeGenericType(definingType).IsInstanceOfType(serviceProvider));
					#endif

					ServiceUtility.AddFor(serviceProvider, ServiceProviderType.IValueProviderT, definingType, Clients.Everywhere, container.transform);
				}
				else
				{
					ServiceUtility.Set(definingType, service);
				}
			}
		}

		#if UNITY_EDITOR
		/// <summary>
		/// Warnings about missing Services should be suppressed when entering Play Mode from a scene
		/// which is not the first enabled one in build settings.
		/// </summary>
		private static bool IsFirstSceneInBuildSettingsLoaded()
		{
			string firstSceneInBuildsPath = Array.Find(EditorBuildSettings.scenes, s => s.enabled)?.path ?? "";
			Scene firstSceneInBuilds = SceneManager.GetSceneByPath(firstSceneInBuildsPath);
			return firstSceneInBuilds.IsValid() && firstSceneInBuilds.isLoaded;
		}
		#endif

		private static void InjectCrossServiceDependencies(List<ServiceInfo> globalServices, HashSet<Type> initialized, [DisallowNull] LocalServices localServices)
		{
			foreach(var serviceInfo in globalServices)
			{
				var concreteOrDefiningType = serviceInfo.ConcreteOrDefiningType;
				if(!uninitializedServices.ContainsKey(concreteOrDefiningType)
					&& services.TryGetValue(concreteOrDefiningType, out var client))
				{
					#if !DEBUG
					_ =
					#endif
					InjectCrossServiceDependencies(client, serviceInfo, initialized, localServices)
					#if DEBUG
					.OnFailure(HandleLogException)
					#endif
					;
				}
			}

			foreach(var serviceOrProvider in localServices.All())
			{
				#if UNITY_EDITOR || INIT_ARGS_SAFE_MODE
				if(!serviceOrProvider)
				{
					continue;
				}
				#endif

				#if !DEBUG
				_ =
				#endif
				InjectCrossServiceDependencies(serviceOrProvider, serviceInfo: null, initialized, localServices)
				#if DEBUG
				.OnFailure(HandleLogException)
				#endif
				;
			}
		}

		private static async Task<object> InjectCrossServiceDependencies([DisallowNull] object service, [MaybeNull] ServiceInfo serviceInfo, HashSet<Type> initialized, [DisallowNull] LocalServices localServices)
		{
			if(service is Task clientTask)
			{
				service = await clientTask.GetResult();

				#if UNITY_EDITOR
				if(!Application.isPlaying)
				{
					throw new TaskCanceledException("Aborted injection of cross-service dependencies because no longer in Play Mode.");
				}
				#endif
			}
			
			#if DEV_MODE || DEBUG || INIT_ARGS_SAFE_MODE
			if(service is null)
			{
				return null;
			}
			#endif

			var concreteType = service.GetType();
			bool isInitialized = !initialized.Add(concreteType);

			if(isInitialized)
			{
				return service;
			}

			if(serviceInfo is null)
			{
				ServiceAttributeUtility.concreteTypes.TryGetValue(concreteType, out serviceInfo);
			}

			if(CanSelfInitializeWithoutInitializer(service) || HasInitializer(service))
			{
				#if DEV_MODE && DEBUG_INIT_SERVICES
				Debug.Log($"Will not inject dependencies to service {TypeUtility.ToString(concreteType)} because it should be able to acquire them independently.");
				#endif

				return service;
			}

			var client = service as Component;
			var interfaceTypes = concreteType.GetInterfaces();
			for(int i = interfaceTypes.Length - 1; i >= 0; i--)
			{
				var interfaceType = interfaceTypes[i];
				if(!interfaceType.IsGenericType || !argumentCountsByIInitializableTypeDefinition.ContainsKey(interfaceType.GetGenericTypeDefinition()))
				{
					continue;
				}

				// If service object is a Wrapper, it might implement IInitializable<TWrapped> -> in this case, don't try to inject the wrapped object to the wrapper.
				var parameterTypes = interfaceType.GetGenericArguments();
				if(parameterTypes.SingleOrDefaultNoException() is { } singleParameterType
					&& serviceInfo is not null
					&& (singleParameterType == serviceInfo.concreteType || Array.Exists(serviceInfo.definingTypes, x => x == singleParameterType)))
				{
					continue;
				}

				(object[] arguments, int failedToGetArgumentAtIndex) = await GetOrInitializeServices(parameterTypes, initialized, localServices, client: client);
				if(failedToGetArgumentAtIndex != -1)
				{
					#if DEBUG
					if(ShouldSelfGuardAgainstNull(service) && (service is not Wrapper wrapper || wrapper.WrappedObject is null))
					{
						throw MissingInitArgumentsException.ForService(concreteType, parameterTypes[failedToGetArgumentAtIndex], localServices);
					}
					#endif

					continue;
				}

				var initMethod = interfaceType.GetMethod(nameof(IInitializable<object>.Init), BindingFlags.Public | BindingFlags.Instance | BindingFlags.DeclaredOnly);
				initMethod.Invoke(service, arguments);

				#if DEV_MODE && DEBUG_INIT_SERVICES
				Debug.Log($"Service {TypeUtility.ToString(concreteType)} received {parameterTypes.Length} dependencies successfully.");
				#endif

				return service;
			}

			return service;
		}
		
		private static void LogMissingDependencyWarning([DisallowNull] Type clientType, [DisallowNull] Type dependencyType, [AllowNull] Object context, [DisallowNull] LocalServices localServices)
		{
			if(!localServices.TryGetInfo(dependencyType, out var serviceInfo))
			{
				Debug.LogError($"Service {TypeUtility.ToString(clientType)} requires argument {TypeUtility.ToString(dependencyType)} but instance not found among {services.Count + uninitializedServices.Count} global and {localServices.GetCountSlow()} local services.", context);
				return;
			}

			if(serviceInfo.serviceOrProvider)
			{
				Debug.LogError($"Service {TypeUtility.ToString(clientType)} requires argument {TypeUtility.ToString(dependencyType)} but the service is only accessible to clients {serviceInfo.toClients}.", context);
				return;
			}

			Debug.LogError($"Service {TypeUtility.ToString(clientType)} requires argument {TypeUtility.ToString(dependencyType)} but reference to the service seems to be broken in the scene component.", context);
		}

		/// <summary>
		/// Should other services be injected to this service by the service injector during application startup or not?
		/// </summary>
		private static bool ShouldInitialize(object client)
		{
			if(CanSelfInitializeWithoutInitializer(client)
			#if UNITY_EDITOR
			&& client is not IInitializableEditorOnly { InitState: InitState.Uninitialized }
			#endif
			)
			{
				return false;
			}

			if(HasInitializer(client))
			{
				return false;
			}

			return true;
		}

		/// <summary>
		/// Should other services be injected to this service by the service injector during application startup or not?
		/// </summary>
		private static bool ShouldInitialize(Type clientType) => !CanSelfInitializeWithoutInitializer(clientType);

		#if UNITY_EDITOR
		// In builds ScriptableObjects' Awake method gets called at runtime when a scene or prefab with a component
		// referencing said ScriptableObject gets loaded.
		// Because of this we can rely on ServiceInjector to execute via the RuntimeInitializeOnLoadMethod attribute
		// before the Awake methods of ScriptableObjects are executed.
		// In the editor however, ScriptableObject can already get loaded in edit mode, in which case Awake gets
		// executed before service injection has taken place.
		// For this reason we need to manually initialize these ScriptableObjects.
		private static void InitializeAlreadyLoadedScriptableObjectsInTheEditor(Dictionary<Type, List<ScriptableObject>> uninitializedScriptableObjects)
		{
			foreach(var scriptableObjects in uninitializedScriptableObjects.Values)
			{
				foreach(var scriptableObject in scriptableObjects)
				{
					if(scriptableObject is IInitializableEditorOnly initializableEditorOnly)
					{
						if(initializableEditorOnly.Initializer is { } initializer)
						{
							initializer.InitTarget();
						}
						else
						{
							initializableEditorOnly.Init(Context.MainThread);
						}
					}
					else if(scriptableObject is IInitializable initializable)
					{
						initializable.Init(Context.MainThread);
					}
				}
			}
		}
		#endif

		private static void SubscribeToUpdateEvents(object service)
		{
			if(service is IUpdate update)
			{
				Updater.Subscribe(update);
				disposables.Add(new UpdateUnsubscriber(update));
			}

			if(service is ILateUpdate lateUpdate)
			{
				Updater.Subscribe(lateUpdate);
				disposables.Add(new LateUpdateUnsubscriber(lateUpdate));
			}

			if(service is IFixedUpdate fixedUpdate)
			{
				Updater.Subscribe(fixedUpdate);
				disposables.Add(new FixedUpdateUnsubscriber(fixedUpdate));
			}
		}

		private static void ExecuteAwake(object service)
		{
			if(service is IAwake awake)
			{
				#if DEV_MODE || DEBUG || INIT_ARGS_SAFE_MODE
				try
				{
				#endif

					awake.Awake();

				#if DEV_MODE || DEBUG || INIT_ARGS_SAFE_MODE
				}
				catch(Exception ex)
				{
					Debug.LogError(ex);
				}
				#endif
			}
		}

		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		private static void ExecuteOnEnable(object service)
		{
			if(service is IOnEnable onEnable)
			{
				#if DEV_MODE || DEBUG || INIT_ARGS_SAFE_MODE
				try
				{
				#endif

					onEnable.OnEnable();

				#if DEV_MODE || DEBUG || INIT_ARGS_SAFE_MODE
				}
				catch(Exception ex)
				{
					Debug.LogError(ex);
				}
				#endif
			}
		}

		#if DEV_MODE || DEBUG || INIT_ARGS_SAFE_MODE
		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		#if UNITY_6000_0_OR_NEWER
		[HideInCallstack]
		#endif
		private static void OnGetOrInitializeServiceFailed(Exception exception, ServiceInfo serviceInfo)
		{
			if(exception is InitArgsException initArgsException)
			{
				initArgsException.LogToConsole();
				return;
			}

			if(exception is AggregateException { InnerException: InitArgsException innerInitArgsException })
			{
				innerInitArgsException.LogToConsole();
				return;
			}
			
			if(exception is null)
			{
				Debug.LogError($"Initializing service {serviceInfo.ConcreteOrDefiningType} failed for an unknown reason.", GetContext(serviceInfo));
				return;
			}

			initArgsException = ServiceInitFailedException.Create(serviceInfo, ServiceInitFailReason.ExceptionWasThrown, exception: exception);
			initArgsException.LogToConsole();
		}
		#endif

		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		private static void ExecuteStartAtEndOfFrame(object service)
		{
			if(service is IStart start)
			{
				Updater.InvokeAtEndOfFrame(start.Start);
			}
		}

		[return: MaybeNull]
		internal static Type GetClassWithServiceAttribute(Type definingType)
			=> ServiceAttributeUtility.TryGetInfoForDefiningType(definingType, out var serviceInfo)
				? serviceInfo.classWithAttribute
				: null;

		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		internal static bool TryGetServiceInfo(Type definingType, [MaybeNullWhen(false), NotNullWhen(true)] out ServiceInfo serviceInfo) => ServiceAttributeUtility.TryGetInfoForDefiningType(definingType, out serviceInfo);

		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		private static List<ServiceInfo> GetServiceDefinitions() => ServiceAttributeUtility.concreteTypes.Values.Concat(ServiceAttributeUtility.definingTypes.Values.Where(d => d.concreteType is null)).ToList();

		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		internal static bool CanProvideService<TService>() => services.ContainsKey(typeof(TService)) || TryGetUninitializedServiceInfo(typeof(TService), out _);

		internal static bool TryGetUninitializedServiceInfo(Type requestedType, out ServiceInfo info)
		{
			if(uninitializedServices.TryGetValue(requestedType, out info))
			{
				return true;
			}

			return requestedType.IsGenericType && uninitializedServices.TryGetValue(requestedType.GetGenericTypeDefinition(), out info);
		}

		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		private static (LoadMethod, ReferenceType) FilterForServiceProvider(Type serviceProviderType, LoadMethod loadMethod, ReferenceType referenceType)
			=> typeof(Object).IsAssignableFrom(serviceProviderType) ? (loadMethod, referenceType) : (LoadMethod.Default, ReferenceType.None);

		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		internal static void HandleLogException(Task task)
		{
			if(task.Exception is { } exception and not { InnerException : OperationCanceledException })
			{
				LogException(exception);
			}
		}

		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		private static object CreateConstantValueProvider(Type definingType, object value) => typeof(ConstantValueProvider<>).MakeGenericType(definingType).GetConstructor(new[] { definingType }).Invoke(new[] { value });
		[UnityEngine.Scripting.Preserve, JetBrains.Annotations.UsedImplicitly]
		private sealed class ConstantValueProvider<T> : IValueProvider<T>
		{
			[UnityEngine.Scripting.Preserve] public T Value { get; }
			[UnityEngine.Scripting.Preserve] public ConstantValueProvider(T value) => Value = value;
		}

		#if UNITY_ENTITIES && !INIT_ARGS_DISABLE_ECS_SUPPORT
		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		private static object CreateISystemProvider(World world, object system, Type closedConcreteType, Type definingType)
			=> typeof(ISystemProvider<,>).MakeGenericType(closedConcreteType, definingType)
										 .GetConstructor(new[] { typeof(World), definingType })
										 .Invoke(new[] { world, system });
		[UnityEngine.Scripting.Preserve, JetBrains.Annotations.UsedImplicitly]
		private sealed class ISystemProvider<TConcrete, TDefining> : IValueProvider<TDefining>, INullGuard where TConcrete : unmanaged, ISystem, TDefining
		{
			private readonly World world;
			private readonly TConcrete system;

			[UnityEngine.Scripting.Preserve]
			public TDefining Value
			{
				get
				{
					var systemHandle = world.GetOrCreateSystem<TConcrete>();
					return world.Unmanaged.GetUnsafeSystemRef<TConcrete>(systemHandle);
				}
			}

			bool IValueProvider<TDefining>.HasValueFor(Component client) => world.GetExistingSystem<TConcrete>() != SystemHandle.Null;

			NullGuardResult INullGuard.EvaluateNullGuard([AllowNull] Component client) => (world.GetExistingSystem<TConcrete>() == SystemHandle.Null) switch
			{
				false => NullGuardResult.Passed,
				_ => Application.isPlaying ? NullGuardResult.ValueProviderValueMissing : NullGuardResult.ValueProviderValueNullInEditMode
			};

			/// <summary>
			/// Gets the value of type <typeparamref name="TValue"/> for the <paramref name="client"/>.
			/// </summary>
			/// <param name="client">
			/// The component requesting the value, if request is coming from a component; otherwise, <see langword="null"/>.
			/// </param>
			/// <param name="value">
			/// When this method returns, contains the value of type <typeparamref name="TValue"/>, if available; otherwise, the default value of <typeparamref name="TValue"/>.
			/// This parameter is passed uninitialized.
			/// </param>
			/// <returns>
			/// <see langword="true"/> if a value was provided; otherwise, <see langword="false"/>.
			/// </returns>
			bool IValueProvider<TDefining>.TryGetFor([AllowNull] Component client, [NotNullWhen(true), MaybeNullWhen(false)] out TDefining value)
			{
				var systemHandle = world.GetExistingSystem<TConcrete>();
				if(systemHandle == SystemHandle.Null)
				{
					value = default;
					return false;
				}

				value = world.Unmanaged.GetUnsafeSystemRef<TConcrete>(systemHandle);
				return true;
			}

			public ISystemProvider(World world, TConcrete system)
			{
				this.world = world;
				this.system = system;
				var systemHandle = world.GetOrCreateSystem<TConcrete>();
				ref var systemRef = ref world.Unmanaged.GetUnsafeSystemRef<TConcrete>(systemHandle);
				systemRef = this.system;
			}
		}
		#endif

		private sealed class UpdateUnsubscriber : IDisposable
		{
			private readonly IUpdate subscriber;
			public UpdateUnsubscriber(IUpdate subscriber) => this.subscriber = subscriber;
			public void Dispose() => Updater.Unsubscribe(subscriber);
		}

		private sealed class FixedUpdateUnsubscriber : IDisposable
		{
			private readonly IFixedUpdate subscriber;
			public FixedUpdateUnsubscriber(IFixedUpdate subscriber) => this.subscriber = subscriber;
			public void Dispose() => Updater.Unsubscribe(subscriber);
		}

		private sealed class LateUpdateUnsubscriber : IDisposable
		{
			private readonly ILateUpdate subscriber;
			public LateUpdateUnsubscriber(ILateUpdate subscriber) => this.subscriber = subscriber;
			public void Dispose() => Updater.Unsubscribe(subscriber);
		}

		private sealed class AsyncDisposableDisposer : IDisposable
		{
			private readonly IAsyncDisposable asyncDisposable;
			public AsyncDisposableDisposer(IAsyncDisposable asyncDisposable) => this.asyncDisposable = asyncDisposable;
			public void Dispose()
			{
				asyncDisposable.DisposeAsync().AsTask().ContinueWith(t =>
				{
					Debug.LogWarning($"Exception occurred while executing DisposeAsync for service {TypeUtility.ToString(asyncDisposable?.GetType())}: " + t.Exception);
				}, TaskContinuationOptions.OnlyOnFaulted);
			}
		}

		private sealed class OnDisableCaller : IDisposable
		{
			private readonly IOnDisable disableable;
			public OnDisableCaller(IOnDisable disableable) => this.disableable = disableable;
			public void Dispose()
			{
				#if DEV_MODE || DEBUG || INIT_ARGS_SAFE_MODE
				try
				{
				#endif

				disableable.OnDisable();

				#if DEV_MODE || DEBUG || INIT_ARGS_SAFE_MODE
				}
				catch(Exception exception)
				{
					Debug.LogWarning($"Exception occurred while executing OnDisable for service {TypeUtility.ToString(disableable?.GetType())}: " + exception);
				}
				#endif
			}
		}

		private sealed class OnDestroyCaller : IDisposable
		{
			private readonly IOnDestroy destroyable;
			public OnDestroyCaller(IOnDestroy destroyable) => this.destroyable = destroyable;
			public void Dispose()
			{
				#if DEV_MODE || DEBUG || INIT_ARGS_SAFE_MODE
				try
				{
				#endif

				destroyable.OnDestroy();

				#if DEV_MODE || DEBUG || INIT_ARGS_SAFE_MODE
				}
				catch(Exception exception)
				{
					Debug.LogWarning($"Exception occurred while executing OnDestroy for service {TypeUtility.ToString(destroyable?.GetType())}: " + exception);
				}
				#endif
			}
		}

		#if UNITY_EDITOR // Only needed to be destroyed manually in the editor
		private sealed class ScriptableObjectDestroyer : IDisposable
		{
			private readonly ScriptableObject scriptableObject;
			public ScriptableObjectDestroyer(ScriptableObject scriptableObject) => this.scriptableObject = scriptableObject;

			public void Dispose()
			{
				if(!scriptableObject)
				{
					return;
				}
				
				#if DEV_MODE || DEBUG || INIT_ARGS_SAFE_MODE
				try
				{
				#endif

				Object.Destroy(scriptableObject);

				#if DEV_MODE || DEBUG || INIT_ARGS_SAFE_MODE
				}
				catch(Exception exception)
				{
					Debug.LogWarning($"Exception occurred while destroying {TypeUtility.ToString(scriptableObject.GetType())}: " + exception, scriptableObject);
				}
				#endif
			}
		}
		#endif

		private sealed class ValueReleaser : IDisposable
		{
			private readonly IValueReleaser releaser;
			private readonly Component client;
			private readonly object value;
			private bool released;
			
			public ValueReleaser(IValueReleaser releaser, Component client, object value)
			{
				this.releaser = releaser;
				this.client = client;
				this.value = value;

				#if UNITY_2022_2_OR_NEWER
				if(client is MonoBehaviour monoBehaviour)
				{
					if(client is not IInitializer initializer)
					{
						monoBehaviour.destroyCancellationToken.Register(OnClientDestroyed);
					}
					else if(initializer.Target is MonoBehaviour targetMonoBehaviour)
					{
						targetMonoBehaviour.destroyCancellationToken.Register(OnClientDestroyed);
					}
				}
				#endif
			}

			private void OnClientDestroyed()
			{
				if(exitingApplicationOrPlayMode || released)
				{
					return;
				}

				disposables.Remove(this);
				Dispose();
			}

			public void Dispose()
			{
				if(released)
				{
					return;
				}

				released = true;

				#if DEV_MODE || DEBUG || INIT_ARGS_SAFE_MODE
				try
				{
				#endif

				releaser.Release(client, value);

				#if DEV_MODE || DEBUG || INIT_ARGS_SAFE_MODE
				}
				catch(Exception exception)
				{
					Debug.LogWarning($"Exception occurred while executing {TypeUtility.ToString(releaser.GetType())}.Release for service {TypeUtility.ToString(value?.GetType())}: " + exception);
				}
				#endif
			}
		}

		private sealed class ValueByTypeReleaser : IDisposable
		{
			private readonly IValueByTypeReleaser releaser;
			private readonly Component clientIfTransient;
			private readonly object value;
			private bool released;

			public ValueByTypeReleaser(IValueByTypeReleaser releaser, Component clientIfTransient, object value)
			{
				this.releaser = releaser;
				this.clientIfTransient = clientIfTransient;
				this.value = value;

				#if UNITY_2022_2_OR_NEWER
				if(clientIfTransient is MonoBehaviour monoBehaviour)
				{
					if(clientIfTransient is not IInitializer initializer)
					{
						monoBehaviour.destroyCancellationToken.Register(OnClientDestroyed);
					}
					else if(initializer.Target is MonoBehaviour targetMonoBehaviour)
					{
						targetMonoBehaviour.destroyCancellationToken.Register(OnClientDestroyed);
					}
				}
				#endif
			}

			private void OnClientDestroyed()
			{
				if(exitingApplicationOrPlayMode || released)
				{
					return;
				}

				disposables.Remove(this);
				Dispose();
			}

			public void Dispose()
			{
				if(released)
				{
					return;
				}

				released = true;

				#if DEV_MODE || DEBUG || INIT_ARGS_SAFE_MODE
				try
				{
				#endif

				releaser.Release(clientIfTransient, value);

				#if DEV_MODE || DEBUG || INIT_ARGS_SAFE_MODE
				}
				catch(Exception exception)
				{
					Debug.LogWarning($"Exception occurred while executing {TypeUtility.ToString(releaser.GetType())}.Release for service {TypeUtility.ToString(value?.GetType())}: " + exception);
				}
				#endif
			}
		}

		#if UNITY_ADDRESSABLES_1_17_4_OR_NEWER
		private sealed class AddressableAssetReleaser : IDisposable
		{
			private readonly AsyncOperationHandle<Object> asyncOperation;
			private bool released;
			
			public AddressableAssetReleaser(AsyncOperationHandle<Object> asyncOperation, Component clientIfTransient)
			{
				this.asyncOperation = asyncOperation;

				if(clientIfTransient is MonoBehaviour monoBehaviour)
				{
					if(clientIfTransient is not IInitializer initializer)
					{
						monoBehaviour.destroyCancellationToken.Register(OnClientDestroyed);
					}
					else if(initializer.Target is MonoBehaviour targetMonoBehaviour)
					{
						targetMonoBehaviour.destroyCancellationToken.Register(OnClientDestroyed);
					}
				}
			}

			private void OnClientDestroyed()
			{
				if(exitingApplicationOrPlayMode || released)
				{
					return;
				}

				disposables.Remove(this);
				Dispose();
			}

			public void Dispose()
			{
				if(released)
				{
					return;
				}

				released = true;

				#if DEV_MODE || DEBUG || INIT_ARGS_SAFE_MODE
				try
				{
				#endif

				asyncOperation.Release();

				#if DEV_MODE || DEBUG || INIT_ARGS_SAFE_MODE
				}
				catch(Exception exception)
				{
					Debug.LogWarning($"Exception occurred while trying to release addressable load handle '{asyncOperation.DebugName}': " + exception);
				}
				#endif
			}
		}
		#endif
	}
}
#endif