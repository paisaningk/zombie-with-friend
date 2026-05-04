//#define DEBUG_SERVICE_PROVIDERS
//#define DEBUG_SERVICE_CHANGED
//#define DEBUG_LAZY_INIT

using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Sisus.Init.Internal;
using UnityEngine;
using UnityEngine.Scripting;
using static Sisus.NullExtensions;
using Component = UnityEngine.Component;
using Debug = UnityEngine.Debug;
using Object = UnityEngine.Object;
#if UNITY_ADDRESSABLES_1_17_4_OR_NEWER
using UnityEngine.ResourceManagement.ResourceLocations;
#endif

namespace Sisus.Init
{
	/// <summary>
	/// Utility class that clients can use to retrieve services that are accessible to them.
	/// <para>
	/// <see cref="Get{TService}"/> and <see cref="TryGet{TService}"/> can be used to acquire global services,
	/// while <see cref="GetFor{TService}(UnityEngine.Component)"/> and <see cref="TryGetFor{TService}(UnityEngine.Component, out TService)"/>
	/// can be used to acquire local services specific to a particular client.
	/// </para>
	/// <para>
	/// Services can be registered automatically using the <see cref="ServiceAttribute"/>, or manually using the
	/// <see cref="Set{TService}"/> and <see cref="AddFor{TService}(Clients, TService, UnityEngine.Component)"/> methods.
	/// </para>
	/// <para>
	/// Services can also be registered using the Inspector by selecting the "Make Service Of Type..." context menu item,
	/// or by dragging and dropping components onto a <see cref="Services"/> component. 
	/// </para>
	/// </summary>
	public static class Service
	{
		internal static Type nowSettingInstance;

		#if UNITY_EDITOR
		private static bool batchEditingServices;
		internal static bool BatchEditingServices
		{
			set
			{
				if(batchEditingServices == value)
				{
					return;
				}

				batchEditingServices = value;
				if(!value)
				{
					AnyChangedEditorOnly?.Invoke();
				}
			}
		}

		internal static event Action AnyChangedEditorOnly;
		private static readonly List<ActiveServiceInfo> activeInstancesEditorOnlyEditMode = new(64);
		private static readonly List<ActiveServiceInfo> activeInstancesEditorOnlyPlayMode = new(64);
		private static readonly ServiceInfoOrderer serviceInfoOrdererEditorOnly = new();
		internal static List<ActiveServiceInfo> ActiveInstancesEditorOnly => Application.isPlaying ? activeInstancesEditorOnlyPlayMode : activeInstancesEditorOnlyEditMode;
		private static readonly List<ActiveServiceInfo> inactiveInstancesTemp = new(0);

		internal static void UsingActiveInstancesEditorOnly(Action<List<ActiveServiceInfo>, IEnumerable<ActiveServiceInfo>> action)
		{
			foreach(var services in Services.GetAllEditorOnly(includeInactive: true))
			{
				var providerIsInactive = !services.isActiveAndEnabled;
				foreach(var definition in services.providesServices)
				{
					if(definition.DefiningType is not { } definingType)
					{
						var service = definition.service;
						if(service)
						{
							inactiveInstancesTemp.Add(new(null, services.toClients, service, services, RegistererType.ServicesComponent));
						}
					}
					else
					{
						var service = definition.service;
						if(providerIsInactive || !service || service is Behaviour { isActiveAndEnabled: false })
						{
							inactiveInstancesTemp.Add(new(definingType, services.toClients, service, services, RegistererType.ServicesComponent));
						}
					}
				}
			}

			foreach(var serviceTag in ServiceTag.GetAllEditorOnly(includeInactive: true))
			{
				if(serviceTag.DefiningType is not { } definingType)
				{
					var service = serviceTag.Service;
					if(service)
					{
						inactiveInstancesTemp.Add(new(null, serviceTag.ToClients, service, serviceTag, RegistererType.ServiceTag));
					}
				}
				else
				{
					var service = serviceTag.Service;
					if(!serviceTag.isActiveAndEnabled || !service || service is Behaviour { isActiveAndEnabled: false })
					{
						inactiveInstancesTemp.Add(new(definingType, serviceTag.ToClients, service, serviceTag, RegistererType.ServiceTag));
					}
				}
			}

			var activeInstances = ActiveInstancesEditorOnly;
			lock(activeInstances)
			{
				action(activeInstances, inactiveInstancesTemp);
			}

			inactiveInstancesTemp.Clear();
		}
		#endif

		/// <summary>
		/// Determines whether service of type <typeparamref name="TService"/>
		/// is available for the <paramref name="client"/>.
		/// <para>
		/// The service can be a local service registered using a <see cref="ServiceTag"/> or a
		/// <see cref="Services"/> component in an active scene, or a global service registered using
		/// a <see cref="ServiceAttribute"/> or <see cref="Set{TService}">manually</see>.
		/// </para>
		/// </summary>
		/// <typeparam name="TService"> The defining type of the service. </typeparam>
		/// <param name="client"> The client that needs the service. </param>
		/// <returns>
		/// <see langword="true"/> if service exists for the client; otherwise, <see langword="false"/>.
		/// </returns>
		/// <remarks>
		/// This method can only be called from the main thread.
		/// </remarks>
		public static bool ExistsFor<TService>([DisallowNull] object client)
			=> TryGetComponent(client, out var component)
				? ExistsFor<TService>(component)
				: Exists<TService>();

		/// <summary>
		/// Gets a value indicating whether service of type <typeparamref name="TService"/> is available for the <paramref name="client"/>.
		/// <para>
		/// The service can be a local service registered using a <see cref="ServiceTag"/> or a
		/// <see cref="Services"/> component in an active scene, or a global service registered using
		/// a <see cref="ServiceAttribute"/> or <see cref="Set{TService}">manually</see>.
		/// </para>
		/// </summary>
		/// <typeparam name="TService"> The defining type of the service. </typeparam>
		/// <param name="client"> The client that needs the service. </param>
		/// <returns>
		/// <see langword="true"/> if service is available for the client; otherwise, <see langword="false"/>.
		/// </returns>
		/// <remarks>
		/// This method can only be called from the main thread.
		/// </remarks>
		public static bool ExistsFor<TService>([DisallowNull] Component client)
		{
			if(ServiceInjector.CanProvideService<TService>())
			{
				return true;
			}

			if(LocalService<TService>.ExistsFor(client))
			{
				return true;
			}

			#if UNITY_EDITOR
			var serviceType = typeof(TService);
			if(ServiceAttributeUtility.ContainsDefiningType(serviceType))
			{
				return true;
			}

			// In Edit Mode it's possible that the dependency is registered via a value provider in a Service Tag
			// or Services component, but it will only become available at runtime.
			if(!Application.isPlaying)
			{
				var clientIsInactive = !client.gameObject.activeInHierarchy;

				var all = ServiceTag.GetAllEditorOnly(clientIsInactive);
				foreach(var serviceTag in all)
				{
					if(serviceTag.DefiningType == serviceType && serviceTag.Service && IsAccessibleTo(client, serviceTag, serviceTag.ToClients))
					{
						return true;
					}
				}

				foreach(var servicesComponent in Services.GetAllEditorOnly(clientIsInactive))
				{
					if(!IsAccessibleTo(client, servicesComponent, servicesComponent.toClients))
					{
						continue;
					}

					foreach(var definition in servicesComponent.providesServices)
					{
						if(definition.definingType.Value == serviceType && definition.service)
						{
							return true;
						}
					}
				}
			}
			#endif

			return false;
		}

		/// <summary>
		/// Determines whether service of type <typeparamref name="TService"/> is available
		/// for the <paramref name="client"/>.
		/// <para>
		/// The service can be a local service registered using a <see cref="ServiceTag"/> or a
		/// <see cref="Services"/> component in an active scene, or a global service registered using
		/// a <see cref="ServiceAttribute"/> or <see cref="Set{TService}">manually</see>.
		/// </para>
		/// </summary>
		/// <typeparam name="TService"> The defining type of the service. </typeparam>
		/// <param name="client"> The client that needs the service. </param>
		/// <returns>
		/// <see langword="true"/> if service exists for the client; otherwise, <see langword="false"/>.
		/// </returns>
		/// <remarks>
		/// This method can only be called from the main thread.
		/// </remarks>
		public static bool ExistsFor<TService>([DisallowNull] GameObject client) => ExistsFor<TService>(client.transform);

		/// <summary>
		/// Determines whether a global service of type <typeparamref name="TService"/> is available.
		/// <para>
		/// The service can be a service registered using the <see cref="ServiceAttribute"/>, manually using <see cref="Set{TService}"/>
		/// or one registered using a <see cref="ServiceTag"/> or a <see cref="Services"/> component with availability set to
		/// <see cref="Clients.Everywhere"/>.
		/// </para>
		/// </summary>
		/// <typeparam name="TService"> The defining type of the service. </typeparam>
		/// <returns>
		/// <see langword="true"/> if service exists for the client; otherwise, <see langword="false"/>.
		/// </returns>
		/// <remarks>
		/// This method can only be called from the main thread.
		/// </remarks>
		public static bool Exists<TService>()
		{
			if(ServiceInjector.CanProvideService<TService>())
			{
				return true;
			}

			if(LocalService<TService>.ExistsForAllClients())
			{
				return true;
			}

			#if UNITY_EDITOR
			var serviceType = typeof(TService);
			if(ServiceAttributeUtility.ContainsDefiningType(serviceType))
			{
				return true;
			}

			// In Edit Mode it's possible that the dependency is registered via a value provider in a Service Tag
			// or Services component, but it will only become available at runtime.
			if(!Application.isPlaying)
			{
				foreach(var serviceTag in ServiceTag.GetAllEditorOnly())
				{
					if(serviceTag.DefiningType == serviceType && serviceTag.Service && serviceTag.ToClients is Clients.Everywhere)
					{
						return true;
					}
				}

				foreach(var servicesComponent in Services.GetAllEditorOnly())
				{
					if(servicesComponent.toClients is not Clients.Everywhere)
					{
						continue;
					}

					foreach(var definition in servicesComponent.providesServices)
					{
						if(definition.definingType.Value == serviceType && definition.service)
						{
							return true;
						}
					}
				}
			}
			#endif

			return false;
		}

