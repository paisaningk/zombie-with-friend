namespace Sisus.Init
{
	using System;
	using System.Threading;
	using System.Threading.Tasks;

	/// <summary>
	/// Utility methods related to <see cref="Task"/>.
	/// </summary>
	internal static class Scheduler
	{
		/// <summary>
		/// Gets the TaskScheduler from the current synchronization context, if one exists; otherwise, gets the <see cref="TaskScheduler.Current">current TaskScheduler</see>.
		/// <para>
		/// In Unity platforms, this is the UnitySynchronizationContext.
		/// </para>
		/// <remarks>
		/// This should be passed to <see cref="Task.ContinueWith(System.Action{Task}, TaskScheduler)"/> to have WebGL support.
		/// </remarks>
		/// </summary>
		public static readonly TaskScheduler FromSynchronizationContextOrDefault;

		static Scheduler()
		{
			if (SynchronizationContext.Current is not null)
			{
				try
				{
					FromSynchronizationContextOrDefault = TaskScheduler.FromCurrentSynchronizationContext();
				}
				catch (InvalidOperationException) // Handle "The current SynchronizationContext may not be used as a TaskScheduler".
				{
					FromSynchronizationContextOrDefault = TaskScheduler.Current;
				}
			}
			else
			{
				FromSynchronizationContextOrDefault = TaskScheduler.Current;
			}
		}
	}
}
