using System;
using System.Diagnostics.CodeAnalysis;
using System.Threading;
using System.Threading.Tasks;

namespace Sisus.Init
{
	/// <summary>
	/// Extension methods for <see cref="Task"/>.
	/// </summary>
	internal static class TaskExtensions
	{
		/// <summary>
		/// Creates a continuation that executes asynchronously when the target Task completes.
		/// <remarks>
		/// Same as using <see cref="Task.ContinueWith(Action{Task}, CancellationToken)"/>, except that it uses the task scheduler
		/// from the current synchronization context when available (to have WebGL support).
		/// </remarks>>
		/// </summary>
		/// <param name="task"> Task whose completion to wait for. </param>
		/// <param name="action"> Delegate to execute when the task completes. </param>
		/// <param name="cancellationToken"> Token for canceling the continuation action. </param>
		public static void OnSuccess([DisallowNull] this Task task, [DisallowNull] Action action, CancellationToken cancellationToken = default)
			=> task.ContinueWith(_ => action(), cancellationToken, TaskContinuationOptions.OnlyOnRanToCompletion, Scheduler.FromSynchronizationContextOrDefault);

		/// <summary>
		/// Creates a continuation that executes asynchronously when the target Task completes.
		/// <remarks>
		/// Same as using <see cref="Task.ContinueWith(Action{Task}, CancellationToken)"/>, except that it uses the task scheduler
		/// from the current synchronization context when available (to have WebGL support).
		/// </remarks>>
		/// </summary>
		/// <param name="task"> Task whose completion to wait for. </param>
		/// <param name="action"> Delegate to execute when the task completes. </param>
		/// <param name="cancellationToken"> Token for canceling the continuation action. </param>
		public static void OnSuccess([DisallowNull] this Task task, [DisallowNull] Action<Task> action, CancellationToken cancellationToken = default)
			=> task.ContinueWith(action, cancellationToken, TaskContinuationOptions.OnlyOnRanToCompletion, Scheduler.FromSynchronizationContextOrDefault);

		/// <summary>
		/// Creates a continuation that executes asynchronously when the target Task completes.
		/// <remarks>
		/// Same as using <see cref="Task.ContinueWith(Action{Task}, CancellationToken)"/>, except that it uses the task scheduler
		/// from the current synchronization context when available (to have WebGL support).
		/// </remarks>>
		/// </summary>
		/// <param name="task"> Task whose completion to wait for. </param>
		/// <param name="action"> Delegate to execute when the task completes. </param>
		/// <param name="cancellationToken"> Token for canceling the continuation action. </param>
		public static void OnFailure([DisallowNull] this Task task, [DisallowNull] Action action, CancellationToken cancellationToken = default)
			=> task.ContinueWith(_ => action(), cancellationToken, TaskContinuationOptions.OnlyOnFaulted, Scheduler.FromSynchronizationContextOrDefault);

		/// <summary>
		/// Creates a continuation that executes asynchronously when the target Task completes.
		/// <remarks>
		/// Same as using <see cref="Task.ContinueWith(Action{Task}, CancellationToken)"/>, except that it uses the task scheduler
		/// from the current synchronization context when available (to have WebGL support).
		/// </remarks>>
		/// </summary>
		/// <param name="task"> Task whose completion to wait for. </param>
		/// <param name="action"> Delegate to execute when the task completes. </param>
		/// <param name="cancellationToken"> Token for canceling the continuation action. </param>
		public static void OnFailure([DisallowNull] this Task task, [DisallowNull] Action<Task> action, CancellationToken cancellationToken = default)
			=> task.ContinueWith(action, cancellationToken, TaskContinuationOptions.OnlyOnFaulted, Scheduler.FromSynchronizationContextOrDefault);

		/// <summary>
		/// Creates a continuation that executes asynchronously when the target ValueTask completes.
		/// <remarks>
		/// Same as using <see cref="ValueTask.AsTask()"/> and <see cref="Task.ContinueWith(Action{Task}, CancellationToken)"/>, except that it uses the task scheduler
		/// from the current synchronization context when available (to have WebGL support).
		/// </remarks>>
		/// </summary>
		/// <param name="valueTask"> ValueTask whose completion to wait for. </param>
		/// <param name="action"> Delegate to execute when the task completes. </param>
		/// <param name="cancellationToken"> Token for canceling the continuation action. </param>
		public static void OnFailure([DisallowNull] this ValueTask valueTask, [DisallowNull] Action<Task> action, CancellationToken cancellationToken = default)
			=> valueTask.AsTask().ContinueWith(action, cancellationToken, TaskContinuationOptions.OnlyOnFaulted, Scheduler.FromSynchronizationContextOrDefault);
	}
}