		/// <summary>
		/// Determines whether a global or local service of type <typeparamref name="TService"/>
		/// is currently available anywhere for any clients.
		/// </summary>
		internal static bool AnyExist<TService>()
		{
			if(ServiceInjector.CanProvideService<TService>())
			{
				return true;
			}

			if(LocalService<TService>.Instances.Any())
			{
				return true;
			}

			#if UNITY_EDITOR
			var serviceType = typeof(TService);
			if(ServiceAttributeUtility.ContainsDefiningType(serviceType))
			{
				return true;
			}

			// In Edit Mode it's possible that the dependency is registered via a value provider in a Service Tag
			// or Services component, but it will only become available at runtime.
			if(!Application.isPlaying)
			{
				foreach(var serviceTag in ServiceTag.GetAllEditorOnly())
				{
					if(serviceTag.DefiningType == serviceType && serviceTag.Service)
					{
						return true;
					}
				}

				foreach(var servicesComponent in Services.GetAllEditorOnly())
				{
					foreach(var definition in servicesComponent.providesServices)
					{
						if(definition.definingType.Value == serviceType && definition.service)
						{
							return true;
						}
					}
				}
			}
			#endif

			return false;
		}

		/// <summary>
		/// Determines whether the given <paramref name="object"/> is a service accessible by the <paramref name="client"/>.
		/// <para>
		/// Services are components that have the <see cref="ServiceTag"/> attached to them,
		/// have been defined as a service in a <see cref="Services"/> component,
		/// have the <see cref="ServiceAttribute"/> on their class,
		/// or have been <see cref="Set{TService}">manually registered</see> as a service in code.
		/// </para>
		/// </summary>
		/// <typeparam name="TService"> The defining type of the service. </typeparam>
		/// <param name="client"> The client that has to be able to access the service. </param>
		/// <param name="object"> The object to test. </param>
		/// <returns>
		/// <see langword="true"/> if the <paramref name="object"/> is a service accessible by the <paramref name="client"/>;
		/// otherwise, <see langword="false"/>.
		/// </returns>
		/// <remarks>
		/// This method can only be called from the main thread.
		/// </remarks>
		internal static bool ForEquals<TService>([DisallowNull] GameObject client, TService @object)
			=> TryGetFor(client, out TService service) && ReferenceEquals(service, @object);

		internal static bool TryGetClients<TService>([AllowNull] TService service, out Clients clients)
		{
			if(service is null)
			{
				if(ServiceAttributeUtility.ContainsDefiningType(typeof(TService)))
				{
					clients = Clients.Everywhere;
					return true;
				}

				clients = default;
				return false;
			}

			if(!typeof(TService).IsValueType && ReferenceEquals(Service<TService>.Instance, service))
			{
				clients = Clients.Everywhere;
				return true;
			}

			foreach(var instance in LocalService<TService>.Instances)
			{
				#if UNITY_EDITOR
				if(!instance.registerer)
				{
					continue;
				}
				#endif

				if(ReferenceEquals(instance.service, service))
				{
					clients = instance.clients;
					return true;
				}
			}

			clients = default;
			return false;
		}

		/// <summary>
		/// Tries to get <paramref name="service"/> of type <typeparamref name="TService"/>
		/// for the <paramref name="client"/>.
		/// <para>
		/// The returned service can be a local service registered using a <see cref="ServiceTag"/> or a
		/// <see cref="Services"/> component in an active scene, or if one is not found, a global service
		/// registered using a <see cref="ServiceAttribute"/> or <see cref="Set{TService}">manually</see>.
		/// </para>
		/// </summary>
		/// <typeparam name="TService"> The defining type of the service. </typeparam>
		/// <param name="client"> The client that needs the service. </param>
		/// <param name="service">
		/// When this method returns, contains service of type <typeparamref name="TService"/>,
		/// if found; otherwise, <see langword="null"/>. This parameter is passed uninitialized.
		/// </param>
		/// <returns> <see langword="true"/> if service was found; otherwise, <see langword="false"/>. </returns>
		/// <remarks>
		/// This method can only be called from the main thread.
		/// </remarks>
		public static bool TryGetFor<TService>([DisallowNull] object client, [NotNullWhen(true), MaybeNullWhen(false)] out TService service)
			=> TryGetComponent(client, out Component component) ? TryGetFor(component, out service) : TryGet(out service);

		/// <summary>
		/// Tries to get <paramref name="service"/> of type <typeparamref name="TService"/>
		/// for <paramref name="client"/>.
		/// <para>
		/// The returned service can be a local service registered using a <see cref="ServiceTag"/> or a
		/// <see cref="Services"/> component in an active scene, or if one is not found, a global service
		/// registered using a <see cref="ServiceAttribute"/> or <see cref="Set{TService}">manually</see>.
		/// </para>
		/// </summary>
		/// <typeparam name="TService"> The defining type of the service. </typeparam>
		/// <param name="client"> The client <see cref="Component"/> that needs the service. </param>
		/// <param name="service">
		/// When this method returns, contains service of type <typeparamref name="TService"/>, if found; otherwise, <see langword="null"/>. This parameter is passed uninitialized.
		/// </param>
		/// <returns> <see langword="true"/> if service was found; otherwise, <see langword="false"/>. </returns>
		/// <remarks>
		/// This method can only be called from the main thread.
		/// </remarks>
		public static bool TryGetFor<TService>([DisallowNull] Component client, [NotNullWhen(true), MaybeNullWhen(false)] out TService service)
		{
			#if DEV_MODE
			Debug.Assert(client);
			#endif

			LocalService<TService>.Instance? nearestInstance = default;
			foreach(var instance in LocalService<TService>.Instances)
			{
				#if UNITY_EDITOR
				if(!instance.registerer)
				{
					continue;
				}
				#endif

				if(!IsAccessibleTo(instance, client.transform))
				{
					continue;
				}

				if(nearestInstance is not { } nearest)
				{
					#if DEV_MODE && DEBUG_SERVICE_PROVIDERS
					Debug.Log($"Service.TryGetFor<{TypeUtility.ToString(typeof(TService))}> - nearestInstance = {TypeUtility.ToString(((object)instance.service ?? instance.serviceProvider).GetType())} (registerer: '{instance.registerer.name}')");
					#endif
					nearestInstance = instance;
					continue;
				}

				var clientScene = client.gameObject.scene;
				if(instance.Scene != clientScene)
				{
					#if DEBUG || INIT_ARGS_SAFE_MODE && !INIT_ARGS_DISABLE_WARNINGS
					if(nearest.Scene == clientScene)
					{
						continue;
					}

					#if UNITY_EDITOR
					// Prioritize scene objects over uninstantiated prefabs
					var prefabStage = UnityEditor.SceneManagement.PrefabStageUtility.GetCurrentPrefabStage();
					var prefabStageScene = prefabStage ? prefabStage.scene : default;
					if(!instance.Scene.IsValid() || (prefabStage && instance.Scene == prefabStageScene))
					{
						continue;
					}

					if(!nearest.Scene.IsValid() || (prefabStage && nearest.Scene == prefabStageScene))
					{
						continue;
					}

					// Don't spam warnings when services are requested in edit mode, for example in Inspector code.
					if(!Application.isPlaying || !clientScene.IsValid() || clientScene == prefabStageScene)
					{
						continue;
					}
					#endif

					#if DEV_MODE
					Debug.Assert(!EqualityComparer<TService>.Default.Equals(instance.service, nearest.service) || !ReferenceEquals(instance.serviceProvider, nearest.serviceProvider));
					#endif

					Debug.LogWarning($"AmbiguousMatchWarning: Client on GameObject \"{client.name}\" has access to both services {TypeUtility.ToString(instance.service.GetType())} and {TypeUtility.ToString(nearest.service.GetType())} via {nameof(Services)}s and unable to determine which one should be prioritized." +
					$"\nFirst option: {nearest.service}\nprovider:{TypeUtility.ToString(nearest.serviceProvider?.GetType())}\nregisterer:{nearest.registerer}\nclients:{nearest.clients}\nscene:{(nearest.Scene.IsValid() ? nearest.Scene.name : "n/a")}." +
					$"\n\nSecond option {instance.service}\nprovider:{TypeUtility.ToString(instance.serviceProvider?.GetType())}\nregisterer:{instance.registerer}\nclients:{instance.clients}\nscene:{(instance.Scene.IsValid() ? instance.Scene.name : "n/a")}", client);
					#endif

					continue;
				}

				if(nearest.Scene != clientScene)
				{
					#if DEV_MODE && DEBUG_SERVICE_PROVIDERS
					Debug.Log($"Service.TryGetFor<{TypeUtility.ToString(typeof(TService))}> - nearestInstance = {TypeUtility.ToString(((object)instance.service ?? instance.serviceProvider).GetType())} (registerer: '{instance.registerer.name}')");
					#endif

					nearestInstance = instance;
					continue;
				}

				var instanceTransform = instance.Transform;
				var nearestTransform = nearest.Transform;

				#if DEBUG || INIT_ARGS_SAFE_MODE
				bool betterMatchFound = false;
				#endif

				for(var clientParent = client.transform; clientParent; clientParent = clientParent.parent)
				{
					if(clientParent == instanceTransform)
					{
						#if DEBUG || INIT_ARGS_SAFE_MODE
						if(clientParent == nearestTransform)
						{
							break;
						}

						betterMatchFound = true;
						#endif

						#if DEV_MODE && DEBUG_SERVICE_PROVIDERS
						Debug.Log($"Service.TryGetFor<{TypeUtility.ToString(typeof(TService))}> - nearestInstance = {TypeUtility.ToString(((object)instance.service ?? instance.serviceProvider).GetType())} (registerer: '{instance.registerer.name}'))");
						#endif

						nearestInstance = instance;
						break;
					}

					if(clientParent == nearestTransform)
					{
						#if DEBUG || INIT_ARGS_SAFE_MODE
						betterMatchFound = true;
						#endif
						break;
					}
				}

				#if DEBUG || INIT_ARGS_SAFE_MODE && !INIT_ARGS_DISABLE_WARNINGS
				if(!betterMatchFound)
				{
					#if UNITY_EDITOR
					// Don't spam warnings when services are requested in edit mode, for example in Inspector code.
					if(!Application.isPlaying || !clientScene.IsValid())
					{
						continue;
					}

					var prefabStage = UnityEditor.SceneManagement.PrefabStageUtility.GetCurrentPrefabStage();
					if(prefabStage && clientScene == prefabStage.scene)
					{
						continue;
					}
					#endif

					Debug.LogWarning($"AmbiguousMatchWarning: Client on GameObject \"{client.name}\" has access to both services \"{instance.Transform.name}\"/{TypeUtility.ToString(instance.service.GetType())} and \"{nearest.Transform.name}\"/{TypeUtility.ToString(nearest.service.GetType())} via {nameof(Services)} and unable to determine which one should be prioritized.", client);
				}
				#endif
			}

			if(nearestInstance.HasValue)
			{
				if(nearestInstance.Value.serviceProvider is { } serviceProvider)
				{
					if(serviceProvider.TryGetFor(client, out service))
					{
						#if DEV_MODE && DEBUG_SERVICE_PROVIDERS
						Debug.Log($"Service.TryGetFor => {TypeUtility.ToString(serviceProvider.GetType())}.TryGetFor: {TypeUtility.ToString(service?.GetType())} (registerer: '{(nearestInstance.Value.registerer ? nearestInstance.Value.registerer.name : "null")}')");
						#endif
						return true;
					}
					#if DEV_MODE && DEBUG_SERVICE_PROVIDERS
					Debug.Log($"Service.TryGetFor => {TypeUtility.ToString(serviceProvider.GetType())}.TryGetFor: false");
					#endif
				}
				else if((service = nearestInstance.Value.service) != Null)
				{
					return true;
				}
			}

			#if !INIT_ARGS_DISABLE_SERVICE_INJECTION
			bool isUninitializedLazyOrTransientService = ServiceInjector.TryGetUninitializedServiceInfo(typeof(TService), out var serviceInfo);
			if(isUninitializedLazyOrTransientService)
			{
				if(serviceInfo.LazyInit
   				#if UNITY_EDITOR
				&& Application.isPlaying
				#endif
				)
				{
					var task = ServiceInjector.LazyInit(serviceInfo, typeof(TService), client);
					if(task.IsCompletedSuccessfully)
					{
						if(task.Result is TService taskResult)
						{
							service = taskResult;
							return true;
						}

						if(task.Result is Task nestedTask && nestedTask.GetResult() is { IsCompletedSuccessfully: true, Result: TService nestedTaskResult })
						{
							service = nestedTaskResult;
							return true;
						}

						#if DEV_MODE && DEBUG_LAZY_INIT
						Debug.Log($"Service.TryGetFor<{TypeUtility.ToString(typeof(TService))}>: false because LazyInit result {TypeUtility.ToString(task.Result?.GetType())} is not of type {TypeUtility.ToString(typeof(TService))}");
						#endif
					}
					#if DEV_MODE || DEBUG || INIT_ARGS_SAFE_MODE
					else
					{
						#if DEV_MODE && DEBUG_LAZY_INIT
						Debug.Log($"Service.TryGetFor<{TypeUtility.ToString(typeof(TService))}>: false because LazyInit result task status {task.Status}");
						#endif
						task.OnFailure(ServiceInjector.HandleLogException);
					}
					#endif
				}
			}
			#endif

			if(!typeof(TService).IsValueType)
			{
				var globalService = Service<TService>.Instance;
				if(globalService != Null)
				{
					service = globalService;
					return true;
				}

				#if UNITY_EDITOR
				if(!Application.isPlaying
					&& ServiceAttributeUtility.TryGetInfoForDefiningType(typeof(TService), out ServiceInfo info)
					&& info.FindFromScene
					&& Find.Any(out service))
				{
					return true;
				}
				#endif
			}

			service = default;
			return false;
		}

