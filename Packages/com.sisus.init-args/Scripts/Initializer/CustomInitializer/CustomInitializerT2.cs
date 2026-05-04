using Sisus.Init.Internal;
using UnityEngine;
using UnityEngine.Serialization;
using static Sisus.Init.Internal.InitializerUtility;
#if UNITY_EDITOR
using System.Collections.Generic;
#endif

namespace Sisus.Init
{
	/// <summary>
	/// A base class for a component that can specify the two arguments used to initialize an object of type <typeparamref name="TClient"/>.
	/// <para>
	/// The arguments can be assigned using the inspector and are serialized as part of the client's scene or prefab asset.
	/// </para>
	/// <para>
	/// The <typeparamref name="TClient">client</typeparamref> does not need to implement the
	/// <see cref="IInitializable{TFirstArgument, TSecondArgument}"/> interface.
	/// The initialization arguments can instead be injected, for example, directly into properties with public setters.
	/// </para>
	/// <para>
	/// After the arguments have been injected the initializer is removed from the <see cref="GameObject"/> that holds it.
	/// </para>
	/// </summary>
	/// <typeparam name="TClient"> Type of the initialized client component. </typeparam>
	/// <typeparam name="TFirstArgument"> Type of the first argument to inject to the client. </typeparam>
	/// <typeparam name="TSecondArgument"> Type of the second argument to inject to the client. </typeparam>
	public abstract class CustomInitializer<TClient, TFirstArgument, TSecondArgument>
		: CustomInitializerBase<TClient, TFirstArgument, TSecondArgument>
			where TClient : Component
	{
		[SerializeField] private Any<TFirstArgument> firstArgument = default;
		[SerializeField] private Any<TSecondArgument> secondArgument = default;

		[SerializeField, HideInInspector] private Arguments disposeArgumentsOnDestroy = Arguments.None;
		[FormerlySerializedAs("asyncValueProviderArguments"),SerializeField, HideInInspector] private Arguments asyncArguments = Arguments.None;

		/// <inheritdoc/>
		protected override TFirstArgument FirstArgument { get => firstArgument.GetValue(this, Context.MainThread); set => firstArgument = value; }
		/// <inheritdoc/>
		protected override TSecondArgument SecondArgument { get => secondArgument.GetValue(this, Context.MainThread); set => secondArgument = value; }

		protected override bool IsRemovedAfterTargetInitialized => disposeArgumentsOnDestroy == Arguments.None;
		private protected override bool IsAsync => asyncArguments != Arguments.None;

		private protected sealed override async
		#if UNITY_2023_1_OR_NEWER
		Awaitable<TClient>
		#else
		System.Threading.Tasks.Task<TClient>
		#endif
		InitTargetAsync(TClient target)
		{
			var cancellationToken = destroyCancellationToken;

			var firstAwaitable = this.firstArgument.GetValueAsync(this, cancellationToken: cancellationToken);
			var secondAwaitable = this.secondArgument.GetValueAsync(this, cancellationToken: cancellationToken);

			var firstArgument = await firstAwaitable;
			var secondArgument = await secondAwaitable;

			cancellationToken.ThrowIfCancellationRequested();

			#if DEBUG
			if(disposeArgumentsOnDestroy is not Arguments.None)
			{
				OptimizeValueProviderNameForDebugging(this, this.firstArgument);
				OptimizeValueProviderNameForDebugging(this, this.secondArgument);
			}
			#endif

			#if DEBUG || INIT_ARGS_SAFE_MODE
			if(IsRuntimeNullGuardActive) ValidateArgumentsAtRuntime(firstArgument, secondArgument);
			#endif
			
			TClient result;
			#if UNITY_EDITOR
			if(!target)
			#else
			if(target is null)
			#endif
			{
				result = gameObject.AddComponent<TClient>();
			}
			else if(target.gameObject != gameObject)
			{
				result = Instantiate(target);
			}
			else
			{
				result = target;
			}

			InitTarget(result, firstArgument, secondArgument);
			return result;
		}

		private protected void OnDestroy()
		{
			if(disposeArgumentsOnDestroy is Arguments.None)
			{
				return;
			}

			HandleDisposeValue(this, disposeArgumentsOnDestroy, Arguments.First, ref firstArgument);
			HandleDisposeValue(this, disposeArgumentsOnDestroy, Arguments.Second, ref secondArgument);
		}

		#if UNITY_EDITOR
		private protected sealed override void SetReleaseArgumentOnDestroy(Arguments argument, bool shouldRelease)
		{
			var setValue = disposeArgumentsOnDestroy.WithFlag(argument, shouldRelease);
			if(disposeArgumentsOnDestroy != setValue)
			{
				disposeArgumentsOnDestroy = setValue;
				UnityEditor.EditorUtility.SetDirty(this);
			}
		}

		private protected sealed override void SetIsArgumentAsync(Arguments argument, bool isAsync)
		{
			var setValue = asyncArguments.WithFlag(argument, isAsync);
			if(asyncArguments != setValue)
			{
				asyncArguments = setValue;
				UnityEditor.EditorUtility.SetDirty(this);
			}
		}

		private protected override void EvaluateNullGuard(List<NullGuardResult> failures)
		{
			EvaluateInitStateNullGuard(failures);
			EvaluateNullGuard(firstArgument, failures);
			EvaluateNullGuard(secondArgument, failures);
		}

		private protected override void OnValidate() => Validate(this, gameObject, firstArgument, secondArgument);
		#endif
	}
}