//#define SHOW_SERVICE_TAGS

using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Linq;
using Sisus.Init.Serialization;
using Sisus.Init.ValueProviders;
using UnityEngine;
#if UNITY_EDITOR
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEditorInternal;
using Sisus.Shared.EditorOnly;
#endif

namespace Sisus.Init.Internal
{
	[ExecuteAlways, AddComponentMenu(Hidden), DefaultExecutionOrder(ExecutionOrder.ServiceTag), Init(Enabled = false)]
	public sealed partial class ServiceTag : MonoBehaviour, IServiceProvider, IValueByTypeProvider, IValueProvider<Component>, IInitializable<Component, Clients, Type>
		#if UNITY_EDITOR
		, INullGuardByType
		#endif
	{
		#if UNITY_EDITOR
		private static readonly HashSet<ServiceTag> allEditorOnly = new();

		internal static IEnumerable<ServiceTag> GetAllEditorOnly(bool includeInactive = false)
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

		private const string Hidden = "";

		[SerializeField]
		private Component service;

		[SerializeField,
		Tooltip("Specifies which clients can use this service.\n\n" +
				"When set to " + nameof(Clients.InChildren) + ", only clients that are attached to this GameObject or its children (including nested children) can access this service.\n\n" +
				"When set to " + nameof(Clients.InScene) + ", only clients that are in the same scene can access this service.\n\n" +
				"When set to " + nameof(Clients.Everywhere) + ", all clients can access this service, regardless of their location in a scene, or whether they are a scene object at all.")]
		private Clients toClients = Clients.Everywhere;

		[SerializeField]
		private _Type definingType = new();

		/// <summary>
		/// Gets the defining type that clients should be able to use to retrieve
		/// an instance of the <see cref="Service"/>.
		/// </summary>
		internal Type DefiningType
		{
			get => definingType;

			set
			{
				if(definingType == value)
				{
					return;
				}

				ServiceUtility.RemoveServiceProvidedBy(this);

				definingType = value;

				Register();

				#if UNITY_ASSERTIONS || INIT_ARGS_SAFE_MODE
				if(value is null)
				{
					Debug.LogWarning($"ServiceTag on GameObject \"{name}\" was assigned a null defining type.", gameObject);
				}
				else if(service && !ServiceTagUtility.IsValidDefiningType(value, service))
				{
					Debug.LogWarning(value.IsInterface
						? $"Local Service {TypeUtility.ToString(service.GetType())} on \"{name}\" was assigned the defining type {TypeUtility.ToString(value)} which the service does not implement."
						: $"Local Service {TypeUtility.ToString(service.GetType())} on \"{name}\" was assigned the defining type {TypeUtility.ToString(value)} which the service does not derive from.", gameObject);
				}
				#endif
			}
		}

		/// <summary>
		/// Gets service instance that clients that depend on objects of type
		/// <see cref="DefiningType"/> should be able to recieve.
		/// </summary>
		internal Component Service
		{
			get => service;

			set
			{
				if(ReferenceEquals(value, service))
				{
					return;
				}

				ServiceUtility.RemoveServiceProvidedBy(this);

				service = value;
				
				#if DEBUG || INIT_ARGS_SAFE_MODE
				if(!value)
				{
					Debug.LogWarning($"ServiceTag on GameObject \"{name}\" was assigned a null target.", gameObject);
					return;
				}

				if(DefiningType is { } type && !ServiceTagUtility.IsValidDefiningType(type, value))
				{
					Debug.LogWarning($"ServiceTag on GameObject \"{name}\" was assigned an invalid {nameof(Service)} instance. {nameof(DefiningType)} value {TypeUtility.ToString(type)} is not assignable from the instance of type {TypeUtility.ToString(value.GetType())}.", gameObject);
					return;
				}
				#endif

				Register();
			}
		}

		/// <summary>
		/// Specifies which clients can receive services from this provider.
		/// <para>
		/// When set to <see cref="Clients.InChildren"/>, only clients that are attached to the same GameObject as this provider, or any of its children (including nested children), can access its services.
		/// </para>
		/// <para>
		/// When set to <see cref="Clients.InScene"/>, only clients that are in the same scene as this provider, can access its services.
		/// </para>
		/// <para>
		/// When set to <see cref="Clients.Everywhere"/>, all clients are allowed to access its services, regardless of their location in scenes hierarchies, or whether they are a scene object at all.
		/// </para>
		/// </summary>
		internal Clients ToClients
		{
			get => toClients;

			set
			{
				if(toClients == value)
				{
					return;
				}

				ServiceUtility.RemoveServiceProvidedBy(this);
				toClients = value;
				Register();
			}
		}

		Component IValueProvider<Component>.Value => toClients == Clients.Everywhere ? service : null;

		bool IValueProvider<Component>.TryGetFor(Component client, out Component value)
		{
			if(client ? IsAvailableToClient(client.gameObject) : IsAvailableToAnyClient())
			{
				value = service;
				return service;
			}
			
			value = null;
			return false;
		}

		#if UNITY_EDITOR
		NullGuardResult INullGuardByType.EvaluateNullGuard<TService>(Component client)
		{
			if(!service)
			{
				return NullGuardResult.Error("Service reference is missing");
			}

			if(definingType.Value is not { } serviceType)
			{
				if(definingType.TypeNameAndAssembly is { Length: > 0 } typeNameAndAssembly)
				{
					return NullGuardResult.Error($"Failed to deserialize service defining type: {typeNameAndAssembly}'");
				}

				return NullGuardResult.Error("Service defining Type has not been set.");
			}

			if(serviceType != typeof(TService))
			{
				return NullGuardResult.Error($"Requested type {TypeUtility.ToString(typeof(TService))} does not match service defining type {TypeUtility.ToString(serviceType)}.");
			}
			
			if(!IsAvailableToAnyClient())
			{
				return NullGuardResult.Error($"Service is only available to clients in '{ObjectNames.NicifyVariableName(toClients.ToString())}'.");
			}

			if(service is not TService && !ValueProviderUtility.IsValueProvider(service))
			{
				return NullGuardResult.Error($"Service reference is not assignable to '{TypeUtility.ToString(typeof(TService))}'.");
			}

			if(service is IWrapper wrapper)
			{
				if(wrapper.WrappedObject is { } wrappedObject and not TService)
				{
					return NullGuardResult.Error($"Wrapped object of type {TypeUtility.ToString(wrappedObject.GetType())} is not assignable to '{TypeUtility.ToString(typeof(TService))}'.");
				}

				return NullGuardResult.Passed;
			}

			if(service is IInitializer initializer)
			{
				if(initializer.Target is { } targetObject && targetObject)
				{
					if(!Find.In<TService>(targetObject, out _))
					{
						return NullGuardResult.Error($"{TypeUtility.ToString(initializer.GetType())} target is not assignable to '{TypeUtility.ToString(typeof(TService))}'.");
					}

					return NullGuardResult.Passed;
				}

				if(InitializerUtility.TryGetClientType(initializer.GetType(), out Type clientType) && !typeof(TService).IsAssignableFrom(clientType))
				{
					return NullGuardResult.Error($"{TypeUtility.ToString(initializer.GetType())} target {TypeUtility.ToString(clientType)} is not assignable to '{TypeUtility.ToString(typeof(TService))}'.");
				}

				return NullGuardResult.Passed;
			}

			if(!ValueProviderUtility.TryGetValueProviderValue(service, out object value))
			{
				return NullGuardResult.Error($"{TypeUtility.ToString(service.GetType())}' is not assignable to '{TypeUtility.ToString(typeof(TService))}' and is not a value provider.");
			}

			if(!Find.In<TService>(value, out _))
			{
				return NullGuardResult.Error($"'{TypeUtility.ToString(service.GetType())}' provided value {TypeUtility.ToString(value.GetType())} is not assignable to '{TypeUtility.ToString(typeof(TService))}'.");
			}

			return NullGuardResult.Passed;
		}
		#endif

		#if UNITY_EDITOR
		/// <summary>
		/// Makes a local service of type <paramref name="definingType"/> from the specified <paramref name="target"/> component
		/// by attaching the <see cref="ServiceTag"/> to it.
		/// </summary>
		/// <param name="target"> The component to make into a local service. </param>
		/// <param name="definingType"> The type that clients can use to receive the service. </param>
		/// <remarks>
		/// This method only exists in the Editor.
		/// </remarks>
		public static void Add(Component target, Type definingType)
		{
			var tag = Undo.AddComponent<ServiceTag>(target.gameObject);
			tag.hideFlags = HideFlags.HideInInspector;
			tag.DefiningType = definingType;
			tag.Service = target;
		}

		/// <summary>
		/// Unmakes the local service of type <paramref name="definingType"/> from the specified <paramref name="target"/> component
		/// by removing the corresponding <see cref="ServiceTag"/> from it.
		/// </summary>
		/// <remarks>
		/// This method only exists in the Editor.
		/// </remarks>
		public static bool Remove(Component target, Type definingType)
		{
			foreach(var serviceTag in target.gameObject.GetComponentsNonAlloc<ServiceTag>())
			{
				if(serviceTag.definingType.Value == definingType && serviceTag.service == target)
				{
					Undo.DestroyObjectImmediate(serviceTag);
					return true;
				}
			}

			return false;
		}
		#endif

		/// <inheritdoc cref="IServiceProvider.TryGet{TService}(out TService)"/>
		public bool TryGet<TService>(out TService service) => TryGetFor(null, out service);

		/// <inheritdoc cref="IServiceProvider.TryGetFor{TService}(Component, out TService)"/>
		public bool TryGetFor<TService>([AllowNull] Component client, out TService service)
		{
			if(definingType.Value == typeof(TService)
			&& this.service
			&& Find.In(this.service, out service)
			&& (IsAvailableToAnyClient() || (client && IsAvailableToClient(client.gameObject))))
			{
				return true;
			}

			service = default;
			return false;
		}

		bool IValueByTypeProvider.IsValueTypeSupported(Type valueType) => definingType.Value == valueType;
		IEnumerable<Type> IValueByTypeProvider.GetSupportedValueTypes() => definingType.Value is { } type ? new[] { type } : Array.Empty<Type>();

		private void OnEnable()
		{
			#if UNITY_EDITOR
			// Avoid duplicate services if they have already been registered via OnValidate.
			ServiceUtility.RemoveServiceProvidedBy(this);
			allEditorOnly.Add(this);
			#endif

			Register();
		}

		private void OnDisable()
		{
			#if UNITY_EDITOR
			EditorApplication.hierarchyChanged -= OnHierarchyChanged;
			#endif

			ServiceUtility.RemoveServiceProvidedBy(this);
		}

		#if UNITY_EDITOR
		private void OnDestroy() => allEditorOnly.Remove(this);
		#endif

		void IInitializable<Component, Clients, Type>.Init(Component service, Clients toClients, Type definingType)
		{
			this.service = service;
			this.toClients = toClients;
			this.definingType = definingType;

			#if UNITY_EDITOR
			hideFlags = HideFlags.HideInInspector;
			#endif
		}

		internal bool IsAvailableToAnyClient() => toClients == Clients.Everywhere;

		internal bool IsAvailableToClient([DisallowNull] GameObject client)
		{
			#if DEV_MODE
			Debug.Assert(client);
			Debug.Assert(this);
			#endif

			switch(toClients)
			{
				case Clients.InGameObject:
					return client == gameObject;
				case Clients.InChildren:
					var injectorTransform = transform;
					for(var parent = client.transform; parent != null; parent = parent.parent)
					{
						if(parent == injectorTransform)
						{
							return true;
						}
					}
					return false;
				case Clients.InParents:
					var clientTransform = client.transform;
					for(var parent = transform; parent; parent = parent.parent)
					{
						if(parent == clientTransform)
						{
							return true;
						}
					}
					return false;
				case Clients.InHierarchyRootChildren:
					return transform.root == client.transform.root;
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

		private void Register()
		{
			#if UNITY_EDITOR
			if(!service)
			{
				return;
			}

			if(definingType.Value is null)
			{
				return;
			}

			Clients registerForClients = toClients switch
			{
				Clients.InGameObject => toClients,
				Clients.InChildren => toClients,
				Clients.InParents => toClients,
				Clients.InHierarchyRootChildren => toClients,
				_ when PrefabUtility.IsPartOfPrefabAsset(this) => Clients.InHierarchyRootChildren,
				Clients.InScene => toClients,
				_ when PrefabStageUtility.GetPrefabStage(gameObject) => Clients.InScene,
				_ => toClients
			};

			ServiceUtility.AddFor(service, definingType, registerForClients, this);
			#else
			ServiceUtility.AddFor(service, definingType, toClients, this);
			#endif
		}

		#if UNITY_EDITOR
		private async void OnValidate()
		{
			await Until.UnitySafeContext();

			if(!this)
			{
				return;
			}

			allEditorOnly.Add(this);

			// In Edit Mode register all services that are loaded to memory for purposes of Null Argument Guard
			// analysis, and visibility in Service Debugger window, but in Play Mode only services that have
			// been instantiated into a scene and are active and enabled should be registered.
			if(Application.isPlaying)
			{
				return;
			}

			ServiceUtility.RemoveServiceProvidedBy(this);

			if(!service)
			{
				// Handle "Missing" / "Destroyed" service in particular; not unassigned.
				if(service is not null && service.GetHashCode() != 0)
				{
					var instancesOfServiceType = Find.All(service.GetType(), true);
					if(instancesOfServiceType.Where(instance => !ServiceTagUtility.HasServiceTag(instance)).SingleOrDefaultNoException() is Component instance)
					{
						#if DEV_MODE
						Debug.Log($"Moving Service tag {DefiningType.Name} of {instance.GetType().Name} from {name} to {instance.gameObject.name}...", instance.gameObject);
						#endif

						service = instance;
						ComponentUtility.CopyComponent(this);
						Undo.DestroyObjectImmediate(this);
						ComponentUtility.PasteComponentAsNew(instance.gameObject);
					}
					else
					{
						if(EditorUtility.scriptCompilationFailed)
						{
							#if DEV_MODE
							Debug.Log($"ServiceTag on GameObject \"{name}\" is missing its target, but won't remove it from the GameObject because there are compile errors.", gameObject);
							#endif
						}
						else
						{
							if(Array.IndexOf(Selection.gameObjects, gameObject) is not -1 || InspectorContents.IsBeingInspected(gameObject))
							{
								#if DEV_MODE
								Debug.LogWarning($"ServiceTag on GameObject \"{name}\" is missing its target. It was probably removed by the user. Removing the ServiceTag as well.", gameObject);
								#endif

								Undo.DestroyObjectImmediate(this);
							}
						}

						// Since the game object is being inspected and target has become missing, it's likely
						// that the user removed the component from the game object using the Inspector.
						// In this case silently remove the service tag as well.
					}

					// Ensure that data corruption does not occur if references are only temporarily missing due to script compilation having failed.
				}
				else if(EditorUtility.scriptCompilationFailed)
				{
					#if DEV_MODE
					Debug.Log($"ServiceTag on GameObject \"{name}\" is missing its target, but won't remove it from the GameObject because there are compile errors.", gameObject);
					#endif
				}
				else
				{
					#if DEV_MODE
					Debug.LogWarning($"ServiceTag on GameObject \"{name}\" is missing its target. Removing it from the GameObject.", gameObject);
					#endif

					Undo.DestroyObjectImmediate(this);
				}

				return;
			}

			if(DefiningType is not { } type)
			{
				if(EditorUtility.scriptCompilationFailed)
				{
					#if DEV_MODE
					Debug.Log($"ServiceTag on GameObject \"{name}\" is missing its target, but won't remove it from the GameObject because there are compile errors.", gameObject);
					#endif
					return;
				}

				#if DEV_MODE
				Debug.Log($"ServiceTag on GameObject \"{name}\" is missing its {nameof(DefiningType)}. Removing it from the GameObject.", gameObject);
				#endif
				Undo.DestroyObjectImmediate(this);
				return;
			}

			if(!ServiceTagUtility.IsValidDefiningType(type, service))
			{
				#if DEV_MODE
				Debug.Log($"ServiceTag on GameObject \"{name}\" has a {nameof(DefiningType)} that is not assignable from service of type {service.GetType()}. Removing it from the GameObject.", gameObject);
				#endif
				Undo.DestroyObjectImmediate(this);
				return;
			}

			if(service.gameObject != gameObject)
			{
				#if DEV_MODE
				Debug.Log($"Moving Service tag {type.Name} of {service.GetType().Name} from {name} to {service.gameObject.name}...", service.gameObject);
				#endif
				ComponentUtility.CopyComponent(this);
				ComponentUtility.PasteComponentAsNew(service.gameObject);
				Undo.DestroyObjectImmediate(this);
				return;
			}

			#if DEV_MODE && SHOW_SERVICE_TAGS
			const HideFlags setFlags = HideFlags.None;
			#else
			const HideFlags setFlags = HideFlags.HideInInspector;
			#endif

			if(hideFlags != setFlags)
			{
				hideFlags = setFlags;
				EditorUtility.SetDirty(this);
			}

			if(gameObject.scene.IsValid())
			{
				if(gameObject.activeInHierarchy)
				{
					Register();
				}

				EditorApplication.hierarchyChanged -= OnHierarchyChanged;
				EditorApplication.hierarchyChanged += OnHierarchyChanged;
			}
		}

		private void Validate()
		{
			if(!service)
			{
				// Handle "Missing" / "Destroyed" service in particular; not unassigned.
				if(service is not null && service.GetHashCode() != 0)
				{
					var instancesOfServiceType = Find.All(service.GetType(), true);

					if(instancesOfServiceType.Where(instance => !ServiceTagUtility.HasServiceTag(instance)).SingleOrDefaultNoException() is Component instance)
					{
						#if DEV_MODE
						Debug.Log($"Moving Service tag {DefiningType.Name} of {instance.GetType().Name} from {name} to {instance.gameObject.name}...", instance.gameObject);
						#endif

						service = instance;
						ComponentUtility.CopyComponent(this);
						Undo.DestroyObjectImmediate(this);
						ComponentUtility.PasteComponentAsNew(instance.gameObject);
					}
					else
					{
						if(EditorUtility.scriptCompilationFailed)
						{
							#if DEV_MODE
							Debug.Log($"ServiceTag on GameObject \"{name}\" is missing its target, but won't remove it from the GameObject because there are compile errors.", gameObject);
							#endif
						}
						else
						{
							if(Array.IndexOf(Selection.gameObjects, gameObject) is not -1 || InspectorContents.IsBeingInspected(gameObject))
							{
								#if DEV_MODE
								Debug.LogWarning($"ServiceTag on GameObject \"{name}\" is missing its target. It was probably removed by the user. Removing the ServiceTag as well.", gameObject);
								#endif

								Undo.DestroyObjectImmediate(this);
							}
						}

						// Since the game object is being inspected and target has become missing, it's likely
						// that the user removed the component from the game object using the Inspector.
						// In this case silently remove the service tag as well.
					}

					// Ensure that data corruption does not occur if references are only temporarily missing due to script compilation having failed.
				}
				else if(EditorUtility.scriptCompilationFailed)
				{
					#if DEV_MODE
					Debug.Log($"ServiceTag on GameObject \"{name}\" is missing its target, but won't remove it from the GameObject because there are compile errors.", gameObject);
					#endif
				}
				else
				{
					#if DEV_MODE
					Debug.LogWarning($"ServiceTag on GameObject \"{name}\" is missing its target. Removing it from the GameObject.", gameObject);
					#endif

					Undo.DestroyObjectImmediate(this);
				}

				return;
			}

			if(DefiningType is not { } definingType)
			{
				if(EditorUtility.scriptCompilationFailed)
				{
					#if DEV_MODE
					Debug.Log($"ServiceTag on GameObject \"{name}\" is missing its target, but won't remove it from the GameObject because there are compile errors.", gameObject);
					#endif
					return;
				}

				#if DEV_MODE
				Debug.Log($"ServiceTag on GameObject \"{name}\" is missing its {nameof(DefiningType)}. Removing it from the GameObject.", gameObject);
				#endif
				Undo.DestroyObjectImmediate(this);
				return;
			}

			if(!ServiceTagUtility.IsValidDefiningType(definingType, service))
			{
				#if DEV_MODE
				Debug.Log($"ServiceTag on GameObject \"{name}\" has a {nameof(DefiningType)} that is not assignable from service of type {service.GetType()}. Removing it from the GameObject.", gameObject);
				#endif
				Undo.DestroyObjectImmediate(this);
				return;
			}

			if(service.gameObject != gameObject)
			{
				#if DEV_MODE
				Debug.Log($"Moving Service tag {definingType.Name} of {service.GetType().Name} from {name} to {service.gameObject.name}...", service.gameObject);
				#endif
				ComponentUtility.CopyComponent(this);
				ComponentUtility.PasteComponentAsNew(service.gameObject);
				Undo.DestroyObjectImmediate(this);
				return;
			}

			#if DEV_MODE && SHOW_SERVICE_TAGS
			const HideFlags setFlags = HideFlags.None;
			#else
			const HideFlags setFlags = HideFlags.HideInInspector;
			#endif

			if(hideFlags != setFlags)
			{
				hideFlags = setFlags;
				EditorUtility.SetDirty(this);
			}

			if(gameObject.scene.IsValid() && !EditorOnly.ThreadSafe.Application.IsPlaying)
			{
				Register();

				EditorApplication.hierarchyChanged -= OnHierarchyChanged;
				EditorApplication.hierarchyChanged += OnHierarchyChanged;
			}
		}

		/// <summary>
		/// Handle:
		/// - Service was destroyed.
		/// - Service was dragged to another GameObject.
		/// </summary>
		private void OnHierarchyChanged()
		{
			if(!this)
			{
				EditorApplication.hierarchyChanged -= OnHierarchyChanged;
				return;
			}

			if(Application.isPlaying)
			{
				return;
			}

			if(!service)
			{
				// Handle "Missing" / "Destroyed" service in particular; not unassigned.
				if(service is not null && service.GetHashCode() != 0)
				{
					if(Find.All(service.GetType(), true)
						.Where(x => !ServiceTagUtility.HasServiceTag(x))
						.SingleOrDefaultNoException() is Component instance)
					{
						#if DEV_MODE
						Debug.Log($"Moving Service tag {DefiningType.Name} of {instance.GetType().Name} from {name} to {instance.gameObject.name}...", instance.gameObject);
						#endif

						service = instance;
						ComponentUtility.CopyComponent(this);
						Undo.DestroyObjectImmediate(this);
						ComponentUtility.PasteComponentAsNew(instance.gameObject);
					}
					else
					{
						if(EditorUtility.scriptCompilationFailed)
						{
							#if DEV_MODE
							Debug.Log($"ServiceTag on GameObject \"{name}\" is missing its target, but won't remove it from the GameObject because there are compile errors.", gameObject);
							#endif
						}
						else
						{
							if(Array.IndexOf(Selection.gameObjects, gameObject) is not -1 || InspectorContents.IsBeingInspected(gameObject))
							{
								#if DEV_MODE
								Debug.LogWarning($"ServiceTag on GameObject \"{name}\" is missing its target. It was probably removed by the user. Removing the ServiceTag as well.", gameObject);
								#endif

								Undo.DestroyObjectImmediate(this);
							}
						}

						// Since the game object is being inspected and target has become missing, it's likely
						// that the user removed the component from the game object using the Inspector.
						// In this case silently remove the service tag as well.
					}

					// Ensure that data corruption does not occur if references are only temporarily missing due to script compilation having failed.
				}
				else if(EditorUtility.scriptCompilationFailed)
				{
					#if DEV_MODE
					Debug.Log($"ServiceTag on GameObject \"{name}\" is missing its target, but won't remove it from the GameObject because there are compile errors.", gameObject);
					#endif
				}
				else
				{
					#if DEV_MODE
					Debug.LogWarning($"ServiceTag on GameObject \"{name}\" is missing its target. Removing it from the GameObject.", gameObject);
					#endif

					Undo.DestroyObjectImmediate(this);
				}

				return;
			}

			if(service.gameObject != gameObject)
			{
				#if DEV_MODE
				Debug.Log($"Moving Service Tag of type {TypeUtility.ToString(definingType.Value)} for {service.GetType().Name} from '{name}' to '{service.gameObject.name}'...", service.gameObject);
				#endif
				ComponentUtility.CopyComponent(this);
				ComponentUtility.PasteComponentAsNew(service.gameObject);
				Undo.DestroyObjectImmediate(this);
			}
		}
		#endif
	}
}