		/// <summary>
		/// Gets all services of type <typeparamref name="TService"/> accessible to <paramref name="client"/>.
		/// <para>
		/// Returned services can include local services registered using <see cref="ServiceTag"/> and
		/// <see cref="Services"/> components in active scenes, as well as global services
		/// registered using <see cref="ServiceAttribute"/> and <see cref="Set{TService}">manually</see>.
		/// </para>
		/// </summary>
		/// <typeparam name="TService"> The defining type of the services. </typeparam>
		/// <param name="client"> The client <see cref="Component"/> that needs the services. </param>
		/// <returns>
		/// A collection containing zero or more services of type <typeparamref name="TService"/>
		/// accessible to the <see cref="client"/>.
		/// </returns>
		/// <remarks>
		/// This method can only be called from the main thread.
		/// </remarks>
		[return: NotNull]
		public static IEnumerable<TService> GetAllFor<TService>([DisallowNull] Component client)
		{
			#if DEV_MODE
			Debug.Assert(client);
			#endif

			foreach(var instance in LocalService<TService>.Instances)
			{
				#if UNITY_EDITOR
				if(!instance.registerer)
				{
					continue;
				}
				#endif

				if(!IsAccessibleTo(instance, client.transform))
				{
					continue;
				}

				if(instance.serviceProvider is { } serviceProvider)
				{
					if(serviceProvider.TryGetFor(client, out var service))
					{
						#if DEV_MODE && DEBUG_SERVICE_PROVIDERS
						Debug.Log($"Service.GetAllFor => {TypeUtility.ToString(serviceProvider.GetType())}.TryGetFor: {TypeUtility.ToString(service?.GetType())}");
						#endif
						yield return service;
					}
					#if DEV_MODE && DEBUG_SERVICE_PROVIDERS
					else { Debug.Log($"Service.GetAllFor => {TypeUtility.ToString(serviceProvider.GetType())}.TryGetFor: false"); }
					#endif
				}
				else if(instance.service != Null)
				{
					yield return instance.service;
				}
			}

			#if !INIT_ARGS_DISABLE_SERVICE_INJECTION
			bool isUninitializedLazyOrTransientService = ServiceInjector.TryGetUninitializedServiceInfo(typeof(TService), out var serviceInfo);
			if(isUninitializedLazyOrTransientService)
			{
				if(serviceInfo.LazyInit
   				#if UNITY_EDITOR
				&& Application.isPlaying
				#endif
				)
				{
					var task = ServiceInjector.LazyInit(serviceInfo, typeof(TService), client);
					if(task.IsCompletedSuccessfully)
					{
						if(task.Result is TService taskResult)
						{
							yield return taskResult;
						}
						else if(task.Result is Task nestedTask && nestedTask.GetResult() is { IsCompletedSuccessfully: true, Result: TService nestedTaskResult })
						{
							yield return nestedTaskResult;
						}
						#if DEV_MODE && DEBUG_LAZY_INIT
						else { Debug.Log($"Service.GetAllFor<{TypeUtility.ToString(typeof(TService))}>: LazyInit result {TypeUtility.ToString(task.Result?.GetType())} is not of type {TypeUtility.ToString(typeof(TService))}"); }
						#endif
					}
					#if DEV_MODE && DEBUG_LAZY_INIT
					else
					{
						Debug.Log($"Service.GetAllFor<{TypeUtility.ToString(typeof(TService))}>: LazyInit result task status {task.Status}");
						task.OnFailure(ServiceInjector.HandleLogException);
					}
					#endif
				}
			}
			#endif

			if(!typeof(TService).IsValueType)
			{
				var globalService = Service<TService>.Instance;
				if(globalService != Null)
				{
					yield return globalService;
				}
				#if UNITY_EDITOR
				else if(!Application.isPlaying
					&& ServiceAttributeUtility.TryGetInfoForDefiningType(typeof(TService), out ServiceInfo info)
					&& info.FindFromScene
					&& Find.Any(out globalService))
				{
					yield return globalService;
				}
				#endif
			}
		}

		/// <summary>
		/// Gets all global services of type <typeparamref name="TService"/>.
		/// <para>
		/// The returned services can include services registered using <see cref="ServiceTag"/> and
		/// <see cref="Services"/> components with availability set to <see cref="Clients.Everywhere"/>, and services
		/// registered using <see cref="ServiceAttribute"/> and <see cref="Set{TService}">manually</see>.
		/// </para>
		/// </summary>
		/// <typeparam name="TService"> The defining type of the services. </typeparam>
		/// <returns> A collection containing zero or more global services of type <typeparamref name="TService"/>. </returns>
		/// <remarks>
		/// This method can only be called from the main thread.
		/// </remarks>
		[return: NotNull]
		public static IEnumerable<TService> GetAll<TService>()
		{
			foreach(var instance in LocalService<TService>.Instances)
			{
				#if UNITY_EDITOR
				if(!instance.registerer)
				{
					continue;
				}
				#endif

				if(instance.clients != Clients.Everywhere)
				{
					continue;
				}

				if(instance.serviceProvider is not null)
				{
					if(instance.serviceProvider.TryGetFor(null, out var service))
					{
						#if DEV_MODE && DEBUG_SERVICE_PROVIDERS
						Debug.Log($"Service.GetAll => {TypeUtility.ToString(instance.serviceProvider.GetType())}.TryGetFor: {TypeUtility.ToString(service?.GetType())}");
						#endif
						yield return service;
					}
					#if DEV_MODE && DEBUG_SERVICE_PROVIDERS
					else { Debug.Log($"Service.GetAll => {TypeUtility.ToString(instance.serviceProvider.GetType())}.TryGetFor: false"); }
					#endif
				}
				else if(instance.service != Null)
				{
					yield return instance.service;
				}
			}

			#if !INIT_ARGS_DISABLE_SERVICE_INJECTION
			if(ServiceInjector.TryGetUninitializedServiceInfo(typeof(TService), out var serviceInfo)
				&& serviceInfo.LazyInit
				#if UNITY_EDITOR
				&& Application.isPlaying
				#endif
			)
			{
				var task = ServiceInjector.LazyInit(serviceInfo, typeof(TService), client: null);
				if(task.IsCompletedSuccessfully)
				{
					if(task.Result is TService taskResult)
					{
						yield return taskResult;
					}
					else if(task.Result is Task nestedTask && nestedTask.GetResult() is { IsCompletedSuccessfully: true, Result: TService nestedTaskResult })
					{
						yield return nestedTaskResult;
					}
					#if DEV_MODE && DEBUG_LAZY_INIT
					else { Debug.Log($"Service.GetAll<{TypeUtility.ToString(typeof(TService))}>: LazyInit result {TypeUtility.ToString(task.Result?.GetType())} it is not of type {TypeUtility.ToString(typeof(TService))}"); }
					#endif
				}
				#if DEV_MODE && DEBUG_LAZY_INIT
				else
				{
					Debug.Log($"Service.GetAll<{TypeUtility.ToString(typeof(TService))}>: LazyInit result task status {task.Status}");
					task.OnFailure(ServiceInjector.HandleLogException);
				}
				#endif
			}
			#endif

			if(!typeof(TService).IsValueType)
			{
				var globalService = Service<TService>.Instance;
				if(globalService != Null)
				{
					yield return globalService;
				}
				#if UNITY_EDITOR
				else if(!Application.isPlaying
					&& ServiceAttributeUtility.TryGetInfoForDefiningType(typeof(TService), out ServiceInfo info)
					&& info.FindFromScene
					&& Find.Any(out globalService))
				{
					yield return globalService;
				}
				#endif
			}
		}

