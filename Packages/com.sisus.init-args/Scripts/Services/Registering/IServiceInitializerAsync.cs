using System;
using System.Diagnostics.CodeAnalysis;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using UnityEngine;
using UnityEngine.Scripting;

namespace Sisus.Init
{
	/// <summary>
	/// Represents an asynchronous initializer for a global service registered using the <see cref="ServiceAttribute"/>.
	/// </summary>
	/// <remarks>
	/// Base interface for all generic <see cref="IServiceInitializerAsync{}"/> interfaces,
	/// which should be implemented by all asynchronous service initializer classes.
	/// </remarks>
	[RequireImplementors]
	public interface IServiceInitializerAsync
	{
		[return: NotNull] public Task<object> InitTargetAsync(params object[] arguments)
		{
			foreach(var interfaceType in GetType().GetInterfaces())
			{
				if(interfaceType.IsGenericType
					&& interfaceType.GetGenericArguments().Length == arguments.Length
					&& interfaceType.GetMethod(nameof(InitTargetAsync)) is { } initTargetAsyncMethod)
				{
					var task = (Task)initTargetAsyncMethod.Invoke(this, arguments);
					var taskCompletionSource = new TaskCompletionSource<object>();
					task.OnSuccess(t => taskCompletionSource.SetResult(t.GetType().GetProperty(nameof(Task<object>.Result)).GetValue(t)));
					#if DEV_MODE || DEBUG || INIT_ARGS_SAFE_MODE
					task.OnFailure(t => Debug.LogException(t.Exception));
					#endif
					return taskCompletionSource.Task;
				}
			}

			throw new InvalidProgramException($"{GetType().Name} implements the non-generic base interface {nameof(IServiceInitializerAsync)} but not the generic interface {nameof(IServiceInitializerAsync)}<{new string(Enumerable.Repeat(',', arguments.Length).ToArray())}> accepting {arguments.Length} arguments.");
		}
	}

	/// <summary>
	/// Represents an asynchronous initializer for a global service registered using the <see cref="ServiceAttribute"/>.
	/// </summary>
	/// <typeparam name="TService"> The concrete type of the initialized service. </typeparam>
	[RequireImplementors]
	public interface IServiceInitializerAsync<TService> : IServiceInitializerAsync
	{
		/// <summary>
		/// Initializes the global service registered using the <see cref="ServiceAttribute"/> asynchronously using another global service that it depends on.
		/// </summary>
		/// <param name="cancellationToken"> Token that can be used to cancel the asynchronous operation (e.g. if exited Play Mode before initialization completed). </param>
		/// <summary> <see cref="Task{TService}"/> that can be awaited to get the initialized service of type <see cref="TService"/> asynchronously. </summary>
		[return: NotNull] Task<TService> InitTargetAsync(CancellationToken cancellationToken = default);
		
	}

	/// <summary>
	/// Represents an asynchronous initializer for a global service registered using the <see cref="ServiceAttribute"/> which depends on two other global services.
	/// </summary>
	/// <typeparam name="TService"> The concrete type of the initialized service. </typeparam>
	/// <typeparam name="TArgument"> Defining type of another service which the service of type <typeparamref name="TService"/> depends on. </typeparam>
	[RequireImplementors]
	public interface IServiceInitializerAsync<TService, in TArgument> : IServiceInitializerAsync
	{
		/// <summary>
		/// Initializes the global service registered using the <see cref="ServiceAttribute"/> asynchronously using another global service that it depends on.
		/// </summary>
		/// <param name="argument"> First service used during initialization of the target service. </param>
		/// <param name="cancellationToken"> Token that can be used to cancel the asynchronous operation (e.g. if exited Play Mode before initialization completed). </param>
		/// <summary> <see cref="Task{TService}"/> that can be awaited to get the initialized service of type <see cref="TService"/> asynchronously. </summary>
		[return: NotNull]
		Task<TService> InitTargetAsync(TArgument argument, CancellationToken cancellationToken);
	}

