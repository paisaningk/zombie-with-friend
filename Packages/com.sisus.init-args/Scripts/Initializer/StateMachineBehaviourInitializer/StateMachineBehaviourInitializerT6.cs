using System.Threading.Tasks;
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
	/// A base class for a component that can be used to specify the six arguments used to
	/// initialize a state machine behaviour of type <typeparamref name="TStateMachineBehaviour"/>
	/// that implements <see cref="IInitializable{TFirstArgument, TSecondArgument, TThirdArgument, TFourthArgument, TFifthArgument, TSixthArgument}"/>.
	/// <para>
	/// The argument value can be assigned using the inspector and serialized as part of a scene or a prefab.
	/// </para>
	/// <para>
	/// The argument gets injected to the <typeparamref name="TStateMachineBehaviour">client</typeparamref> during the <see cref="Awake"/> event.
	/// </para>
	/// <para>
	/// The client receives the argument via the
	/// <see cref="IInitializable{TFirstArgument, TSecondArgument, TThirdArgument, TFourthArgument, TFifthArgument, TSixthArgument}.Init">Init</see>
	/// method where it can assign them to a member field or property.
	/// </para>
	/// <para>
	/// After the argument has been injected the initializer is removed from the <see cref="GameObject"/> that holds it.
	/// </para>
	/// </summary>
	/// <typeparam name="TStateMachineBehaviour"> Type of the initialized state machine behaviour client. </typeparam>
	/// <typeparam name="TFirstArgument"> Type of the first argument to pass to the client's Init function. </typeparam>
	/// <typeparam name="TSecondArgument"> Type of the second argument to pass to the client's Init function. </typeparam>
	/// <typeparam name="TThirdArgument"> Type of the third argument to pass to the client's Init function. </typeparam>
	/// <typeparam name="TFourthArgument"> Type of the fourth argument to pass to the client's Init function. </typeparam>
	/// <typeparam name="TFifthArgument"> Type of the fifth argument to pass to the client's Init function. </typeparam>
	/// <typeparam name="TSixthArgument"> Type of the sixth argument to pass to the client's Init function. </typeparam>
	public abstract class StateMachineBehaviourInitializer<TStateMachineBehaviour, TFirstArgument, TSecondArgument, TThirdArgument, TFourthArgument, TFifthArgument, TSixthArgument>
		: StateMachineBehaviourInitializerBase<TStateMachineBehaviour, TFirstArgument, TSecondArgument, TThirdArgument, TFourthArgument, TFifthArgument, TSixthArgument>
			where TStateMachineBehaviour : StateMachineBehaviour, IInitializable<TFirstArgument, TSecondArgument, TThirdArgument, TFourthArgument, TFifthArgument, TSixthArgument>
	{
		[SerializeField] private Any<TFirstArgument> firstArgument = default;
		[SerializeField] private Any<TSecondArgument> secondArgument = default;
		[SerializeField] private Any<TThirdArgument> thirdArgument = default;
		[SerializeField] private Any<TFourthArgument> fourthArgument = default;
		[SerializeField] private Any<TFifthArgument> fifthArgument = default;
		[SerializeField] private Any<TSixthArgument> sixthArgument = default;

		[SerializeField, HideInInspector] private Arguments disposeArgumentsOnDestroy = Arguments.None;
		[FormerlySerializedAs("asyncValueProviderArguments"),SerializeField, HideInInspector] private Arguments asyncArguments = Arguments.None;

		/// <inheritdoc/>
		protected override TFirstArgument FirstArgument { get => firstArgument.GetValue(this, Context.MainThread); set => firstArgument = value; }
		/// <inheritdoc/>
		protected override TSecondArgument SecondArgument { get => secondArgument.GetValue(this, Context.MainThread); set => secondArgument = value; }
		/// <inheritdoc/>
		protected override TThirdArgument ThirdArgument { get => thirdArgument.GetValue(this, Context.MainThread); set => thirdArgument = value; }
		/// <inheritdoc/>
		protected override TFourthArgument FourthArgument { get => fourthArgument.GetValue(this, Context.MainThread); set => fourthArgument = value; }
		/// <inheritdoc/>
		protected override TFifthArgument FifthArgument { get => fifthArgument.GetValue(this, Context.MainThread); set => fifthArgument = value; }
		/// <inheritdoc/>
		protected override TSixthArgument SixthArgument { get => sixthArgument.GetValue(this, Context.MainThread); set => sixthArgument = value; }

		protected override bool IsRemovedAfterTargetInitialized => disposeArgumentsOnDestroy == Arguments.None;
		private protected override bool IsAsync => asyncArguments != Arguments.None;

		private protected sealed override async ValueTask<TStateMachineBehaviour> InitTargetAsync(Animator target)
		{
			var cancellationToken = destroyCancellationToken;

			var firstAwaitable = this.firstArgument.GetValueAsync(this, cancellationToken: cancellationToken);
			var secondAwaitable = this.secondArgument.GetValueAsync(this, cancellationToken: cancellationToken);
			var thirdAwaitable = this.thirdArgument.GetValueAsync(this, cancellationToken: cancellationToken);
			var fourthAwaitable = this.fourthArgument.GetValueAsync(this, cancellationToken: cancellationToken);
			var fifthAwaitable = this.fifthArgument.GetValueAsync(this, cancellationToken: cancellationToken);
			var sixthAwaitable = this.sixthArgument.GetValueAsync(this, cancellationToken: cancellationToken);

			var firstArgument = await firstAwaitable;
			var secondArgument = await secondAwaitable;
			var thirdArgument = await thirdAwaitable;
			var fourthArgument = await fourthAwaitable;
			var fifthArgument = await fifthAwaitable;
			var sixthArgument = await sixthAwaitable;

			cancellationToken.ThrowIfCancellationRequested();

			#if DEBUG || INIT_ARGS_SAFE_MODE
			if(disposeArgumentsOnDestroy is not Arguments.None)
			{
				if(disposeArgumentsOnDestroy.HasFlag(Arguments.First)) OptimizeValueProviderNameForDebugging(this, this.firstArgument);
				if(disposeArgumentsOnDestroy.HasFlag(Arguments.Second)) OptimizeValueProviderNameForDebugging(this, this.secondArgument);
				if(disposeArgumentsOnDestroy.HasFlag(Arguments.Third)) OptimizeValueProviderNameForDebugging(this, this.thirdArgument);
				if(disposeArgumentsOnDestroy.HasFlag(Arguments.Fourth)) OptimizeValueProviderNameForDebugging(this, this.fourthArgument);
				if(disposeArgumentsOnDestroy.HasFlag(Arguments.Fifth)) OptimizeValueProviderNameForDebugging(this, this.fifthArgument);
				if(disposeArgumentsOnDestroy.HasFlag(Arguments.Sixth)) OptimizeValueProviderNameForDebugging(this, this.sixthArgument);
			}
			#endif

			#if DEBUG || INIT_ARGS_SAFE_MODE
			if(IsRuntimeNullGuardActive) ValidateArgumentsAtRuntime(firstArgument, secondArgument, thirdArgument, fourthArgument, fifthArgument, sixthArgument);
			#endif

			var behaviours = target.GetBehaviours<TStateMachineBehaviour>();
			int count = behaviours.Length;
			
			#if DEBUG || INIT_ARGS_SAFE_MODE
			if(count == 0) throw new MissingComponentException($"No {typeof(TStateMachineBehaviour).Name} was found in the Animator on '{name}'.", null);
			#endif

			for(int i = count - 1; i >= 0; i--)
			{
				behaviours[i].Init(firstArgument, secondArgument, thirdArgument, fourthArgument, fifthArgument, sixthArgument);
			}

			return behaviours[0];
		}

		private protected void OnDestroy()
		{
			if(disposeArgumentsOnDestroy is Arguments.None)
			{
				return;
			}

			HandleDisposeValue(this, disposeArgumentsOnDestroy, Arguments.First, ref firstArgument);
			HandleDisposeValue(this, disposeArgumentsOnDestroy, Arguments.Second, ref secondArgument);
			HandleDisposeValue(this, disposeArgumentsOnDestroy, Arguments.Third, ref thirdArgument);
			HandleDisposeValue(this, disposeArgumentsOnDestroy, Arguments.Fourth, ref fourthArgument);
			HandleDisposeValue(this, disposeArgumentsOnDestroy, Arguments.Fifth, ref fifthArgument);
			HandleDisposeValue(this, disposeArgumentsOnDestroy, Arguments.Sixth, ref sixthArgument);
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

		private protected sealed override void SetIsArgumentAsync(Arguments argument, bool isAsyncValueProvider)
		{
			var setValue = asyncArguments.WithFlag(argument, isAsyncValueProvider);
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
			EvaluateNullGuard(thirdArgument, failures);
			EvaluateNullGuard(fourthArgument, failures);
			EvaluateNullGuard(fifthArgument, failures);
			EvaluateNullGuard(sixthArgument, failures);
		}

		private protected override void OnValidate() => Validate(this, gameObject, firstArgument, secondArgument, thirdArgument, fourthArgument, fifthArgument, sixthArgument);
		#endif
	}
}