		/// <summary>
		/// Tries to get <paramref name="service"/> of type <typeparamref name="TService"/>
		/// for <paramref name="client"/>.
		/// <para>
		/// The returned service can be a local service registered using a <see cref="ServiceTag"/> or a
		/// <see cref="Services"/> component in an active scene, or if one is not found, a global service
		/// registered using a <see cref="ServiceAttribute"/> or <see cref="Set{TService}">manually</see>.
		/// </para>
		/// </summary>
		/// <typeparam name="TService"> The defining type of the service. </typeparam>
		/// <param name="client"> The client <see cref="GameObject"/> that needs the service. </param>
		/// <param name="service">
		/// When this method returns, contains service of type <typeparamref name="TService"/>, if found; otherwise, <see langword="null"/>. This parameter is passed uninitialized.
		/// </param>
		/// <returns> <see langword="true"/> if service was found; otherwise, <see langword="false"/>. </returns>
		/// <remarks>
		/// This method can only be called from the main thread.
		/// </remarks>
		public static bool TryGetFor<TService>([DisallowNull] GameObject client, [NotNullWhen(true), MaybeNullWhen(false)] out TService service)
			=> TryGetFor(client.transform, out service);

		/// <summary>
		/// Tries to get a global <paramref name="service"/> of type <typeparamref name="TService"/>.
		/// <para>
		/// The returned service can be a service registered using a <see cref="ServiceTag"/> or a
		/// <see cref="Services"/> component with availability set to <see cref="Clients.Everywhere"/>, or a service
		/// registered using a <see cref="ServiceAttribute"/> or <see cref="Set{TService}">manually</see>.
		/// </para>
		/// </summary>
		/// <typeparam name="TService"> The defining type of the service. </typeparam>
		/// <param name="service">
		/// When this method returns, contains service of type <typeparamref name="TService"/>, if found; otherwise, <see langword="null"/>. This parameter is passed uninitialized.
		/// </param>
		/// <returns> <see langword="true"/> if service was found; otherwise, <see langword="false"/>. </returns>
		/// <remarks>
		/// This method can only be called from the main thread.
		/// </remarks>
		public static bool TryGet<TService>([NotNullWhen(true), MaybeNullWhen(false)] out TService service)
		{
			bool foundResult = false;
			LocalService<TService>.Instance nearest = default;

			foreach(var instance in LocalService<TService>.Instances)
			{
				#if UNITY_EDITOR
				if(!instance.registerer)
				{
					continue;
				}
				#endif

				if(instance.clients != Clients.Everywhere)
				{
					continue;
				}

				#if DEBUG || INIT_ARGS_SAFE_MODE && !INIT_ARGS_DISABLE_WARNINGS

				#if UNITY_EDITOR
				// Prioritize scene objects over uninstantiated prefabs
				var prefabStage = UnityEditor.SceneManagement.PrefabStageUtility.GetCurrentPrefabStage();
				if(!instance.Scene.IsValid() || (prefabStage && instance.Scene == prefabStage.scene))
				{
					continue;
				}

				if(!nearest.Scene.IsValid() || (prefabStage && nearest.Scene == prefabStage.scene))
				{
					nearest = instance;
					foundResult = true;
					continue;
				}

				// Don't spam warnings when services are requested in edit mode, for example in Inspector code.
				if(!Application.isPlaying)
				{
					continue;
				}
				#endif

				if(!EqualityComparer<TService>.Default.Equals(nearest.service, instance.service) && nearest.service != Null && instance.service != Null)
				{
					Debug.LogWarning($"AmbiguousMatchWarning: All clients have access to both services \"{nearest.Transform.name}\"/{TypeUtility.ToString(nearest.service.GetType())} and \"{instance.Transform.name}\"/{TypeUtility.ToString(instance.service.GetType())} and unable to determine which one should be prioritized.");
				}
				#endif

				nearest = instance;
				foundResult = true;

				#if !DEBUG
				break;
				#endif
			}

			if(foundResult)
			{
				if(nearest.serviceProvider is not null)
				{
					if(nearest.serviceProvider.TryGetFor(null, out service))
					{
						#if DEV_MODE && DEBUG_SERVICE_PROVIDERS
						Debug.Log($"Service.TryGet => {TypeUtility.ToString(nearest.serviceProvider.GetType())}.TryGetFor: {TypeUtility.ToString(service?.GetType())}");
						#endif
						return true;
					}
					#if DEV_MODE && DEBUG_SERVICE_PROVIDERS
					else { Debug.Log($"Service.TryGet => {TypeUtility.ToString(nearest.serviceProvider.GetType())}.TryGetFor: false"); }
					#endif
				}
				else if(nearest.service != Null)
				{
					service = nearest.service;
					return true;
				}
			}

			#if !INIT_ARGS_DISABLE_SERVICE_INJECTION
			if(ServiceInjector.TryGetUninitializedServiceInfo(typeof(TService), out var serviceInfo)
				&& serviceInfo.LazyInit
				#if UNITY_EDITOR
				&& Application.isPlaying
				#endif
			)
			{
				var task = ServiceInjector.LazyInit(serviceInfo, typeof(TService), client: null);
				if(task.IsCompletedSuccessfully)
				{
					if(task.Result is TService taskResult)
					{
						service = taskResult;
						return true;
					}

					if(task.Result is Task nestedTask && nestedTask.GetResult() is { IsCompletedSuccessfully: true, Result: TService nestedTaskResult })
					{
						service = nestedTaskResult;
						return true;
					}

					#if DEV_MODE && DEBUG_LAZY_INIT
					Debug.Log($"Service.TryGet<{TypeUtility.ToString(typeof(TService))}>: false because LazyInit result {TypeUtility.ToString(task.Result?.GetType())} is not of type {TypeUtility.ToString(typeof(TService))}");
					#endif
				}
				else
				{
					#if DEV_MODE && DEBUG_LAZY_INIT
					Debug.Log($"Service.TryGet<{TypeUtility.ToString(typeof(TService))}>: false because LazyInit result task status {task.Status}");
					#endif
					task.OnFailure(ServiceInjector.HandleLogException);
				}
				
			}
			#endif

			if(!typeof(TService).IsValueType)
			{
				service = Service<TService>.Instance;
				if(service != Null)
				{
					return true;
				}

				#if UNITY_EDITOR
				if(!Application.isPlaying
					&& ServiceAttributeUtility.TryGetInfoForDefiningType(typeof(TService), out ServiceInfo info)
					&& info.FindFromScene
					&& Find.Any(out service))
				{
					return true;
				}
				#endif
			}

			service = default;
			return false;
		}

		/// <summary>
		/// Gets service of type <typeparamref name="TService"/> for <paramref name="client"/>.
		/// <para>
		/// The returned service can be a local service registered using a <see cref="ServiceTag"/> or a
		/// <see cref="Services"/> component in an active scene, or if one is not found, a global service
		/// registered using a <see cref="ServiceAttribute"/> or <see cref="Set{TService}">manually</see>.
		/// </para>
		/// <para>
		/// This method can only be called from the main thread.
		/// </para>
		/// </summary>
		/// <typeparam name="TService"> The defining type of the service. </typeparam>
		/// <param name="client"> The client that needs the service. </param>
		/// <returns> Service of type <typeparamref name="TService"/>. </returns>
		/// <exception cref="NullReferenceException"> Thrown if no service of type <typeparamref name="TService"/> is found that is accessible to the <paramref name="client"/>. </exception>
		[return: NotNull]
		public static TService GetFor<TService>([DisallowNull] Component client)
			=> TryGetFor(client.gameObject, out TService result)
			? result
			: throw new NullReferenceException($"No service of type {typeof(TService).Name} was found that was accessible to client {TypeUtility.ToString(client.GetType())}.");

		/// <summary>
		/// Gets service of type <typeparamref name="TService"/> for <paramref name="client"/> asynchronously.
		/// <para>
		/// The returned service can be a local service registered using a <see cref="ServiceTag"/> or a
		/// <see cref="Services"/> component in an active scene, or if one is not found, a global service
		/// registered using a <see cref="ServiceAttribute"/> or <see cref="Set{TService}">manually</see>.
		/// </para>
		/// <para>
		/// This will suspend the execution of the calling async method until a service of type <see typeparamref="TService"/>
		/// becomes available for the <paramref name="client"/>.
		/// </para>
		/// </summary>
		/// <typeparam name="TService"> The defining type of the service. </typeparam>
		/// <param name="client"> The client that needs the service. </param>
		/// <param name="cancellationToken">
		/// Token that can be used to cancel the asynchronous operation (e.g. if the client gets destroyed while waiting for the value).
		/// </param>
		/// <param name="context"> Initialization phase during which the method is being called. </param>
		/// <returns> Service of type <typeparamref name="TService"/>. </returns>
		/// <remarks>
		/// This method can be called from any thread. If it is called from a non-main thread, it will
		/// switch to the main thread before trying to acquire the service.
		/// </remarks>
		public static async
		#if UNITY_2023_1_OR_NEWER
		Awaitable<TService>
		#else
		System.Threading.Tasks.Task<TService>
		#endif
		GetForAsync<TService>([DisallowNull] Component client, CancellationToken cancellationToken = default, Context context = Context.MainThread)
		{
			if(!context.IsUnitySafeContext())
			{
				await Until.UnitySafeContext();
			}

			var gameObject = client.gameObject;
			if(TryGetFor(gameObject, out TService result))
			{
				return result;
			}

			#if UNITY_2023_1_OR_NEWER
			AwaitableCompletionSource<TService> completionSource = new();
			ServiceChanged<TService>.listeners += OnServiceChanged;
			await using var registration = cancellationToken.Register(() => completionSource.TrySetCanceled());
			return await completionSource.Awaitable;
			#else
			TaskCompletionSource<TService> completionSource = new();
			ServiceChanged<TService>.listeners += OnServiceChanged;
			await using var registration = cancellationToken.Register(() => completionSource.TrySetCanceled());
			return await completionSource.Task;
			#endif

			void OnServiceChanged(Clients clients, TService oldInstance, TService newInstance)
			{
				if(!client)
				{
					ServiceChanged<TService>.listeners -= OnServiceChanged;
					completionSource.TrySetCanceled();
					return;
				}

				if(TryGetFor(gameObject, out TService result))
				{
					ServiceChanged<TService>.listeners -= OnServiceChanged;
					completionSource.TrySetResult(result);
				}
			}
		}

		/// <remarks>
		/// This method exists for backwards compatibility, it just forwards the call to <see cref="GetForAsync{TService}(Component, CancellationToken, Context)"/>.
		/// </remarks>
		[EditorBrowsable(EditorBrowsableState.Never)]
		public static
		#if UNITY_2023_1_OR_NEWER
		Awaitable<TService>
		#else
		System.Threading.Tasks.Task<TService>
		#endif
		GetForAsync<TService>([DisallowNull] Component client, Context context)
			=> GetForAsync<TService>(client, CancellationToken.None, context);