	/// <summary>
	/// Represents an asynchronous initializer for a global service registered using the <see cref="ServiceAttribute"/> which depends on two other global services.
	/// </summary>
	/// <typeparam name="TService"> The concrete type of the initialized service. </typeparam>
	/// <typeparam name="TFirstArgument"> Defining type of the first service which the service of type <typeparamref name="TService"/> depends on. </typeparam>
	/// <typeparam name="TSecondArgument"> Defining type of the second service which the service of type <typeparamref name="TService"/> depends on. </typeparam>
	[RequireImplementors]
	public interface IServiceInitializerAsync<TService, in TFirstArgument, in TSecondArgument> : IServiceInitializerAsync
	{
		/// <summary>
		/// Initializes the global service registered using the <see cref="ServiceAttribute"/> asynchronously using two other global services that it depends on.
		/// </summary>
		/// <param name="firstArgument"> First service used during initialization of the target service. </param>
		/// <param name="secondArgument"> Second service used during initialization of the target service. </param>
		/// <param name="cancellationToken"> Token that can be used to cancel the asynchronous operation (e.g. if exited Play Mode before initialization completed). </param>
		/// <summary> <see cref="Task{TService}"/> that can be awaited to get the initialized service of type <see cref="TService"/> asynchronously. </summary>
		[return: NotNull]
		Task<TService> InitTargetAsync(TFirstArgument firstArgument, TSecondArgument secondArgument, CancellationToken cancellationToken);
	}

	/// <summary>
	/// Represents an asynchronous initializer for a global service registered using the <see cref="ServiceAttribute"/> which depends on three other global services.
	/// </summary>
	/// <typeparam name="TService"> The concrete type of the initialized service. </typeparam>
	/// <typeparam name="TFirstArgument"> Defining type of the first service which the service of type <typeparamref name="TService"/> depends on. </typeparam>
	/// <typeparam name="TSecondArgument"> Defining type of the second service which the service of type <typeparamref name="TService"/> depends on. </typeparam>
	/// <typeparam name="TThirdArgument"> Defining type of the third service which the service of type <typeparamref name="TService"/> depends on. </typeparam>
	[RequireImplementors]
	public interface IServiceInitializerAsync<TService, in TFirstArgument, in TSecondArgument, in TThirdArgument> : IServiceInitializerAsync
	{
		/// <summary>
		/// Initializes the global service registered using the <see cref="ServiceAttribute"/> asynchronously using three other global services that it depends on.
		/// </summary>
		/// <param name="firstArgument"> First service used during initialization of the target service. </param>
		/// <param name="secondArgument"> Second service used during initialization of the target service. </param>
		/// <param name="thirdArgument"> Third service used during initialization of the target service. </param>
		/// <param name="cancellationToken"> Token that can be used to cancel the asynchronous operation (e.g. if exited Play Mode before initialization completed). </param>
		/// <summary> <see cref="Task{TService}"/> that can be awaited to get the initialized service of type <see cref="TService"/> asynchronously. </summary>
		[return: NotNull]
		Task<TService> InitTargetAsync(TFirstArgument firstArgument, TSecondArgument secondArgument, TThirdArgument thirdArgument, CancellationToken cancellationToken);
	}

	/// <summary>
	/// Represents an asynchronous initializer for a global service registered using the <see cref="ServiceAttribute"/> which depends on four other global services.
	/// </summary>
	/// <typeparam name="TService"> The concrete type of the initialized service. </typeparam>
	/// <typeparam name="TFirstArgument"> Defining type of the first service which the service of type <typeparamref name="TService"/> depends on. </typeparam>
	/// <typeparam name="TSecondArgument"> Defining type of the second service which the service of type <typeparamref name="TService"/> depends on. </typeparam>
	/// <typeparam name="TThirdArgument"> Defining type of the third service which the service of type <typeparamref name="TService"/> depends on. </typeparam>
	/// <typeparam name="TFourthArgument"> Defining type of the fourth service which the service of type <typeparamref name="TService"/> depends on. </typeparam>
	[RequireImplementors]
	public interface IServiceInitializerAsync<TService, in TFirstArgument, in TSecondArgument, in TThirdArgument, in TFourthArgument> : IServiceInitializerAsync
	{
		/// <summary>
		/// Initializes the global service registered using the <see cref="ServiceAttribute"/> asynchronously using four other global services that it depends on.
		/// </summary>
		/// <param name="firstArgument"> First service used during initialization of the target service. </param>
		/// <param name="secondArgument"> Second service used during initialization of the target service. </param>
		/// <param name="thirdArgument"> Third service used during initialization of the target service. </param>
		/// <param name="fourthArgument"> Fourth service used during initialization of the target service. </param>
		/// <param name="cancellationToken"> Token that can be used to cancel the asynchronous operation (e.g. if exited Play Mode before initialization completed). </param>
		/// <summary> <see cref="Task{TService}"/> that can be awaited to get the initialized service of type <see cref="TService"/> asynchronously. </summary>
		[return: NotNull]
		Task<TService> InitTargetAsync(TFirstArgument firstArgument, TSecondArgument secondArgument, TThirdArgument thirdArgument, TFourthArgument fourthArgument, CancellationToken cancellationToken);
	}

