#if UNITY_2023_1_OR_NEWER
using UnityEngine;

namespace Sisus.Init.Internal
{
	/// <summary>
	/// Utility methods for <see cref="Awaitable"/> and <see cref="Awaitable{TResult}"/>.
	/// </summary>
	internal static class AwaitableUtility
	{
		/// <summary>
		/// Gets an <see cref="Awaitable{TResult}"/> that has already completed with the specified result.
		/// </summary>
		/// <typeparam name="TResult"> The type of the result returned by the awaitable. </typeparam>
		/// <param name="result"> The result to store into the completed awaitable. </param>
		/// <returns> A completed awaitable. </returns>
		public static Awaitable<TResult> FromResult<TResult>(TResult result) => Result<TResult>.From(result);

		static class Result<TResult>
		{
			private static readonly AwaitableCompletionSource<TResult> completionSource = new();

			internal static Awaitable<TResult> From(TResult result)
			{
				completionSource.SetResult(result);
				var awaitable = completionSource.Awaitable;
				completionSource.Reset();
				return awaitable;
			}
		}
	}
}
#endif