		/// <summary>
		/// Gets service of type <typeparamref name="TService"/> asynchrounously.
		/// <para>
		/// The returned service can be a service registered using a <see cref="ServiceTag"/> or a
		/// <see cref="Services"/> component with availability set to <see cref="Clients.Everywhere"/>, or a service
		/// registered using a <see cref="ServiceAttribute"/> or <see cref="Set{TService}">manually</see>.
		/// </para>
		/// <para>
		/// This will suspend the execution of the calling async method until a service of type <see typeparamref="TService"/>
		/// becomes available.
		/// </para>
		/// </summary>
		/// <typeparam name="TService"> The defining type of the service. </typeparam>
		/// <param name="cancellationToken">
		/// Token that can be used to cancel the asynchronous operation (e.g. if the client gets destroyed while waiting for the value).
		/// </param>
		/// <param name="context"> Initialization phase during which the method is being called. </param>
		/// <returns> Service of type <typeparamref name="TService"/>. </returns>
		/// <remarks>
		/// This method can be called from any thread. If it is called from a non-main thread, it will
		/// switch to the main thread before trying to acquire the service.
		/// </remarks>
		public static async
		#if UNITY_2023_1_OR_NEWER
		Awaitable<TService>
		#else
		System.Threading.Tasks.Task<TService>
		#endif
		GetAsync<TService>(CancellationToken cancellationToken = default, Context context = Context.MainThread)
		{
			if(!context.IsUnitySafeContext())
			{
				await Until.UnitySafeContext();
				cancellationToken.ThrowIfCancellationRequested();
			}

			if(TryGet(out TService result))
			{
				return result;
			}

			#if UNITY_2023_1_OR_NEWER
			AwaitableCompletionSource<TService>
			#else
			TaskCompletionSource<TService>
			#endif
				completionSource = new();

			ServiceChanged<TService>.listeners += OnServiceChanged;
			await using var registration = cancellationToken.Register(OnCanceled);

			return await completionSource.
				#if UNITY_2023_1_OR_NEWER
				Awaitable;
				#else
				Task;
				#endif

			void OnServiceChanged(Clients clients, TService oldInstance, TService newInstance)
			{
				if(TryGet(out TService result))
				{
					ServiceChanged<TService>.listeners -= OnServiceChanged;
					completionSource.TrySetResult(result);
				}
			}

			void OnCanceled()
			{
				ServiceChanged<TService>.listeners -= OnServiceChanged;
				completionSource.TrySetCanceled();
			}
		}

		/// <summary>
		/// Gets service of type <typeparamref name="TService"/> for <paramref name="client"/>.
		/// <para>
		/// The service can be retrieved from <see cref="Services"/> components in the active scenes, or failing that,
		/// from the globally shared <see cref="Service{TService}.Instance"/>.
		/// </para>
		/// <para>
		/// This method can only be called from the main thread.
		/// </para>
		/// </summary>
		/// <typeparam name="TService"> The defining type of the service. </typeparam>
		/// <param name="client"> The client <see cref="GameObject"/> that needs the service. </param>
		/// <returns> Service of type <typeparamref name="TService"/>. </returns>
		/// <exception cref="NullReferenceException"> Thrown if no service of type <typeparamref name="TService"/> is found that is accessible to the <paramref name="client"/>. </exception>
		[return: NotNull]
		public static TService GetFor<TService>([DisallowNull] GameObject client) => TryGetFor(client, out TService service) ? service : throw new NullReferenceException($"No service of type {typeof(TService).Name} was found that was accessible to client {client.GetType().Name}.");

		/// <summary>
		/// Gets service of type <typeparamref name="TService"/> for any client.
		/// <para>
		/// The service can be retrieved from <see cref="Services"/> components in the active scenes, or failing that,
		/// from the globally shared <see cref="Service{TService}.Instance"/>.
		/// </para>
		/// </summary>
		/// <typeparam name="TService"> The defining type of the service. </typeparam>
		/// <returns> Service of type <typeparamref name="TService"/>. </returns>
		/// <exception cref="NullReferenceException"> Thrown if no service of type <typeparamref name="TService"/> is found that is globally accessible to any client. </exception>
		/// <remarks>
		/// This method can only be called from the main thread.
		/// </remarks>
		[return: NotNull]
		public static TService Get<TService>() => TryGet(out TService service) ? service : throw new NullReferenceException($"No globally accessible Service of type {typeof(TService).Name} was found.");

		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		private static bool TryGetComponent(object client, out Component component)
		{
			component = client as Component;
			if(component)
			{
				return true;
			}

			var gameObject = client as GameObject;
			if(gameObject)
			{
				return gameObject.transform;
			}

			if(!Find.WrapperOf(client, out var wrapper))
			{
				component = null;
				return false;
			}

			component = wrapper as Component;
			return component;
		}

		/// <summary>
		/// Registers the given object as a global service of type <typeparamref name="TService"/>.
		/// </summary>
		/// <remarks>
		/// If the provided instance is not equal to the old <see cref="Service{TService}.Instance"/>
		/// then the <see cref="ServiceChanged{TService}.listeners"/> event will be raised.
		/// </remarks>
		/// <typeparam name="TService"> The defining type of the service. </typeparam>
		/// <param name="newInstance"> The new instance of the service. </param>
		public static void Set<TService>([DisallowNull] TService newInstance)
		{
			#if DEBUG
			if(typeof(TService).IsValueType)
			{
				Debug.LogError("Service.Set<TService>(TService) does not support value type services. Service.AddFor<TService>(Clients, IValueProvider<TService>, Component) can be used instead."
				#if UNITY_EDITOR
				, Find.Script(typeof(TService))
				#endif
				);
				return;
			}
			#endif

			Debug.Assert(newInstance != null, typeof(TService).Name);

			nowSettingInstance = typeof(TService);

			var oldInstance = Service<TService>.Instance;

			if(ReferenceEquals(oldInstance, newInstance))
			{
				nowSettingInstance = null;
				return;
			}

			Service<TService>.Instance = newInstance;

			nowSettingInstance = null;

			HandleInstanceChanged(Clients.Everywhere, oldInstance, newInstance
			#if UNITY_EDITOR
			, null, GetRegistererType()
			#endif
			);
		}

		/// <summary>
		/// Unregisters the current global service of type <typeparamref name="TService"/>.
		/// </summary>
		/// <remarks>
		/// If <see cref="Service{TService}.Instance"/> contains a non-null value,
		/// it will be set to <see langword="null"/>, and the <see cref="ServiceChanged{TService}.listeners"/>
		/// event will be raised.
		/// </remarks>
		/// <typeparam name="TService"> The defining type of the global service to unset. </typeparam>
		/// <returns>
		/// The global service of type <typeparamref name="TService"/> that was unset, if any; otherwise,
		/// the <see langword="default"/> value of <typeparamref name="TService"/>.
		/// </returns>
		public static TService Unset<TService>()
		{
			#if DEV_MODE
			Debug.Assert(!typeof(TService).IsValueType);
			#endif

			nowSettingInstance = typeof(TService);

			var currentInstance = Service<TService>.Instance;
			if(currentInstance is null)
			{
				nowSettingInstance = null;
				return currentInstance;
			}

			Service<TService>.Instance = default;
			nowSettingInstance = null;
			HandleInstanceChanged(Clients.Everywhere, currentInstance, default
			#if UNITY_EDITOR
			, null, GetRegistererType()
			#endif
			);
			return currentInstance;
		}

		/// <summary>
		/// Unregisters the provided global service of type <typeparamref name="TService"/>.
		/// </summary>
		/// <remarks>
		/// If <see cref="Service{TService}.Instance"/> contains <see paramref="instance"/> then it will be
		/// set to <see langword="null"/>, and the <see cref="ServiceChanged{TService}.listeners"/>
		/// event will be raised.
		/// </remarks>
		/// <typeparam name="TService"> The defining type of the global service to unregister. </typeparam>
		/// <param name="instance"> The global service to unregister. </param>
		/// <returns>
		/// <see langword="true"/> if the provided <paramref name="instance"/> was the current global service of type <typeparamref name="TService"/>;
		/// </returns>
		public static bool Unset<TService>([DisallowNull] TService instance)
		{
			#if DEV_MODE
			Debug.Assert(!typeof(TService).IsValueType);
			#endif

			var currentInstance = Service<TService>.Instance;
			if(!ReferenceEquals(currentInstance, instance))
			{
				return false;
			}

			nowSettingInstance = typeof(TService);
			Service<TService>.Instance = default;
			nowSettingInstance = null;

			HandleInstanceChanged(Clients.Everywhere, currentInstance, default
			#if UNITY_EDITOR
			, null, GetRegistererType()
			#endif
			);
			return true;
		}

		public static void Dispose<TService>()
		{
			nowSettingInstance = typeof(TService);

			var oldInstance = Service<TService>.Instance;

			if(oldInstance is null)
			{
				nowSettingInstance = null;
				return;
			}

			Service<TService>.Instance = default;

			if(oldInstance is Object unityObject)
			{
				if(unityObject)
				{
					Object.Destroy(unityObject);
				}
			}
			else if(oldInstance is IDisposable disposable)
			{
				disposable.Dispose();
			}

			nowSettingInstance = null;

			HandleInstanceChanged(Clients.Everywhere, oldInstance, default
			#if UNITY_EDITOR
			, null, GetRegistererType()
			#endif
			);
		}

		#if DEV_MODE
		internal static void Set<TService, TInstance>
		(
			string resourcePath,
			#if UNITY_ADDRESSABLES_1_17_4_OR_NEWER
			string addressableKey = null,
			#endif
			bool lazyInit = false,
			bool loadAsync = false,
			bool? instantiate = null
		)
			where TInstance : TService
		{
			Debug.Assert(!typeof(TInstance).IsAbstract, $"{nameof(Service)}.{nameof(Set)}<{TypeUtility.ToString(typeof(TService))}, {TypeUtility.ToString(typeof(TInstance))}> an abstract instance type: {TypeUtility.ToString(typeof(TInstance))}.");
			Debug.Assert(!typeof(TInstance).IsValueType, $"{nameof(Service)}.{nameof(Set)}<{TypeUtility.ToString(typeof(TService))}, {TypeUtility.ToString(typeof(TInstance))}> was with a instance type that was a struct: {TypeUtility.ToString(typeof(TInstance))}.");
			#if UNITY_ADDRESSABLES_1_17_4_OR_NEWER
			Debug.Assert(string.IsNullOrEmpty(resourcePath) || string.IsNullOrEmpty(addressableKey), $"{nameof(Service)}.{nameof(Set)} was called with both a {nameof(resourcePath)} (\"{resourcePath}\") and an {nameof(addressableKey)} (\"{addressableKey}\"). A service can't be loaded using two different methods simultaneously.");
			#endif

			var attribute = new ServiceAttribute
			{
				LazyInit = lazyInit,
				LoadAsync = loadAsync,
				ResourcePath = resourcePath,
				#if UNITY_ADDRESSABLES_1_17_4_OR_NEWER
				AddressableKey = addressableKey
				#endif
			};

			if(instantiate.HasValue)
			{
				attribute.Instantiate = instantiate.Value;
			}

			var concreteType = typeof(TService);
			var definingType = typeof(TInstance);
			var serviceInfo = new ServiceInfo(concreteType, new[] { attribute }, concreteType, new [] { definingType });
			ServiceInjector.Register(serviceInfo);
		}
		#endif

