using System.Threading;
using System.Threading.Tasks;
using UnityEngine;

namespace Sisus.Init
{
	/// <summary>
	/// A base class for an initializer that is responsible for asynchronously initializing a service
	/// of type <typeparamref name="TService"/>, which itself depends on one other service.
	/// <para>
	/// The <see cref="ServiceAttribute"/> must be added to all classes that derive from this
	/// base class; otherwise the framework will not discover the initializer and the
	/// service will not get registered.
	/// </para>
	/// <para>
	/// The <see cref="ServiceAttribute"/> can also be used to specify additional details
	/// about the service, such as its <see cref="ServiceAttribute.definingType">defining type</see>.
	/// </para>
	/// <para>
	/// Adding the <see cref="ServiceAttribute"/> to a service initializer instead of the service
	/// class itself makes it possible to decouple the service class from the <see cref="ServiceAttribute"/>.
	/// If you want to keep your service classes as decoupled from Init(args) as possible,
	/// this is one tool at your disposable that can help with that.
	/// </para>
	/// </summary>
	/// <typeparam name="TService"> The concrete type of the initialized service. </typeparam>
	/// <typeparam name="TArgument"> Type of the other service which the initialized service depends on. </typeparam>
	/// <seealso cref="ServiceInitializer{TService}"/>
	public abstract class ServiceInitializerAsync<TService, TArgument> : IServiceInitializerAsync<TService, TArgument>
	{
		/// <inheritdoc/>
		async Task<object> IServiceInitializerAsync.InitTargetAsync(params object[] arguments)
		{
			#if DEBUG || INIT_ARGS_SAFE_MODE
			if(arguments is null) Debug.LogWarning($"{GetType().Name}.{nameof(InitTargetAsync)} was provided no services, when one was expected.");
			else if(arguments.Length != 2) Debug.LogWarning($"{GetType().Name}.{nameof(InitTargetAsync)} was provided {arguments.Length} services, when two were expected.");
			#endif

			return await InitTargetAsync((TArgument)arguments[0], (CancellationToken)arguments[1]);
		}

		/// <inheritdoc/>
		public abstract Task<TService> InitTargetAsync(TArgument argument, CancellationToken cancellationToken);
	}
}