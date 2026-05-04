using Sisus.Init;
using UnityEngine;

namespace Init.Demo
{
	/// <summary>
	/// An object that can provide the <see cref="ITrackable"/> component of the <see cref="IPlayer"/>.
	/// </summary>
	[CreateAssetMenu]
    public sealed class PlayerTrackableProvider : ScriptableObject, IValueProvider<ITrackable>
	#if UNITY_EDITOR
	, INullGuard
	#endif
	{
		public ITrackable Value => Service.TryGet(out IPlayer player) ? player.Trackable : null;

		#if UNITY_EDITOR
		NullGuardResult INullGuard.EvaluateNullGuard(Component client) => NullGuardResult.Passed;
		#endif
	}
}