		/// <summary>
		/// Obsolete.
		/// <para>
		/// Use <see cref="Set{TService}"/> instead.
		/// </para>
		/// </summary>
		public static void SetInstance<TService>([DisallowNull] TService newInstance) => Set(newInstance);

		internal static void SetSilently<TService>([AllowNull] TService newInstance)
		{
			Debug.Assert(newInstance != null, typeof(TService).Name);

			nowSettingInstance = typeof(TService);

			var oldInstance = Service<TService>.Instance;

			if(ReferenceEquals(oldInstance, newInstance))
			{
				nowSettingInstance = null;
				return;
			}

			Service<TService>.Instance = newInstance;

			nowSettingInstance = null;
		}

		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		private static void HandleInstanceChanged<TService>(Clients clients, TService oldInstance, TService newInstance
			#if UNITY_EDITOR
			, [MaybeNull] Component registerer
			, RegistererType registererType
			#endif
			)
		{
			#if DEV_MODE && DEBUG_SERVICE_CHANGED
			Debug.Log($"Service {TypeUtility.ToString(typeof(TService))} Changed: {TypeUtility.ToString(oldInstance?.GetType())} -> {TypeUtility.ToString(newInstance?.GetType())})");
			#endif

			#if UNITY_EDITOR
			var activeInstances = ActiveInstancesEditorOnly;
			if(oldInstance is not null)
			{
				lock(activeInstances)
				{
					#if DEV_MODE && DEBUG_SERVICE_CHANGED
					bool removed =
					#endif

					activeInstances.Remove(new(typeof(TService), clients, oldInstance, registerer: registerer, registererType));

					#if DEV_MODE && DEBUG_SERVICE_CHANGED
					Debug.Log($"activeInstances.Remove(new({TypeUtility.ToString(typeof(TService))}, {clients}, {TypeUtility.ToString(oldInstance?.GetType())})): {removed}\nactiveInstances:" +
								string.Join("\n", activeInstances.Select(x => $"({TypeUtility.ToString(x.definingType)}, {x.ClientsText}, {TypeUtility.ToString(x.ServiceOrProvider?.GetType())})")));
					#endif
				}
			}

			if(newInstance is not null)
			{
				var serviceInfo = new ActiveServiceInfo(typeof(TService), clients, newInstance, registerer: registerer, registererType);
				lock(activeInstances)
				{
					int index = activeInstances.BinarySearch(serviceInfo, serviceInfoOrdererEditorOnly);
					if(index >= 0)
					{
						activeInstances[index] = serviceInfo;
					}
					else
					{
						activeInstances.Insert(~index, serviceInfo);
					}
				}
			}
			#endif

			ServiceChanged<TService>.listeners?.Invoke(clients, oldInstance, newInstance);

			#if UNITY_EDITOR
			if(!batchEditingServices)
			{
				AnyChangedEditorOnly?.Invoke();
			}
			#endif
		}
		
		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		private static void HandleProviderChanged<TService>(Clients clients, ServiceProvider<TService> removed, ServiceProvider<TService> added
			#if UNITY_EDITOR
			, [MaybeNull] Component registerer
			, RegistererType registererType
			#endif
			)
		{
			#if DEV_MODE && DEBUG_SERVICE_CHANGED
			Debug.Log($"Service {TypeUtility.ToString(typeof(TService))} Provider Changed: {TypeUtility.ToString(removed?.GetValueProvider()?.GetType())} -> {TypeUtility.ToString(added?.GetValueProvider()?.GetType())})");
			#endif

			ServiceProviderChanged<TService>.listeners?.Invoke(clients, removed, added);

			#if UNITY_EDITOR
			var activeInstances = ActiveInstancesEditorOnly;
			if(removed is not null)
			{
				lock(activeInstances)
				{
					#if DEV_MODE && DEBUG_SERVICE_CHANGED
					bool wasRemoved =
					#endif

					activeInstances.Remove(new(typeof(TService), clients, removed.GetValueProvider(), registerer: registerer, registererType));

					#if DEV_MODE && DEBUG_SERVICE_CHANGED
					Debug.Log($"activeInstances.Remove(new({TypeUtility.ToString(typeof(TService))}, {clients}, {TypeUtility.ToString(removed?.GetValueProvider()?.GetType())})): {wasRemoved}\nactiveInstances:" +
								string.Join("\n", activeInstances.Select(x => $"({TypeUtility.ToString(x.definingType)}, {x.ClientsText}, {TypeUtility.ToString(x.ServiceOrProvider?.GetType())})")));
					#endif
				}
			}

			if(added is not null)
			{
				var serviceInfo = new ActiveServiceInfo(typeof(TService), clients, added.GetValueProvider(), registerer: registerer, registererType);
				lock(activeInstances)
				{
					int index = activeInstances.BinarySearch(serviceInfo, serviceInfoOrdererEditorOnly);
					if(index >= 0)
					{
						activeInstances[index] = serviceInfo;
					}
					else
					{
						activeInstances.Insert(~index, serviceInfo);
					}
				}
			}
			#endif

			ServiceProviderChanged<TService>.listeners?.Invoke(clients, removed, added);

			#if UNITY_EDITOR
			if(!batchEditingServices)
			{
				AnyChangedEditorOnly?.Invoke();
			}
			#endif
		}

		[Conditional("UNITY_EDITOR")]
		internal static void HandleInitializationFailed(InitArgsException exception, [DisallowNull] ServiceInfo globalServiceInfo, ServiceInitFailReason reason, Object asset, Object sceneObject, object initializerOrWrapper, Type concreteType)
		{
			#if UNITY_EDITOR
			var serviceInfo = new ActiveServiceInfo(globalServiceInfo.definingTypes.FirstOrDefault() ?? concreteType, Clients.Everywhere, sceneObject ?? asset ?? initializerOrWrapper, sceneObject as Component ?? asset as Component, RegistererType.ServiceAttribute, exception.Message, reason);

			var activeInstances = ActiveInstancesEditorOnly;
			lock(activeInstances)
			{
				int index = activeInstances.BinarySearch(serviceInfo, serviceInfoOrdererEditorOnly);
				if(index >= 0)
				{
					activeInstances[index] = serviceInfo;
				}
				else
				{
					activeInstances.Insert(~index, serviceInfo);
				}
			}
			#endif
		}

		/// <summary>
		/// Registers a service with the defining type <typeparamref name="TService"/>
		/// available to a limited set of clients.
		/// <para>
		/// If the provided instance is available to clients <see cref="Clients.Everywhere"/>
		/// then the <see cref="ServiceChanged{TService}.listeners"/> event will be raised.
		/// </para>
		/// </summary>
		/// <typeparam name="TService">
		/// The defining type of the service; the class or interface type that uniquely defines
		/// the service and can be used to retrieve an instance of it.
		/// <para>
		/// This must be an interface that the service implement, a base type that the service derives from,
		/// or the exact type of the service.
		/// </para>
		/// </typeparam>
		/// <param name="clients">
		/// Specifies which client objects can receive the service instance in their Init function
		/// during their initialization.
		/// </param>
		/// <param name="service"> The service instance to add. </param>
		/// <param name="registerer">
		/// Component that is registering the service. This can also be the service itself, if it is a component.
		/// <para>
		/// This same argument should be passed when <see cref="RemoveFrom{TService}(Clients, TService)">removing the instance</see>.
		/// </para>
		/// </param>
		[Preserve]
		public static void AddFor<TService>(Clients clients, [DisallowNull] TService service, Component registerer)
		{
			#if DEV_MODE
			Debug.Assert(service != Null);
			Debug.Assert(registerer);
			#endif

			if(LocalService<TService>.Add(service, clients, registerer))
			{
				HandleInstanceChanged(clients, default, service
				#if UNITY_EDITOR
				, registerer, GetRegistererType(registerer)
				#endif
				);
			}
		}

		[Preserve]
		public static void AddFor<TService>(Clients clients, [DisallowNull] IValueProvider<TService> serviceProvider, [DisallowNull] Component registerer)
		{
			#if DEV_MODE
			Debug.Assert(serviceProvider != Null);
			Debug.Assert(registerer);
			#endif

			var adding = new ServiceProvider<TService>(serviceProvider);
			if(LocalService<TService>.Add(adding, clients, registerer))
			{
				HandleProviderChanged(clients, default, adding
				#if UNITY_EDITOR
				, registerer, GetRegistererType(registerer)
				#endif
				);
			}
		}

		[Preserve]
		public static void AddFor<TService>(Clients clients, [DisallowNull] IValueProviderAsync<TService> serviceProvider, [DisallowNull] Component registerer)
		{
			#if DEV_MODE
			Debug.Assert(serviceProvider != Null);
			Debug.Assert(registerer);
			#endif

			var adding = new ServiceProvider<TService>(serviceProvider);
			if(LocalService<TService>.Add(adding, clients, registerer))
			{
				HandleProviderChanged(clients, default, adding
				#if UNITY_EDITOR
				, registerer, GetRegistererType(registerer)
				#endif
				);
			}
		}

		[Preserve]
		public static void AddFor<TService>(Clients clients, [DisallowNull] IValueProvider serviceProvider, [DisallowNull] Component registerer)
		{
			#if DEV_MODE
			Debug.Assert(serviceProvider != Null);
			Debug.Assert(registerer);
			#endif

			var adding = new ServiceProvider<TService>(serviceProvider);
			if(LocalService<TService>.Add(adding, clients, registerer))
			{
				HandleProviderChanged(clients, default, adding
				#if UNITY_EDITOR
				, registerer, GetRegistererType(registerer)
				#endif
				);
			}
		}

		[Preserve]
		public static void AddFor<TService>(Clients clients, [DisallowNull] IValueProviderAsync serviceProvider, [DisallowNull] Component registerer)
		{
			#if DEV_MODE
			Debug.Assert(serviceProvider != Null);
			Debug.Assert(registerer);
			#endif

			var adding = new ServiceProvider<TService>(serviceProvider);
			if(LocalService<TService>.Add(adding, clients, registerer))
			{
				HandleProviderChanged(clients, default, adding
				#if UNITY_EDITOR
				, registerer, GetRegistererType(registerer)
				#endif
				);
			}
		}