	/// <summary>
	/// Represents an asynchronous initializer for a global service registered using the <see cref="ServiceAttribute"/> which depends on five other global services.
	/// </summary>
	/// <typeparam name="TService"> The concrete type of the initialized service. </typeparam>
	/// <typeparam name="TFirstArgument"> Defining type of the first service which the service of type <typeparamref name="TService"/> depends on. </typeparam>
	/// <typeparam name="TSecondArgument"> Defining type of the second service which the service of type <typeparamref name="TService"/> depends on. </typeparam>
	/// <typeparam name="TThirdArgument"> Defining type of the third service which the service of type <typeparamref name="TService"/> depends on. </typeparam>
	/// <typeparam name="TFourthArgument"> Defining type of the fourth service which the service of type <typeparamref name="TService"/> depends on. </typeparam>
	/// <typeparam name="TFifthArgument"> Defining type of the fifth service which the service of type <typeparamref name="TService"/> depends on. </typeparam>
	[RequireImplementors]
	public interface IServiceInitializerAsync<TService, in TFirstArgument, in TSecondArgument, in TThirdArgument, in TFourthArgument, in TFifthArgument> : IServiceInitializerAsync
	{
		/// <summary>
		/// Initializes the global service registered using the <see cref="ServiceAttribute"/> asynchronously using five other global services that it depends on.
		/// </summary>
		/// <param name="firstArgument"> First service used during initialization of the target service. </param>
		/// <param name="secondArgument"> Second service used during initialization of the target service. </param>
		/// <param name="thirdArgument"> Third service used during initialization of the target service. </param>
		/// <param name="fourthArgument"> Fourth service used during initialization of the target service. </param>
		/// <param name="fifthArgument"> Fifth service used during initialization of the target service. </param>
		/// <param name="cancellationToken"> Token that can be used to cancel the asynchronous operation (e.g. if exited Play Mode before initialization completed). </param>
		/// <summary> <see cref="Task{TService}"/> that can be awaited to get the initialized service of type <see cref="TService"/> asynchronously. </summary>
		[return: NotNull]
		Task<TService> InitTargetAsync(TFirstArgument firstArgument, TSecondArgument secondArgument, TThirdArgument thirdArgument, TFourthArgument fourthArgument, TFifthArgument fifthArgument, CancellationToken cancellationToken);
	}

	/// <summary>
	/// Represents an asynchronous initializer for a global service registered using the <see cref="ServiceAttribute"/> which depends on six other global services.
	/// </summary>
	/// <typeparam name="TService"> The concrete type of the initialized service. </typeparam>
	/// <typeparam name="TFirstArgument"> Defining type of the first service which the service of type <typeparamref name="TService"/> depends on. </typeparam>
	/// <typeparam name="TSecondArgument"> Defining type of the second service which the service of type <typeparamref name="TService"/> depends on. </typeparam>
	/// <typeparam name="TThirdArgument"> Defining type of the third service which the service of type <typeparamref name="TService"/> depends on. </typeparam>
	/// <typeparam name="TFourthArgument"> Defining type of the fourth service which the service of type <typeparamref name="TService"/> depends on. </typeparam>
	/// <typeparam name="TFifthArgument"> Defining type of the fifth service which the service of type <typeparamref name="TService"/> depends on. </typeparam>
	/// <typeparam name="TSixthArgument"> Defining type of the sixth service which the service of type <typeparamref name="TService"/> depends on. </typeparam>
	[RequireImplementors]
	public interface IServiceInitializerAsync<TService, in TFirstArgument, in TSecondArgument, in TThirdArgument, in TFourthArgument, in TFifthArgument, in TSixthArgument> : IServiceInitializerAsync
	{
		/// <summary>
		/// Initializes the global service registered using the <see cref="ServiceAttribute"/> asynchronously using six other global services that it depends on.
		/// </summary>
		/// <param name="firstArgument"> First service used during initialization of the target service. </param>
		/// <param name="secondArgument"> Second service used during initialization of the target service. </param>
		/// <param name="thirdArgument"> Third service used during initialization of the target service. </param>
		/// <param name="fourthArgument"> Fourth service used during initialization of the target service. </param>
		/// <param name="fifthArgument"> Fifth service used during initialization of the target service. </param>
		/// <param name="sixthArgument"> Sixth service used during initialization of the target service. </param>
		/// <param name="cancellationToken"> Token that can be used to cancel the asynchronous operation (e.g. if exited Play Mode before initialization completed). </param>
		/// <summary> <see cref="Task{TService}"/> that can be awaited to get the initialized service of type <see cref="TService"/> asynchronously. </summary>
		[return: NotNull]
		Task<TService> InitTargetAsync(TFirstArgument firstArgument, TSecondArgument secondArgument, TThirdArgument thirdArgument, TFourthArgument fourthArgument, TFifthArgument fifthArgument, TSixthArgument sixthArgument, CancellationToken cancellationToken);
	}

