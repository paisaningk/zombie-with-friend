using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Linq;
using Sisus.Init.ValueProviders;
using UnityEngine;
using UnityEngine.Serialization;
#if UNITY_EDITOR
using UnityEditor;
using Sisus.Shared.EditorOnly;
#endif

namespace Sisus.Init.Internal
{
	/// <summary>
	/// The <see cref="Services"/> component can be attached to a <see cref="GameObject"/> and used to
	/// define services which can be injected to its clients as part of their initialization process.
	/// <para>
	/// Clients which have access to all the services defined in the component can be configured to be
	/// limited only to members belonging to any of the following groups:
	/// <list type="table">
	/// <item><term> In GameObject </term>
	/// <description> Only clients that are attached to the same <see cref="UnityEngine.GameObject"/> as the
	/// <see cref="Services"/> component have access to its services.
	/// </description></item>
	/// <item><term> In Children </term>
	/// <description> Only clients that are attached to the same <see cref="UnityEngine.GameObject"/> as the
	/// <see cref="Services"/> component or any child GameObjects below it (including nested children)
	/// have access to its services.
	/// </description></item>
	/// <item><term> In Parents </term>
	/// <description> Only clients that are attached to the same <see cref="UnityEngine.GameObject"/> as the
	/// <see cref="Services"/> component or any parent GameObjects above it (including nested parents)
	/// have access to its services.
	/// </description></item>
	/// <item><term> In Hierarchy Root Children </term>
	/// <description> Only clients that are attached to the <see cref="UnityEngine.GameObject"/> at the
	/// <see cref="UnityEngine.Transform.root"/> of the hierarchy when traversing up the <see cref="Services"/>
	/// component's parent chain, or any child GameObjects below the root GameObject (including nested children)
	/// have access to its services.
	/// </description></item>
	/// <item><term> In Scene </term>
	/// <description> Only clients belonging to the same <see cref="UnityEngine.GameObject.scene"/> as the
	/// <see cref="Services"/> component have access to its services.
	/// </description></item>
	/// <item><term> In All Scenes </term>
	/// <description>
	/// All clients belonging to any scene have access to the services in the <see cref="Services"/> component.
	/// <para>
	/// Clients that don't belong to any scene, such as <see cref="UnityEngine.ScriptableObject">ScriptableObjects</see>
	/// and plain old classes that are not attached to a <see cref="UnityEngine.GameObject"/> via a <see cref="Wrapper{T}"/> component,
	/// can not access the services in the <see cref="Services"/> component.
	/// </para>
	/// </description></item>
	/// <item><term> Everywhere </term>
	/// <description>
	/// All clients have access to the services in the <see cref="Services"/> component without limitations.
	/// <para>
	/// This includes clients that don't belong to any scene, such as <see cref="UnityEngine.ScriptableObject">ScriptableObjects</see>
	/// and plain old classes.
	/// </para>
	/// </description></item>
	/// </list>
	/// </para>
	/// </summary>
	/// <seealso cref="ServiceTag"/>
	/// <seealso cref="ServiceAttribute"/>
	[ExecuteAlways, AddComponentMenu("Initialization/Services"), DefaultExecutionOrder(ExecutionOrder.ServiceTag)]
	public partial class Services : MonoBehaviour, IValueByTypeProvider, IServiceProvider
	#if UNITY_EDITOR
	, INullGuardByType
	#endif
	{
		#if UNITY_EDITOR
		private static readonly HashSet<Services> allEditorOnly = new();

		internal static IEnumerable<Services> GetAllEditorOnly(bool includeInactive = false)
		{
			foreach(var item in allEditorOnly)
			{
				if(!item)
				{
					allEditorOnly.RemoveWhere(x => !x);
					return GetAllEditorOnly(includeInactive);
				}
			}

			return includeInactive ? allEditorOnly : allEditorOnly.Where(x => x.isActiveAndEnabled);
		}
		#endif

		[SerializeField, FormerlySerializedAs("provideServices")]
		internal ServiceDefinition[] providesServices = Array.Empty<ServiceDefinition>();

		[SerializeField,
		 Tooltip("Specifies which clients can use these services.\n\n" +
		 "When set to " + nameof(Clients.InChildren) + ", only clients that are attached to this GameObject or its children (including nested children) can access these services.\n\n" +
		 "When set to " + nameof(Clients.InScene) + ", only clients that are in the same scene can access these services.\n\n" +
		 "When set to " + nameof(Clients.Everywhere) + ", all clients can access these services, regardless of their location in a scene, or whether they are a scene object at all.")]
		internal Clients toClients = Clients.InChildren;

		/// <inheritdoc cref="IServiceProvider.TryGet{TService}(out TService)"/>
		public bool TryGet<TService>(out TService service) => TryGetFor(null, out service);

		/// <inheritdoc cref="IServiceProvider.TryGetFor{TService}(Component, out TService)"/>
		public bool TryGetFor<TService>([AllowNull] Component client, out TService service)
		{
			if(!AreAvailableToAnyClient() && (!client || !AreAvailableToClient(client.gameObject)))
			{
				service = default;
				return false;
			}

			foreach(ServiceDefinition definition in providesServices)
			{
				if(definition.definingType.Value != typeof(TService))
				{
					continue;
				}

				if(definition.definingType.Value == typeof(TService) && definition.service is TService result && definition.service)
				{
					service = result;
					return true;
				}
			}

			service = default;
			return false;
		}

		bool IValueByTypeProvider.IsValueTypeSupported(Type valueType)
		{
			foreach(ServiceDefinition definition in providesServices)
			{
				if(definition.definingType.Value == valueType)
				{
					return true;
				}
			}

			return false;
		}

		IEnumerable<Type> IValueByTypeProvider.GetSupportedValueTypes() => providesServices.Select(d => d.definingType.Value).Where(t => t is not null);

		internal int GetStateBasedHashCode()
		{
			unchecked
			{
				int hashCode = 397 * providesServices.Length;
				for(int n = providesServices.Length - 1; n >= 0; n--)
				{
					hashCode = (hashCode * 397) ^ providesServices[n].GetStateBasedHashCode();
				}
				return (hashCode * 397) ^ (int)toClients;
			}
		}

		#if UNITY_EDITOR
		private bool isFirstOnValidate = true;
		private async void OnValidate()
		{
			await Until.UnitySafeContext();

			if(!this)
			{
				return;
			}

			allEditorOnly.Add(this);

			// Avoid unnecessary double validation and registration during initial loading via both OnValidate and OnEnable.
			// This also helps prevent duplicate validation warnings.
			if(isFirstOnValidate)
			{
				isFirstOnValidate = false;
				return;
			}

			// In Edit Mode register all services that are loaded to memory for purposes of Null Argument Guard
			// analysis, and visibility in Service Debugger window, but in Play Mode only services that have
			// been instantiated into a scene and are active and enabled should be registered.
			if(Application.isPlaying)
			{
				return;
			}

			ServiceUtility.RemoveAllServicesProvidedBy(this);

			if(isActiveAndEnabled)
			{
				Register();
			}
		}
		#endif

		protected virtual void OnEnable()
		{
			#if UNITY_EDITOR
			// Avoid duplicate services if they have already been registered via OnValidate.
			ServiceUtility.RemoveAllServicesProvidedBy(this);
			allEditorOnly.Add(this);
			#endif

			Register();
		}

		protected virtual void Register()
		{
			#if UNITY_EDITOR
			try
			{
			#endif

				for(var i = 0; i < providesServices.Length; i++)
				{
					var serviceDefinition = providesServices[i];
					var service = serviceDefinition.service;
					if(!service)
					{
						#if UNITY_EDITOR
						if(InspectorContents.IsBeingInspected(this))
						{
							continue;
						}
						#endif

						Debug.LogWarning($"Local Service #{i} on Services component on \"{name}\" is missing.", this);
						continue;
					}

					var definingType = serviceDefinition.definingType.Value;

					#if DEV_MODE || DEBUG || INIT_ARGS_SAFE_MODE
					if(definingType is null)
					{
						#if UNITY_EDITOR
						if(InspectorContents.IsBeingInspected(this))
						{
							continue;
						}
						#endif
						
						if(string.IsNullOrEmpty(serviceDefinition.definingType.TypeNameAndAssembly))
						{
							Debug.LogWarning($"Local Service {TypeUtility.ToString(service.GetType())} on Services component on \"{name}\" has no defining type selected.", this);
						}
						else
						#if UNITY_EDITOR
						if(!EditorUtility.scriptCompilationFailed)
						#endif
						{
							Debug.LogWarning($"Local Service {TypeUtility.ToString(service.GetType())} on Services component on \"{name}\" has a defining type which could not be deserialized: {serviceDefinition.definingType.TypeNameAndAssembly}.", this);
						}

						continue;
					}
					#endif

					#if DEV_MODE || DEBUG || INIT_ARGS_SAFE_MODE
					if(!ServiceTagUtility.IsValidDefiningType(definingType, service))
					{
						#if UNITY_EDITOR
						if(InspectorContents.IsBeingInspected(this))
						{
							continue;
						}
						#endif

						if(definingType.IsInterface)
						{
							Debug.LogWarning($"Local Service {TypeUtility.ToString(service.GetType())} on Services component on \"{name}\" has the defining type {TypeUtility.ToString(definingType)} but the service's class does not implement the interface.", this);
						}
						else
						{
							Debug.LogWarning($"Local Service {TypeUtility.ToString(service.GetType())} on Services component on \"{name}\" has the defining type {TypeUtility.ToString(definingType)} but the service's class does not derive from the class.", this);
						}

						continue;
					}
					#endif

					#if UNITY_EDITOR
					// Avoid raising the Service.AnyChangedEditorOnly more than once
					Service.BatchEditingServices = true;
					#endif

					ServiceUtility.AddFor(service, definingType, toClients, this);
				}

			#if UNITY_EDITOR
			}
			finally
			{
				Service.BatchEditingServices = false;
			}
			#endif
		}

		protected virtual void OnDisable()
		{
			foreach(var serviceDefinition in providesServices)
			{
				var service = serviceDefinition.service;
				if(service is null)
				{
					continue;
				}

				var definingType = serviceDefinition.definingType.Value;
				if(definingType is null)
				{
					continue;
				}

				ServiceUtility.RemoveFrom(service, definingType, toClients, this);
			}
		}

		#if UNITY_EDITOR
		protected virtual void OnDestroy() => allEditorOnly.Remove(this);
		#endif

		internal virtual bool AreAvailableToAnyClient() => toClients is Clients.Everywhere;

		internal virtual bool AreAvailableToClient([DisallowNull] GameObject client)
		{
			Debug.Assert((bool)client);
			Debug.Assert(this);

			switch(toClients)
			{
				case Clients.InGameObject:
					return ReferenceEquals(client, gameObject);
				case Clients.InChildren:
					var injectorTransform = transform;
					for(var parent = client.transform; parent; parent = parent.parent)
					{
						if(ReferenceEquals(parent, injectorTransform))
						{
							return true;
						}
					}
					return false;
				case Clients.InParents:
					var clientTransform = client.transform;
					for(var parent = transform; parent; parent = parent.parent)
					{
						if(ReferenceEquals(parent, clientTransform))
						{
							return true;
						}
					}
					return false;
				case Clients.InHierarchyRootChildren:
					return ReferenceEquals(transform.root, client.transform.root);
				case Clients.InScene:
					return client.scene == gameObject.scene;
				case Clients.InAllScenes:
				case Clients.Everywhere:
					return true;
				default:
					Debug.LogError($"Unrecognized {nameof(Clients)} value: {toClients}.", this);
					return false;
			}
		}

		#if UNITY_EDITOR
		NullGuardResult INullGuardByType.EvaluateNullGuard<TService>([AllowNull] Component client)
		{
			if(!AreAvailableToAnyClient())
			{
				if(!client)
				{
					return NullGuardResult.ErrorBox($"Service is only available to clients in '{ObjectNames.NicifyVariableName(toClients.ToString())}', but the client is null.");
				}

				if(!AreAvailableToClient(client.gameObject))
				{
					return NullGuardResult.ErrorBox($"Service is only available to clients in '{ObjectNames.NicifyVariableName(toClients.ToString())}'.");
				}
			}

			foreach(var definition in providesServices)
			{
				var definingType = definition.definingType.Value;
				if(definingType != typeof(TService))
				{
					continue;
				}

				var service = definition.service;
				if(!service)
				{
					return NullGuardResult.ErrorBox($"Service of type {TypeUtility.ToString(typeof(TService))} is missing.");
				}

				if(service is not TService && !ValueProviderUtility.IsValueProvider(service))
				{
					return NullGuardResult.ErrorBox($"Service {TypeUtility.ToString(service.GetType())} has defining type {TypeUtility.ToString(typeof(TService))} but is not assignable to it.");
				}

				return NullGuardResult.Passed;
			}

			return NullGuardResult.Error($"No service with defining type {TypeUtility.ToString(typeof(TService))} found.");
		}
		#endif
	}
}