		[Preserve]
		public static void AddFor<TService>(Clients clients, [DisallowNull] IValueByTypeProvider serviceProvider, [DisallowNull] Component registerer)
		{
			#if DEV_MODE
			Debug.Assert(serviceProvider != Null);
			Debug.Assert(registerer);
			#endif

			var adding = new ServiceProvider<TService>(serviceProvider);
			if(LocalService<TService>.Add(adding, clients, registerer))
			{
				HandleProviderChanged(clients, default, adding
				#if UNITY_EDITOR
				, registerer, GetRegistererType(registerer)
				#endif
				);
			}
		}

		[Preserve]
		public static void AddFor<TService>(Clients clients, [DisallowNull] IValueByTypeProviderAsync serviceProvider, [DisallowNull] Component registerer)
		{
			#if DEV_MODE
			Debug.Assert(serviceProvider != Null);
			Debug.Assert(registerer);
			#endif

			var adding = new ServiceProvider<TService>(serviceProvider);
			if(LocalService<TService>.Add(adding, clients, registerer))
			{
				HandleProviderChanged(clients, default, adding
				#if UNITY_EDITOR
				, registerer, GetRegistererType(registerer)
				#endif
				);
			}
		}

		[Preserve]
		public static void RemoveFrom<TService>(Clients clients, [DisallowNull] IValueProvider<TService> serviceProvider, [DisallowNull] Component registerer)
		{
			var removing = new ServiceProvider<TService>(serviceProvider);
			if(LocalService<TService>.Remove(removing, registerer))
			{
				HandleProviderChanged(clients, removing, default
				#if UNITY_EDITOR
				, registerer, GetRegistererType(registerer)
				#endif
				);
			}
		}

		[Preserve]
		public static void RemoveFrom<TService>(Clients clients, [DisallowNull] IValueProviderAsync<TService> serviceProvider, [DisallowNull] Component registerer)
		{
			var removing = new ServiceProvider<TService>(serviceProvider);
			if(LocalService<TService>.Remove(removing, registerer))
			{
				HandleProviderChanged(clients, removing, default
				#if UNITY_EDITOR
				, registerer, GetRegistererType(registerer)
				#endif
				);
			}
		}

		[Preserve]
		public static void RemoveFrom<TService>(Clients clients, [DisallowNull] IValueProvider serviceProvider, [DisallowNull] Component registerer)
		{
			var removing = new ServiceProvider<TService>(serviceProvider);
			if(LocalService<TService>.Remove(removing, registerer))
			{
				HandleProviderChanged(clients, removing, default
				#if UNITY_EDITOR
				, registerer, GetRegistererType(registerer)
				#endif
				);
			}
		}

		[Preserve]
		public static void RemoveFrom<TService>(Clients clients, [DisallowNull] IValueProviderAsync serviceProvider, [DisallowNull] Component registerer)
		{
			var removing = new ServiceProvider<TService>(serviceProvider);
			if(LocalService<TService>.Remove(removing, registerer))
			{
				HandleProviderChanged(clients, removing, default
				#if UNITY_EDITOR
				, registerer, GetRegistererType(registerer)
				#endif
				);
			}
		}

		[Preserve]
		public static void RemoveFrom<TService>(Clients clients, [DisallowNull] IValueByTypeProvider serviceProvider, [DisallowNull] Component registerer)
		{
			var removing = new ServiceProvider<TService>(serviceProvider);
			if(LocalService<TService>.Remove(removing, registerer))
			{
				HandleProviderChanged(clients, removing, default
				#if UNITY_EDITOR
				, registerer, GetRegistererType(registerer)
				#endif
				);
			}
		}

		[Preserve]
		public static void RemoveFrom<TService>(Clients clients, [DisallowNull] IValueByTypeProviderAsync serviceProvider, [DisallowNull] Component registerer)
		{
			var removing = new ServiceProvider<TService>(serviceProvider);
			if(LocalService<TService>.Remove(removing, registerer))
			{
				HandleProviderChanged(clients, removing, default
				#if UNITY_EDITOR
				, registerer, GetRegistererType(registerer)
				#endif
				);
			}
		}

		/// <summary>
		/// Registers a service with the defining type <typeparamref name="TService"/>
		/// available to a limited set of clients.
		/// <para>
		/// If the provided instance is available to clients <see cref="Clients.Everywhere"/>
		/// then the <see cref="ServiceChanged{TService}.listeners"/> event will be raised.
		/// </para>
		/// </summary>
		/// <typeparam name="TService">
		/// The defining type of the service; the class or interface type that uniquely defines
		/// the service and can be used to retrieve an instance of it.
		/// <para>
		/// This must be a base type that the service derives from, or the exact type of the service.
		/// </para>
		/// <para>
		/// This must also be a component type. If you want to register a service that is not a component,
		/// or want to register a component service using an interface that it implements, you can use
		/// the <see cref="AddFor{TService}(Clients, TService, Component)"> overload</see> that
		/// lets you provide a <see cref="Component"/> type reference separately.
		/// </para>
		/// </typeparam>
		/// <param name="clients">
		/// Specifies which client objects can receive the service instance in their Init function
		/// during their initialization.
		/// </param>
		/// <param name="service"> The service component to add. </param>
		[Preserve]
		public static void AddFor<TService>(Clients clients, [DisallowNull] TService service) where TService : Component
		{
			#if DEV_MODE
			Debug.Assert(service);
			#endif

			if(LocalService<TService>.Add(service, clients, service))
			{
				HandleInstanceChanged(clients, null, service
				#if UNITY_EDITOR
				, service, GetRegistererType()
				#endif
				);
			}
		}

		/// <summary>
		/// Unregisters a service with the defining type <typeparamref name="TService"/>
		/// that has been available to a limited set of clients.
		/// <para>
		/// If the provided instance is available to clients <see cref="Clients.Everywhere"/>
		/// then the <see cref="ServiceChanged{TService}.listeners"/> event will be raised.
		/// </para>
		/// </summary>
		/// <typeparam name="TService">
		/// The defining type of the service; the class or interface type that uniquely defines
		/// the service and can be used to retrieve an instance of it.
		/// <para>
		/// This must be an interface that the service implement, a base type that the service derives from,
		/// or the exact type of the service.
		/// </para>
		/// </typeparam>
		/// <param name="clients"> The availability of the service being removed. </param>
		/// <param name="service"> The service instance to remove. </param>
		/// <param name="serviceProvider"> Component that registered the service. </param>
		[Preserve]
		public static void RemoveFrom<TService>(Clients clients, [DisallowNull] TService service, [DisallowNull] Component registerer)
		{
			if(LocalService<TService>.Remove(service, registerer))
			{
				HandleInstanceChanged(clients, service, default
				#if UNITY_EDITOR
				, registerer, GetRegistererType(registerer)
				#endif
				);
			}
		}

		/// <summary>
		/// Unregisters a service with the defining type <typeparamref name="TService"/>
		/// that has been available to a limited set of clients.
		/// <para>
		/// If the provided instance is available to clients <see cref="Clients.Everywhere"/>
		/// then the <see cref="ServiceChanged{TService}.listeners"/> event will be raised.
		/// </para>
		/// </summary>
		/// <typeparam name="TService">
		/// The defining type of the service; the class or interface type that uniquely defines
		/// the service and can be used to retrieve an instance of it.
		/// <para>
		/// This must be an interface that the service implement, a base type that the service derives from,
		/// or the exact type of the service.
		/// </para>
		/// </typeparam>
		/// <param name="clients"> The availability of the service being removed. </param>
		/// <param name="registerer"> Component that registered the service. </param>
		[Preserve]
		public static void RemoveFrom<TService>(Clients clients, [DisallowNull] Component registerer)
		{
			if(LocalService<TService>.RemoveFrom(registerer, out TService service, out _))
			{
				HandleInstanceChanged(clients, service, default
				#if UNITY_EDITOR
				, registerer, GetRegistererType(registerer)
				#endif
				);
			}
		}

		/// <summary>
		/// Unregisters a service with the defining type <typeparamref name="TService"/>
		/// that has been available to a limited set of clients.
		/// <para>
		/// If the provided instance is available to clients <see cref="Clients.Everywhere"/>
		/// then the <see cref="ServiceChanged{TService}.listeners"/> event will be raised.
		/// </para>
		/// </summary>
		/// <typeparam name="TService">
		/// The defining type of the service; the class or interface type that uniquely defines
		/// the service and can be used to retrieve an instance of it.
		/// <para>
		/// This must be an interface that the service implement, a base type that the service derives from,
		/// or the exact type of the service.
		/// </para>
		/// <para>
		/// This must also be a component type. If you want to unregister a service that is not a component,
		/// or want to unregister a component service using an interface that it implements, you can use
		/// the <see cref="RemoveFrom{TService}(Clients, TService, Component)"> overload</see> that
		/// lets you provide a <see cref="Component"/> type reference separately.
		/// </para>
		/// </typeparam>
		/// <param name="clients"> The availability of the service being removed. </param>
		/// <param name="service"> The service component to remove. </param>
		[Preserve]
		public static void RemoveFrom<TService>(Clients clients, [DisallowNull] TService service) where TService : Component
		{
			if(LocalService<TService>.Remove(service, service))
			{
				HandleInstanceChanged(clients, service, default
				#if UNITY_EDITOR
				, service, GetRegistererType()
				#endif
				);
			}
		}

		/// <summary>
		/// Subscribes the provided <paramref name="method"/> to listen for changes made to the shared instance of service of type <typeparamref name="TService"/>.
		/// <para>
		/// The method will only be called when in reaction to services that are accesible by all <see cref="Clients"/> changing.
		/// </para>
		/// </summary>
		/// <typeparam name="TService"> The defining type of the service. </typeparam>
		/// <param name="method">
		/// Method to call when the shared instance of service of type <typeparamref name="TService"/> has changed to a different one.
		/// </param>
		[Preserve]
		public static void AddInstanceChangedListener<TService>(ServiceChangedHandler<TService> method) => ServiceChanged<TService>.listeners += method;

		[Preserve]
		public static void AddChangedListener<TService>(Action method)
		{
			ServiceChangedHandler<TService> serviceHandler = (_, _, _) => method();
			ServiceChanged<TService>.handlers[method] = serviceHandler;
			ServiceChanged<TService>.listeners += serviceHandler;

			ServiceProviderChangedHandler<TService> providerHandler = (_, _, _) => method();
			ServiceProviderChanged<TService>.handlers[method] = providerHandler;
			ServiceProviderChanged<TService>.listeners += providerHandler;
		}

