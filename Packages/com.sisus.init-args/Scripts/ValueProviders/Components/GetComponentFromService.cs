using System;
using System.Collections.Generic;
using System.Threading;
using Sisus.Init.Internal;
using UnityEngine;

namespace Sisus.Init.ValueProviders
{
	/// <summary>
	/// <para>
	/// Base class for value providers that return an object of the requested type
	/// attached to a service of type <typeparamref name="TService"/>.
	/// </para>
	/// <para>
	/// Can be used to retrieve an Init argument at runtime.
	/// </para>
	/// </summary>
	/// <typeparam name="TService">
	/// <para>
	/// The defining type of the service from which the component will be retrieved.
	/// </para>
	/// <para>
	/// This type must be an interface that the service implements, a base type that the service derives from,
	/// or the exact type of the service.
	/// </para>
	/// </typeparam>
	public abstract class GetComponentFromService<TService> : ScriptableObject, IValueByTypeProvider, IValueByTypeProviderAsync
	#if UNITY_EDITOR
	, INullGuardByType
	#endif
	{
		/// <summary>
		/// Gets an object of type <typeparamref name="TValue"/> attached to the <paramref name="client"/>.
		/// </summary>
		/// <typeparam name="TValue"> Type of object to find. </typeparam>
		/// <param name="client"> The <see cref="GameObject"/> to search. </param>
		/// <param name="value">
		/// When this method returns, contains an object <typeparamref name="TValue"/> if one was found; otherwise, the <see langword="null"/>. This parameter is passed uninitialized.
		/// </param>
		/// <returns>
		/// <see langword="true"/> if an object was found; otherwise, <see langword="false"/>.
		/// </returns>
		public bool TryGetFor<TValue>(Component client, out TValue value)
		{
			var service = client ? Service.GetFor<TService>(client) : Service.Get<TService>();
			return Find.In(service, out value);
		}

		public
		#if UNITY_2023_1_OR_NEWER
		Awaitable<TValue>
		#else
		System.Threading.Tasks.Task<TValue>
		#endif
		GetForAsync<TValue>(Component client, CancellationToken cancellationToken)
		{
			var completionSource = new
				#if UNITY_2023_1_OR_NEWER
				AwaitableCompletionSource<TValue>();
				#else
				System.Threading.Tasks.TaskCompletionSource<TValue>();
				#endif

			if(TryGetFor(client, out TValue value))
			{
				completionSource.SetResult(value);
			}
			else
			{
				ServiceChanged<TValue>.listeners += OnServiceChanged;
				void OnServiceChanged(Clients clients, TValue oldInstance, TValue newInstance)
				{
					ServiceChanged<TValue>.listeners -= OnServiceChanged;
					completionSource.SetResult(newInstance);
				}
			}

			return completionSource
				#if UNITY_2023_1_OR_NEWER
				.Awaitable;
				#else
				.Task;
				#endif
		}

		bool IValueByTypeProvider.IsValueTypeSupported(Type valueType) => Find.typesToFindableTypes.ContainsKey(valueType);
		IEnumerable<Type> IValueByTypeProvider.GetSupportedValueTypes() => Find.typesToFindableTypes.Keys;
		bool IValueByTypeProvider.HasValueFor<TValue>(Component client) => client ? Service.ExistsFor<TService>(client) : Service.Exists<TService>() && Find.typesToFindableTypes.ContainsKey(typeof(TValue));

		#if UNITY_EDITOR
		NullGuardResult INullGuardByType.EvaluateNullGuard<TValue>(Component client)
		{
			if(!Find.typesToFindableTypes.ContainsKey(typeof(TValue)))
			{
				return NullGuardResult.Error($"{GetType().Name} can only provide values that are attachable to GameObjects. {TypeUtility.ToString(typeof(TValue))} is not supported.");
			}

			if(client ? Service.ExistsFor<TService>(client) : Service.Exists<TService>())
			{
				return NullGuardResult.Passed;
			}

			foreach(var serviceTag in ServiceTag.GetAllEditorOnly(true))
			{
				if(serviceTag.DefiningType != typeof(TService) || !serviceTag.Service)
				{
					continue;
				}

				if(!serviceTag.gameObject.activeInHierarchy)
				{
					return NullGuardResult.Warning($"Local Service of type '{TypeUtility.ToString(typeof(TService))}' exists but is currently inactive.");
				}

				if(serviceTag.ToClients != Clients.Everywhere && (!client || serviceTag.IsAvailableToClient(client.gameObject)))
				{
					return NullGuardResult.Error($"Local Service of type {TypeUtility.ToString(typeof(TService))}, but is only available {ToString(serviceTag.ToClients, serviceTag)}.");
				}
			}

			foreach(var services in Services.GetAllEditorOnly(true))
			{
				foreach(var providedService in services.providesServices)
				{
					if(providedService.DefiningType != typeof(TService))
					{
						continue;
					}

					if(!services.gameObject.activeInHierarchy)
					{
						return NullGuardResult.Error($"Local Service of type {TypeUtility.ToString(typeof(TService))} is inactive.");
					}

					if(!providedService.service)
					{
						return NullGuardResult.Error($"Local Service of type {TypeUtility.ToString(typeof(TService))} is missing.");
					}

					if(services.toClients != Clients.Everywhere)
					{
						return NullGuardResult.Error($"Local Service of type {TypeUtility.ToString(typeof(TService))} exists, but is only available {ToString(services.toClients, services)}.");
					}
				}
			}

			if(client)
			{
				if(Application.isPlaying)
				{
					return NullGuardResult.Error($"No service of type '{typeof(TService)}' is available for {client.GetType().Name}.");
				}

				return NullGuardResult.Error($"No service of type '{typeof(TService)}' is available for {client.GetType().Name} at this time.\n\n" +
														  "One may still become available at runtime.");
			}

			if(Application.isPlaying)
			{
				return NullGuardResult.Error($"No service of type '{typeof(TService)}' is available Everywhere.");
			}

			return NullGuardResult.Error($"No service of type '{typeof(TService)}' is available Everywhere at this time.\n\n" +
														 "One may still become available at runtime.");

			static string ToString(Clients clients, Component context) => clients switch
			{
				Clients.InGameObject => $"in the GameObject \"{context.gameObject.name}\"",
				Clients.InChildren => $"in the GameObject \"{context.gameObject.name}\" and its children",
				Clients.InParents => $"in the GameObject \"{context.gameObject.name}\" and its parents",
				Clients.InHierarchyRootChildren => $"in the root GameObject \"{context.gameObject.transform.name}\" and its children",
				Clients.InScene => $"in the scene \"{context.gameObject.scene.name}\"",
				Clients.InAllScenes => "in scenes",
				Clients.Everywhere => "everywhere",
				_ => clients.ToString()
			};
		}
		#endif
	}
}