	/// <summary>
	/// Represents an asynchronous initializer for a global service registered using the <see cref="ServiceAttribute"/> which depends on seven other global services.
	/// </summary>
	/// <typeparam name="TService"> The concrete type of the initialized service. </typeparam>
	/// <typeparam name="TFirstArgument"> Defining type of the first service which the service of type <typeparamref name="TService"/> depends on. </typeparam>
	/// <typeparam name="TSecondArgument"> Defining type of the second service which the service of type <typeparamref name="TService"/> depends on. </typeparam>
	/// <typeparam name="TThirdArgument"> Defining type of the third service which the service of type <typeparamref name="TService"/> depends on. </typeparam>
	/// <typeparam name="TFourthArgument"> Defining type of the fourth service which the service of type <typeparamref name="TService"/> depends on. </typeparam>
	/// <typeparam name="TFifthArgument"> Defining type of the fifth service which the service of type <typeparamref name="TService"/> depends on. </typeparam>
	/// <typeparam name="TSixthArgument"> Defining type of the sixth service which the service of type <typeparamref name="TService"/> depends on. </typeparam>
	/// <typeparam name="TSeventhArgument"> Defining type of the seventh service which the service of type <typeparamref name="TService"/> depends on. </typeparam>
	[RequireImplementors]
	public interface IServiceInitializerAsync<TService, in TFirstArgument, in TSecondArgument, in TThirdArgument, in TFourthArgument, in TFifthArgument, in TSixthArgument, in TSeventhArgument> : IServiceInitializerAsync
	{
		/// <summary>
		/// Initializes the global service registered using the <see cref="ServiceAttribute"/> asynchronously using seven other global services that it depends on.
		/// </summary>
		/// <param name="firstArgument"> First service used during initialization of the target service. </param>
		/// <param name="secondArgument"> Second service used during initialization of the target service. </param>
		/// <param name="thirdArgument"> Third service used during initialization of the target service. </param>
		/// <param name="fourthArgument"> Fourth service used during initialization of the target service. </param>
		/// <param name="fifthArgument"> Fifth service used during initialization of the target service. </param>
		/// <param name="sixthArgument"> Sixth service used during initialization of the target service. </param>
		/// <param name="seventhArgument"> Seventh service used during initialization of the target service. </param>
		/// <param name="cancellationToken"> Token that can be used to cancel the asynchronous operation (e.g. if exited Play Mode before initialization completed). </param>
		/// <summary> <see cref="Task{TService}"/> that can be awaited to get the initialized service of type <see cref="TService"/> asynchronously. </summary>
		[return: NotNull]
		Task<TService> InitTargetAsync(TFirstArgument firstArgument, TSecondArgument secondArgument, TThirdArgument thirdArgument, TFourthArgument fourthArgument, TFifthArgument fifthArgument, TSixthArgument sixthArgument, TSeventhArgument seventhArgument, CancellationToken cancellationToken);
	}

