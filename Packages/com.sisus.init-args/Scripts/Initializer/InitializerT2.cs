using Sisus.Init.Internal;
using UnityEngine;
using UnityEngine.Serialization;
using static Sisus.Init.Internal.InitializerUtility;
#if UNITY_EDITOR
using Sisus.Init.EditorOnly;
using System.Collections.Generic;
#endif

namespace Sisus.Init
{
	/// <summary>
	/// A base class for a component that can can specify the two arguments used to
	/// initialize a component that implements <see cref="IInitializable{TFirstArgument, TSecondArgument}"/>.
	/// <para>
	/// The arguments can be assigned using the inspector and are serialized as part of the client's scene or prefab asset.
	/// </para>
	/// <para>
	/// The arguments get injected to the <typeparamref name="TClient">client</typeparamref>
	/// during the Awake event.
	/// </para>
	/// <para>
	/// The client receives the arguments via the
	/// <see cref="IInitializable{TFirstArgument, TSecondArgument}.Init">Init</see>
	/// method where it can assign them to member fields or properties.
	/// </para>
	/// </summary>
	/// <typeparam name="TClient"> Type of the initialized client component. </typeparam>
	/// <typeparam name="TFirstArgument"> Type of the first argument to pass to the client component's Init function. </typeparam>
	/// <typeparam name="TSecondArgument"> Type of the second argument to pass to the client component's Init function. </typeparam>
	public abstract class Initializer<TClient, TFirstArgument, TSecondArgument> : InitializerBase<TClient, TFirstArgument, TSecondArgument> where TClient : MonoBehaviour, IInitializable<TFirstArgument, TSecondArgument>
	{
		[SerializeField] private Any<TFirstArgument> firstArgument = default;
		[SerializeField] private Any<TSecondArgument> secondArgument = default;

		[SerializeField, HideInInspector] private Arguments disposeArgumentsOnDestroy = Arguments.None;
		[FormerlySerializedAs("asyncValueProviderArguments"), SerializeField, HideInInspector] private Arguments asyncArguments = Arguments.None;

		protected override TFirstArgument FirstArgument { get => firstArgument.GetValue(this, Context.MainThread); set => firstArgument = value; }
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

			if(!this)
			{
				return initTargetResult;
			}

			#if DEBUG
			if(disposeArgumentsOnDestroy is not Arguments.None)
			{
				if(disposeArgumentsOnDestroy.HasFlag(Arguments.First)) OptimizeValueProviderNameForDebugging(this, this.firstArgument);
				if(disposeArgumentsOnDestroy.HasFlag(Arguments.Second)) OptimizeValueProviderNameForDebugging(this, this.secondArgument);
			}
			#endif

			#if DEBUG || INIT_ARGS_SAFE_MODE
			if(IsRuntimeNullGuardActive) ValidateArgumentsAtRuntime(firstArgument, secondArgument);
			#endif

			#if UNITY_EDITOR
			if(!target)
			#else
			if(target is null)
			#endif
			{
				gameObject.AddComponent(out TClient result, firstArgument, secondArgument);
				return result;
			}

			if(target.gameObject != gameObject)
			{
				#if UNITY_6000_0_OR_NEWER
				var results = await target.InstantiateAsync(firstArgument, secondArgument);
				return results[0];
				#else
				return target.Instantiate(firstArgument, secondArgument);
				#endif
			}

			if(target is MonoBehaviour<TFirstArgument, TSecondArgument> monoBehaviourT)
			{
				monoBehaviourT.InitInternal(firstArgument, secondArgument);
			}
			else
			{
				target.Init(firstArgument, secondArgument);
			}

			return target;
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

		private protected sealed override void Reset()
		{
			base.Reset();

			if(InitAttributeUtility.TryGet(GetType(), out var initAttribute)
				&& initAttribute.waitForServices is true
				&& ValueProviders.ValueProviderUtility.TryGetSingleSharedInstanceSlow(typeof(WaitForService), out var sharedInstance)
				&& sharedInstance is WaitForService waitForService)
			{
				HandleMakeWaitForService(this, ref firstArgument, waitForService);
				HandleMakeWaitForService(this, ref secondArgument, waitForService);
			}
		}
		#endif
	}
}