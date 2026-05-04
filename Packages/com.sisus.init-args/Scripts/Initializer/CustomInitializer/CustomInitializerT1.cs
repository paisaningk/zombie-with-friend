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
	/// A base class for a component that can specify the argument used to initialize an object of type <typeparamref name="TClient"/>.
	/// <para>
	/// The argument can be assigned using the inspector and is serialized as part of the client's scene or prefab asset.
	/// </para>
	/// <para>
	/// The <typeparamref name="TClient">client</typeparamref> does not need to implement the <see cref="IInitializable{TArgument}"/> interface.
	/// The initialization argument can instead be injected, for example, directly into a property with a public setter.
	/// </para>
	/// <para>
	/// After the arguments have been injected the initializer is removed from the <see cref="GameObject"/> that holds it.
	/// </para>
	/// </summary>
	/// <typeparam name="TClient"> Type of the initialized client component. </typeparam>
	/// <typeparam name="TArgument"> Type of the argument to inject to the client. </typeparam>
	public abstract class CustomInitializer<TClient, TArgument> : CustomInitializerBase<TClient, TArgument> where TClient : Component
	{
		[SerializeField] private protected Any<TArgument> argument = default;

		[SerializeField, HideInInspector] private Arguments disposeArgumentsOnDestroy = Arguments.None;
		[FormerlySerializedAs("asyncValueProviderArguments"),SerializeField, HideInInspector] private Arguments asyncArguments = Arguments.None;

		/// <inheritdoc/>
		protected override TArgument Argument { get => argument.GetValue(this, Context.MainThread); set => argument = value; }

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

			var argument = await this.argument.GetValueAsync(this, cancellationToken: cancellationToken);

			cancellationToken.ThrowIfCancellationRequested();

			#if DEBUG
			if(disposeArgumentsOnDestroy is Arguments.First) OptimizeValueProviderNameForDebugging(this, this.argument);
			#endif

			#if DEBUG || INIT_ARGS_SAFE_MODE
			if(IsRuntimeNullGuardActive) ValidateArgumentAtRuntime(argument);
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

			InitTarget(result, argument);
			return result;
		}

		private protected void OnDestroy()
		{
			if(disposeArgumentsOnDestroy is Arguments.First)
			{
				HandleDisposeValue(this, disposeArgumentsOnDestroy, Arguments.First, ref argument);
			}
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
			EvaluateNullGuard(argument, failures);
		}

		private protected override void OnValidate() => Validate(this, gameObject, argument);
		#endif
	}
}