	/// <summary>
	/// Represents an asynchronous initializer for a global service registered using the <see cref="ServiceAttribute"/> which depends on eight other global services.
	/// </summary>
	/// <typeparam name="TService"> The concrete type of the initialized service. </typeparam>
	/// <typeparam name="TFirstArgument"> Defining type of the first service which the service of type <typeparamref name="TService"/> depends on. </typeparam>
	/// <typeparam name="TSecondArgument"> Defining type of the second service which the service of type <typeparamref name="TService"/> depends on. </typeparam>
	/// <typeparam name="TThirdArgument"> Defining type of the third service which the service of type <typeparamref name="TService"/> depends on. </typeparam>
	/// <typeparam name="TFourthArgument"> Defining type of the fourth service which the service of type <typeparamref name="TService"/> depends on. </typeparam>
	/// <typeparam name="TFifthArgument"> Defining type of the fifth service which the service of type <typeparamref name="TService"/> depends on. </typeparam>
	/// <typeparam name="TSixthArgument"> Defining type of the sixth service which the service of type <typeparamref name="TService"/> depends on. </typeparam>
	/// <typeparam name="TSeventhArgument"> Defining type of the seventh service which the service of type <typeparamref name="TService"/> depends on. </typeparam>
	/// <typeparam name="TEighthArgument"> Defining type of the eighth service which the service of type <typeparamref name="TService"/> depends on. </typeparam>
	[RequireImplementors]
	public interface IServiceInitializerAsync<TService, in TFirstArgument, in TSecondArgument, in TThirdArgument, in TFourthArgument, in TFifthArgument, in TSixthArgument, in TSeventhArgument, in TEighthArgument> : IServiceInitializerAsync
	{
		/// <summary>
		/// Initializes the global service registered using the <see cref="ServiceAttribute"/> asynchronously using eight other global services that it depends on.
		/// </summary>
		/// <param name="firstArgument"> First service used during initialization of the target service. </param>
		/// <param name="secondArgument"> Second service used during initialization of the target service. </param>
		/// <param name="thirdArgument"> Third service used during initialization of the target service. </param>
		/// <param name="fourthArgument"> Fourth service used during initialization of the target service. </param>
		/// <param name="fifthArgument"> Fifth service used during initialization of the target service. </param>
		/// <param name="sixthArgument"> Sixth service used during initialization of the target service. </param>
		/// <param name="seventhArgument"> Seventh service used during initialization of the target service. </param>
		/// <param name="eighthArgument"> Eighth service used during initialization of the target service. </param>
		/// <param name="cancellationToken"> Token that can be used to cancel the asynchronous operation (e.g. if exited Play Mode before initialization completed). </param>
		/// <summary> <see cref="Task{TService}"/> that can be awaited to get the initialized service of type <see cref="TService"/> asynchronously. </summary>
		[return: NotNull]
		Task<TService> InitTargetAsync(TFirstArgument firstArgument, TSecondArgument secondArgument, TThirdArgument thirdArgument, TFourthArgument fourthArgument, TFifthArgument fifthArgument, TSixthArgument sixthArgument, TSeventhArgument seventhArgument, TEighthArgument eighthArgument, CancellationToken cancellationToken);
	}

	/// <summary>
	/// Represents an asynchronous initializer for a global service registered using the <see cref="ServiceAttribute"/> which depends on nine other global services.
	/// </summary>
	/// <typeparam name="TService"> The concrete type of the initialized service. </typeparam>
	/// <typeparam name="TFirstArgument"> Defining type of the first service which the service of type <typeparamref name="TService"/> depends on. </typeparam>
	/// <typeparam name="TSecondArgument"> Defining type of the second service which the service of type <typeparamref name="TService"/> depends on. </typeparam>
	/// <typeparam name="TThirdArgument"> Defining type of the third service which the service of type <typeparamref name="TService"/> depends on. </typeparam>
	/// <typeparam name="TFourthArgument"> Defining type of the fourth service which the service of type <typeparamref name="TService"/> depends on. </typeparam>
	/// <typeparam name="TFifthArgument"> Defining type of the fifth service which the service of type <typeparamref name="TService"/> depends on. </typeparam>
	/// <typeparam name="TSixthArgument"> Defining type of the sixth service which the service of type <typeparamref name="TService"/> depends on. </typeparam>
	/// <typeparam name="TSeventhArgument"> Defining type of the seventh service which the service of type <typeparamref name="TService"/> depends on. </typeparam>
	/// <typeparam name="TEighthArgument"> Defining type of the eighth service which the service of type <typeparamref name="TService"/> depends on. </typeparam>
	/// <typeparam name="TNinthArgument"> Defining type of the ninth service which the service of type <typeparamref name="TService"/> depends on. </typeparam>
	[RequireImplementors]
	public interface IServiceInitializerAsync<TService, in TFirstArgument, in TSecondArgument, in TThirdArgument, in TFourthArgument, in TFifthArgument, in TSixthArgument, in TSeventhArgument, in TEighthArgument, in TNinthArgument> : IServiceInitializerAsync
	{
		/// <summary>
		/// Initializes the global service registered using the <see cref="ServiceAttribute"/> asynchronously using nine other global services that it depends on.
		/// </summary>
		/// <param name="firstArgument"> First service used during initialization of the target service. </param>
		/// <param name="secondArgument"> Second service used during initialization of the target service. </param>
		/// <param name="thirdArgument"> Third service used during initialization of the target service. </param>
		/// <param name="fourthArgument"> Fourth service used during initialization of the target service. </param>
		/// <param name="fifthArgument"> Fifth service used during initialization of the target service. </param>
		/// <param name="sixthArgument"> Sixth service used during initialization of the target service. </param>
		/// <param name="seventhArgument"> Seventh service used during initialization of the target service. </param>
		/// <param name="eighthArgument"> Eighth service used during initialization of the target service. </param>
		/// <param name="ninthArgument"> Ninth service used during initialization of the target service. </param>
		/// <param name="cancellationToken"> Token that can be used to cancel the asynchronous operation (e.g. if exited Play Mode before initialization completed). </param>
		/// <summary> <see cref="Task{TService}"/> that can be awaited to get the initialized service of type <see cref="TService"/> asynchronously. </summary>
		[return: NotNull]
		Task<TService> InitTargetAsync(TFirstArgument firstArgument, TSecondArgument secondArgument, TThirdArgument thirdArgument, TFourthArgument fourthArgument, TFifthArgument fifthArgument, TSixthArgument sixthArgument, TSeventhArgument seventhArgument, TEighthArgument eighthArgument, TNinthArgument ninthArgument, CancellationToken cancellationToken);
	}

