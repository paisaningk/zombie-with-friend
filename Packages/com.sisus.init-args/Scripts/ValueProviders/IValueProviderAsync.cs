using System.Diagnostics.CodeAnalysis;
using System.Threading;
using UnityEngine;
using UnityEngine.Scripting;

namespace Sisus.Init
{
	/// <summary>
	/// Represents an object that can asynchronously retrieve a value of type <typeparamref name="TValue"/> for a client <see cref="Component"/>.
	/// <para>
	/// If a class derives from <see cref="Object"/> and implements <see cref="IValueProviderAsync{T}"/> then
	/// <see cref="Any{T}"/> can wrap an instance of this class and return its value when <see cref="Any{T}.GetValueAsync"/> is called.
	/// </para>
	/// </summary>
	/// <typeparam name="TValue"> Type of the provided value. </typeparam>
	/// <seealso cref="IValueProvider{TValue}"/>
	/// <seealso cref="IValueByTypeProvider"/>
	/// <seealso cref="IValueByTypeProviderAsync"/>
	[RequireImplementors]
	public interface IValueProviderAsync<TValue> : IValueProviderAsync
	{
		/// <summary>
		/// Gets the value of type <typeparamref name="TValue"/> for the <paramref name="client"/>.
		/// </summary>
		/// <param name="client">
		/// The component requesting the value, if request is coming from a component; otherwise, <see langword="null"/>.
		/// </param>
		/// <param name="cancellationToken">
		/// Token that can be used to cancel the asynchronous operation (e.g. if the client gets destroyed while waiting for the value).
		/// </param>
		/// <returns>
		/// <see cref="Awaitable{TValue}"/> that can be <see langword="await">awaited</see> to get the value of type <see cref="TValue"/>, if available;
		/// otherwise, a completed awaitable with the result of <see langword="null"/>.
		/// </returns>
		#if UNITY_2023_1_OR_NEWER
		new Awaitable<TValue>
		#else
		new System.Threading.Tasks.Task<TValue>
		#endif
		GetForAsync([AllowNull] Component client, CancellationToken cancellationToken = default);

		/// <summary>
		/// Gets a value indicating whether this value provider can provide a value of type
		/// <typeparamref name="TValue"/> for the <paramref name="client"/> at this time.
		/// </summary>
		/// <param name="client">
		/// The component requesting the value, if request is coming from a component; otherwise, <see langword="null"/>.
		/// </param>
		/// <returns>
		/// <see langword="true"/> if can provide a value for the client at this time; otherwise, <see langword="false"/>.
		/// </returns>
		bool HasValueFor(Component client) => this is not INullGuard nullGuard || nullGuard.EvaluateNullGuard(client).Type is NullGuardResultType.Passed;

		/// <summary>
		/// Gets the value of type <typeparamref name="TValue"/> for the <paramref name="client"/>.
		/// </summary>
		/// <param name="client">
		/// The component requesting the value, if request is coming from a component; otherwise, <see langword="null"/>.
		/// </param>
		/// <param name="cancellationToken">
		/// Token that can be used to cancel the asynchronous operation (e.g. if the client gets destroyed while waiting for the value).
		/// </param> 
		/// <returns>
		/// An object that can be <see langword="await">awaited</see> to get the value, if available;
		/// otherwise, a completed awaitable with the result of <see langword="default"/>.
		/// </returns>
		#if UNITY_2023_1_OR_NEWER
		async Awaitable<object>
		#else
		async System.Threading.Tasks.Task<object>
		#endif
			IValueProviderAsync.GetForAsync([AllowNull] Component client, CancellationToken cancellationToken) => await GetForAsync(client, cancellationToken);
	}

	/// <summary>
	/// Represents an object that can asynchronously retrieve a value for a client <see cref="Component"/>.
	/// <para>
	/// Base interface of <see cref="IValueProvider{TValue}"/>.
	/// </para>
	/// </summary>
	[RequireImplementors]
	public interface IValueProviderAsync
	{
		/// <summary>
		/// Gets the value provided by this object for the <paramref name="client"/>.
		/// </summary>
		/// <param name="client">
		/// The component requesting the value, if request is coming from a component; otherwise, <see langword="null"/>.
		/// </param>
		/// <param name="cancellationToken">
		/// Token that can be used to cancel the asynchronous operation (e.g. if the client gets destroyed while waiting for the value).
		/// </param> 
		/// <returns>
		/// An object that can be <see langword="await">awaited</see> to get the value, if available;
		/// otherwise, a completed awaitable with the result of <see langword="default"/>.
		/// </returns>
		#if UNITY_2023_1_OR_NEWER
		Awaitable<object>
		#else
		System.Threading.Tasks.Task<object>
		#endif
		GetForAsync([AllowNull] Component client, CancellationToken cancellationToken = default);
	}
}