		[Preserve]
		public static void RemoveChangedListener<TService>(Action method)
		{
			if(ServiceChanged<TService>.handlers.TryGetValue(method, out var serviceHandler))
			{
				ServiceChanged<TService>.listeners -= serviceHandler;
				ServiceChanged<TService>.handlers.Remove(method);
			}

			if(ServiceProviderChanged<TService>.handlers.TryGetValue(method, out var providerHandler))
			{
				ServiceProviderChanged<TService>.listeners -= providerHandler;
				ServiceProviderChanged<TService>.handlers.Remove(method);
			}
		}

		/// <summary>
		/// Unsubscribes the provided <paramref name="method"/> from listening for changes made to the shared instance of service of type <typeparamref name="TService"/>.
		/// </summary>
		/// <typeparam name="TService"> The defining type of the service. </typeparam>
		/// <param name="method">
		/// Method that should no longer be called when the shared instance of service of type <typeparamref name="TService"/> has changed to a different one.
		/// </param>
		[Preserve]
		public static void RemoveInstanceChangedListener<TService>(ServiceChangedHandler<TService> method) => ServiceChanged<TService>.listeners -= method;

		internal static bool IsAccessibleTo<TService>(LocalService<TService>.Instance instance, [DisallowNull] Transform clientTransform)
		{
			#if DEV_MODE
			Debug.Assert(clientTransform);
			Debug.Assert(instance.registerer);
			#endif

			#if UNITY_EDITOR
			// Skip services from prefabs - this can help avoid AmbiguousMatchWarning issues.
			// However, if both objects are part of the same prefab, then it's important to not return false,
			// as otherwise Service tags would never get drawn inside prefabs being Inspected.
			if(clientTransform.gameObject.scene != instance.Scene && instance.Scene.IsInvalidOrPrefabStage())
			{
				return false;
			}
			#endif

			switch(instance.clients)
			{
				case Clients.InGameObject:
					return instance.Transform == clientTransform;
				case Clients.InChildren:
					for(var parent = clientTransform.transform; parent; parent = parent.parent)
					{
						if(parent == instance.Transform)
						{
							return true;
						}
					}
					return false;
				case Clients.InParents:
					for(var parent = instance.Transform; parent; parent = parent.parent)
					{
						if(parent == clientTransform)
						{
							return true;
						}
					}
					return false;
				case Clients.InHierarchyRootChildren:
					return instance.Transform.root == clientTransform.root;
				case Clients.InScene:
					return clientTransform.gameObject.scene == instance.Scene;
				case Clients.InAllScenes:
				case Clients.Everywhere:
					return true;
				default:
					Debug.LogError($"Unrecognized {nameof(Clients)} value: {instance.clients}.", instance.Transform);
					return false;
			}
		}

		internal static bool IsAccessibleTo([AllowNull] Component client, [DisallowNull] Component registerer, Clients accessibility)
		{
			if(!client)
			{
				return accessibility == Clients.Everywhere;
			}

			#if UNITY_EDITOR
			// Skip services from prefabs. This can help avoid AmbiguousMatchWarning issues.
			if(client.gameObject.scene.IsValid() && (!registerer.gameObject.scene.IsValid() || (UnityEditor.SceneManagement.PrefabStageUtility.GetCurrentPrefabStage() is var prefabStage && prefabStage && registerer.gameObject.scene == prefabStage.scene)))
			{
				return false;
			}
			#endif

			switch(accessibility)
			{
				case Clients.InGameObject:
					return registerer.transform == client.transform;
				case Clients.InChildren:
					var serviceTransform = registerer.transform;
					for(var t = client.transform; t; t = t.parent)
					{
						if(ReferenceEquals(t, serviceTransform))
						{
							return true;
						}
					}

					return false;
				case Clients.InParents:
					var clientTransform = registerer.transform;
					for(var t = registerer.transform; t; t = t.parent)
					{
						if(ReferenceEquals(t, clientTransform))
						{
							return true;
						}
					}

					return false;
				case Clients.InHierarchyRootChildren:
					return ReferenceEquals(registerer.transform.root, client.transform.root);
				case Clients.InScene:
					return client.gameObject.scene.handle == registerer.gameObject.scene.handle;
				case Clients.InAllScenes:
				case Clients.Everywhere:
					return true;
				default:
					Debug.LogError($"Unrecognized {nameof(Clients)} value: {accessibility}.", registerer);
					return false;
			}
		}

		#if UNITY_EDITOR
		private static RegistererType GetRegistererType(Component registerer)
		{
			return registerer switch
			{
				ServiceTag => RegistererType.ServiceTag,
				Services => RegistererType.ServicesComponent,
				_ when ServiceInjector.ServiceInitializationInProgressEditorOnly => RegistererType.ServiceAttribute,
				_ when EditorServiceInjector.ServiceInitializationInProgress => RegistererType.EditorServiceAttribute,
				_ => RegistererType.Manual
			};
		}
		
		private static RegistererType GetRegistererType()
			=> ServiceInjector.ServiceInitializationInProgressEditorOnly
				? RegistererType.ServiceAttribute
				: EditorServiceInjector.ServiceInitializationInProgress
					? RegistererType.EditorServiceAttribute
					: RegistererType.Manual;

		internal enum RegistererType
		{
			Manual,
			ServicesComponent,
			ServiceTag,
			ServiceAttribute,
			EditorServiceAttribute
		}

		[StructLayout(LayoutKind.Auto)]
		internal struct ActiveServiceInfo : IEquatable<ActiveServiceInfo>
		{
			[MaybeNull]
			public readonly Type ConcreteType;
			public readonly Clients ToClients;
			[MaybeNull]
			public readonly object ServiceOrProvider;
			public readonly Component Registerer;
			public readonly string ClientsText;
			private bool? isAsset;
			public readonly Type definingType;

			public readonly GUIContent Label; // text will be empty until Setup has been called
			public bool IsAsset => isAsset ??= IsPrefabAsset(ServiceOrProvider);
			public UnityEngine.SceneManagement.Scene Scene => ServiceOrProvider is not null && Find.GameObjectOf(ServiceOrProvider, out var gameObject) ? gameObject.scene : default;
			public Transform Transform => ServiceOrProvider is not null && Find.In<Transform>(ServiceOrProvider, out var transform) ? transform : null;
			public readonly ServiceInitFailReason InitFailReason;
			public readonly RegistererType RegistererType;

			public bool IsSetupDone => Label.text.Length > 0;

			public ActiveServiceInfo(Type definingType, Clients toClients, [AllowNull] object serviceOrProvider, [AllowNull] Component registerer, RegistererType registererType, string tooltip = "", ServiceInitFailReason initFailReason = ServiceInitFailReason.None)
			{
				Label = new("", tooltip);
				ToClients = toClients;
				ServiceOrProvider = serviceOrProvider;
				Registerer = registerer;
				RegistererType = registererType;
				InitFailReason = initFailReason;

				ConcreteType = serviceOrProvider?.GetType();
				if(ConcreteType?.IsGenericType ?? false)
				{
					var typeDefinition = ConcreteType.GetGenericTypeDefinition();
					if(typeDefinition == typeof(ValueTask<>) || typeDefinition == typeof(Task<>) || typeDefinition == typeof(Lazy<>))
					{
						ConcreteType = ConcreteType.GetGenericArguments()[0];
					}
				}

				ClientsText = ToClients.ToString();
				isAsset = null;
				this.definingType = definingType;
			}

			public void Setup(Span<Type> definingTypes)
			{
				var sb = new StringBuilder();
				if(ConcreteType is not null)
				{
					sb.Append(TypeUtility.ToString(ConcreteType));

					int count = definingTypes.Length;
					if(count == 0 || (count == 1 && definingTypes[0] == ConcreteType))
					{
						Label.text = sb.ToString();
						return;
					}

					sb.Append(" <color=grey>(");
					AddDefiningtypes(sb, definingTypes);
					sb.Append(")</color>");
				}
				else
				{
					AddDefiningtypes(sb, definingTypes);
				}

				Label.text = sb.ToString();

				if(Label.tooltip.Length is 0)
				{
					Label.tooltip = RegistererType switch
					{
						RegistererType.ServicesComponent when (bool)Registerer => $"Registered via Services component on '{Registerer.name}'",
						RegistererType.ServicesComponent => "Registered via Services component",
						RegistererType.ServiceTag when (bool)Registerer => $"Registered via Service Tag on '{Registerer.name}'",
						RegistererType.ServiceTag => "Registered via Service Tag.",
						RegistererType.ServiceAttribute => "Registered via [Service] attribute",
						RegistererType.EditorServiceAttribute => "Registered via [EditorService] attribute",
						RegistererType.Manual when (bool)Registerer => $"Registered manually by '{Registerer.name}'.",
						RegistererType.Manual => "Registered manually.",
						_ => "Registered using unknown method"
					};
				}

				[MethodImpl(MethodImplOptions.AggressiveInlining)]
				static void AddDefiningtypes(StringBuilder sb, Span<Type> definingTypes)
				{
					sb.Append(TypeUtility.ToString(definingTypes[0]));
					for(int i = 1, count = definingTypes.Length; i < count; i++)
					{
						sb.Append(", ");
						sb.Append(TypeUtility.ToString(definingTypes[i]));
					}
				}
			}

			public bool Equals(ActiveServiceInfo other) => ReferenceEquals(ServiceOrProvider, other.ServiceOrProvider);
			public override bool Equals(object obj) => obj is ActiveServiceInfo serviceInfo && Equals(serviceInfo);
			public override int GetHashCode() => ServiceOrProvider.GetHashCode();
			private static bool IsPrefabAsset([AllowNull] object obj) => obj != NullExtensions.Null && Find.In(obj, out Transform transform) && transform.gameObject.IsPartOfPrefabAsset();
		}

		private sealed class ServiceInfoOrderer : IComparer<ActiveServiceInfo>
		{
			public int Compare(ActiveServiceInfo x, ActiveServiceInfo y)
			{
				if(x.ConcreteType is { } xConcreteType)
				{
					if(y.ConcreteType is { } yConcreteType)
					{
						if(xConcreteType == yConcreteType)
						{
							return 0;
						}
					}
				}
				
				if(x.IsSetupDone && y.IsSetupDone)
				{
					int compareLabels = x.Label.text.CompareTo(y.Label.text);
					return compareLabels != 0 ? compareLabels : -1;
				}

				int compareTypeNames = x.definingType.Name.CompareTo(y.definingType.Name);
				return compareTypeNames != 0 ? compareTypeNames : -1;
			}
		}
		#endif
	}
}