	/// <summary>
	/// Represents an asynchronous initializer for a global service registered using the <see cref="ServiceAttribute"/> which depends on ten other global services.
	/// </summary>
	/// <typeparam name="TService"> The concrete type of the initialized service. </typeparam>
	/// <typeparam name="TFirstArgument"> Defining type of the first service which the service of type <typeparamref name="TService"/> depends on. </typeparam>
	/// <typeparam name="TSecondArgument"> Defining type of the second service which the service of type <typeparamref name="TService"/> depends on. </typeparam>
	/// <typeparam name="TThirdArgument"> Defining type of the third service which the service of type <typeparamref name="TService"/> depends on. </typeparam>
	/// <typeparam name="TFourthArgument"> Defining type of the fourth service which the service of type <typeparamref name="TService"/> depends on. </typeparam>
	/// <typeparam name="TFifthArgument"> Defining type of the fifth service which the service of type <typeparamref name="TService"/> depends on. </typeparam>
	/// <typeparam name="TSixthArgument"> Defining type of the sixth service which the service of type <typeparamref name="TService"/> depends on. </typeparam>
	/// <typeparam name="TSeventhArgument"> Defining type of the seventh service which the service of type <typeparamref name="TService"/> depends on. </typeparam>
	/// <typeparam name="TEighthArgument"> Defining type of the eighth service which the service of type <typeparamref name="TService"/> depends on. </typeparam>
	/// <typeparam name="TNinthArgument"> Defining type of the ninth service which the service of type <typeparamref name="TService"/> depends on. </typeparam>
	/// <typeparam name="TTenthArgument"> Defining type of the tenth service which the service of type <typeparamref name="TService"/> depends on. </typeparam>
	[RequireImplementors]
	public interface IServiceInitializerAsync<TService, in TFirstArgument, in TSecondArgument, in TThirdArgument, in TFourthArgument, in TFifthArgument, in TSixthArgument, in TSeventhArgument, in TEighthArgument, in TNinthArgument, in TTenthArgument> : IServiceInitializerAsync
	{
		/// <summary>
		/// Initializes the global service registered using the <see cref="ServiceAttribute"/> asynchronously using ten other global services that it depends on.
		/// </summary>
		/// <param name="firstArgument"> First service used during initialization of the target service. </param>
		/// <param name="secondArgument"> Second service used during initialization of the target service. </param>
		/// <param name="thirdArgument"> Third service used during initialization of the target service. </param>
		/// <param name="fourthArgument"> Fourth service used during initialization of the target service. </param>
		/// <param name="fifthArgument"> Fifth service used during initialization of the target service. </param>
		/// <param name="sixthArgument"> Sixth service used during initialization of the target service. </param>
		/// <param name="seventhArgument"> Seventh service used during initialization of the target service. </param>
		/// <param name="eighthArgument"> Eighth service used during initialization of the target service. </param>
		/// <param name="ninthArgument"> Ninth service used during initialization of the target service. </param>
		/// <param name="tenthArgument"> Tenth service used during initialization of the target service. </param>
		/// <param name="cancellationToken"> Token that can be used to cancel the asynchronous operation (e.g. if exited Play Mode before initialization completed). </param>
		/// <summary> <see cref="Task{TService}"/> that can be awaited to get the initialized service of type <see cref="TService"/> asynchronously. </summary>
		[return: NotNull]
		Task<TService> InitTargetAsync(TFirstArgument firstArgument, TSecondArgument secondArgument, TThirdArgument thirdArgument, TFourthArgument fourthArgument, TFifthArgument fifthArgument, TSixthArgument sixthArgument, TSeventhArgument seventhArgument, TEighthArgument eighthArgument, TNinthArgument ninthArgument, TTenthArgument tenthArgument, CancellationToken cancellationToken);
	}

	/// <summary>
	/// Represents an asynchronous initializer for a global service registered using the <see cref="ServiceAttribute"/> which depends on eleven other global services.
	/// </summary>
	/// <typeparam name="TService"> The concrete type of the initialized service. </typeparam>
	/// <typeparam name="TFirstArgument"> Defining type of the first service which the service of type <typeparamref name="TService"/> depends on. </typeparam>
	/// <typeparam name="TSecondArgument"> Defining type of the second service which the service of type <typeparamref name="TService"/> depends on. </typeparam>
	/// <typeparam name="TThirdArgument"> Defining type of the third service which the service of type <typeparamref name="TService"/> depends on. </typeparam>
	/// <typeparam name="TFourthArgument"> Defining type of the fourth service which the service of type <typeparamref name="TService"/> depends on. </typeparam>
	/// <typeparam name="TFifthArgument"> Defining type of the fifth service which the service of type <typeparamref name="TService"/> depends on. </typeparam>
	/// <typeparam name="TSixthArgument"> Defining type of the sixth service which the service of type <typeparamref name="TService"/> depends on. </typeparam>
	/// <typeparam name="TSeventhArgument"> Defining type of the seventh service which the service of type <typeparamref name="TService"/> depends on. </typeparam>
	/// <typeparam name="TEighthArgument"> Defining type of the eighth service which the service of type <typeparamref name="TService"/> depends on. </typeparam>
	/// <typeparam name="TNinthArgument"> Defining type of the ninth service which the service of type <typeparamref name="TService"/> depends on. </typeparam>
	/// <typeparam name="TTenthArgument"> Defining type of the tenth service which the service of type <typeparamref name="TService"/> depends on. </typeparam>
	/// <typeparam name="TEleventhArgument"> Defining type of the eleventh service which the service of type <typeparamref name="TService"/> depends on. </typeparam>
	[RequireImplementors]
	public interface IServiceInitializerAsync<TService, in TFirstArgument, in TSecondArgument, in TThirdArgument, in TFourthArgument, in TFifthArgument, in TSixthArgument, in TSeventhArgument, in TEighthArgument, in TNinthArgument, in TTenthArgument, in TEleventhArgument> : IServiceInitializerAsync
	{
		/// <summary>
		/// Initializes the global service registered using the <see cref="ServiceAttribute"/> asynchronously using eleven other global services that it depends on.
		/// </summary>
		/// <param name="firstArgument"> First service used during initialization of the target service. </param>
		/// <param name="secondArgument"> Second service used during initialization of the target service. </param>
		/// <param name="thirdArgument"> Third service used during initialization of the target service. </param>
		/// <param name="fourthArgument"> Fourth service used during initialization of the target service. </param>
		/// <param name="fifthArgument"> Fifth service used during initialization of the target service. </param>
		/// <param name="sixthArgument"> Sixth service used during initialization of the target service. </param>
		/// <param name="seventhArgument"> Seventh service used during initialization of the target service. </param>
		/// <param name="eighthArgument"> Eighth service used during initialization of the target service. </param>
		/// <param name="ninthArgument"> Ninth service used during initialization of the target service. </param>
		/// <param name="tenthArgument"> Tenth service used during initialization of the target service. </param>
		/// <param name="eleventhArgument"> Eleventh service used during initialization of the target service. </param>
		/// <param name="cancellationToken"> Token that can be used to cancel the asynchronous operation (e.g. if exited Play Mode before initialization completed). </param>
		/// <summary> <see cref="Task{TService}"/> that can be awaited to get the initialized service of type <see cref="TService"/> asynchronously. </summary>
		[return: NotNull]
		Task<TService> InitTargetAsync(TFirstArgument firstArgument, TSecondArgument secondArgument, TThirdArgument thirdArgument, TFourthArgument fourthArgument, TFifthArgument fifthArgument, TSixthArgument sixthArgument, TSeventhArgument seventhArgument, TEighthArgument eighthArgument, TNinthArgument ninthArgument, TTenthArgument tenthArgument, TEleventhArgument eleventhArgument, CancellationToken cancellationToken);
	}

	/// <summary>
	/// Represents an asynchronous initializer for a global service registered using the <see cref="ServiceAttribute"/> which depends on twelve other global services.
	/// </summary>
	/// <typeparam name="TService"> The concrete type of the initialized service. </typeparam>
	/// <typeparam name="TFirstArgument"> Defining type of the first service which the service of type <typeparamref name="TService"/> depends on. </typeparam>
	/// <typeparam name="TSecondArgument"> Defining type of the second service which the service of type <typeparamref name="TService"/> depends on. </typeparam>
	/// <typeparam name="TThirdArgument"> Defining type of the third service which the service of type <typeparamref name="TService"/> depends on. </typeparam>
	/// <typeparam name="TFourthArgument"> Defining type of the fourth service which the service of type <typeparamref name="TService"/> depends on. </typeparam>
	/// <typeparam name="TFifthArgument"> Defining type of the fifth service which the service of type <typeparamref name="TService"/> depends on. </typeparam>
	/// <typeparam name="TSixthArgument"> Defining type of the sixth service which the service of type <typeparamref name="TService"/> depends on. </typeparam>
	/// <typeparam name="TSeventhArgument"> Defining type of the seventh service which the service of type <typeparamref name="TService"/> depends on. </typeparam>
	/// <typeparam name="TEighthArgument"> Defining type of the eighth service which the service of type <typeparamref name="TService"/> depends on. </typeparam>
	/// <typeparam name="TNinthArgument"> Defining type of the ninth service which the service of type <typeparamref name="TService"/> depends on. </typeparam>
	/// <typeparam name="TTenthArgument"> Defining type of the tenth service which the service of type <typeparamref name="TService"/> depends on. </typeparam>
	/// <typeparam name="TEleventhArgument"> Defining type of the eleventh service which the service of type <typeparamref name="TService"/> depends on. </typeparam>
	/// <typeparam name="TTwelfthArgument"> Defining type of the twelfth service which the service of type <typeparamref name="TService"/> depends on. </typeparam>
	[RequireImplementors]
	public interface IServiceInitializerAsync<TService, in TFirstArgument, in TSecondArgument, in TThirdArgument, in TFourthArgument, in TFifthArgument, in TSixthArgument, in TSeventhArgument, in TEighthArgument, in TNinthArgument, in TTenthArgument, in TEleventhArgument, in TTwelfthArgument> : IServiceInitializerAsync
	{
		/// <summary>
		/// Initializes the global service registered using the <see cref="ServiceAttribute"/> asynchronously using twelve other global services that it depends on.
		/// </summary>
		/// <param name="firstArgument"> First service used during initialization of the target service. </param>
		/// <param name="secondArgument"> Second service used during initialization of the target service. </param>
		/// <param name="thirdArgument"> Third service used during initialization of the target service. </param>
		/// <param name="fourthArgument"> Fourth service used during initialization of the target service. </param>
		/// <param name="fifthArgument"> Fifth service used during initialization of the target service. </param>
		/// <param name="sixthArgument"> Sixth service used during initialization of the target service. </param>
		/// <param name="seventhArgument"> Seventh service used during initialization of the target service. </param>
		/// <param name="eighthArgument"> Eighth service used during initialization of the target service. </param>
		/// <param name="ninthArgument"> Ninth service used during initialization of the target service. </param>
		/// <param name="tenthArgument"> Tenth service used during initialization of the target service. </param>
		/// <param name="eleventhArgument"> Eleventh service used during initialization of the target service. </param>
		/// <param name="twelfthArgument"> Twelfth service used during initialization of the target service. </param>
		/// <param name="cancellationToken"> Token that can be used to cancel the asynchronous operation (e.g. if exited Play Mode before initialization completed). </param>
		/// <summary> <see cref="Task{TService}"/> that can be awaited to get the initialized service of type <see cref="TService"/> asynchronously. </summary>
		[return: NotNull]
		Task<TService> InitTargetAsync(TFirstArgument firstArgument, TSecondArgument secondArgument, TThirdArgument thirdArgument, TFourthArgument fourthArgument, TFifthArgument fifthArgument, TSixthArgument sixthArgument, TSeventhArgument seventhArgument, TEighthArgument eighthArgument, TNinthArgument ninthArgument, TTenthArgument tenthArgument, TEleventhArgument eleventhArgument, TTwelfthArgument twelfthArgument, CancellationToken cancellationToken);
	}
}