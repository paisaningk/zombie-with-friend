using System.Diagnostics;
using Sisus.Init;
using UnityEngine;

namespace Init.Demo
{
	/// <summary>
	/// A <see cref="GameObject"/> with the <see cref="Killable"/> component can be <see cref="Kill">killed</see>,
	/// which causes it to be set <see cref="GameObject.activeSelf">inactive</see>.
	/// <para>
	/// <see cref="Kill"/> gets called if the <see cref="GameObject"/> collides with a trigger that has an <see cref="IDeadly"/>
	/// <see cref="Component"/> attached to it.
	/// </para>
	/// </summary>
	[AddComponentMenu("Initialization/Demo/Killable")]
	public sealed class Killable : MonoBehaviour<IEventTrigger>, IResettable
	{
		/// <summary>
		/// Event that is triggered after the player has been <see cref="Kill">killed</see>.
		/// </summary>
		private IEventTrigger onKilled;

		protected override void Init(IEventTrigger onKilled) => this.onKilled = onKilled;

		/// <inheritdoc/>
		void IResettable.ResetState() => gameObject.SetActive(true);

		/// <summary>
		/// Kills the player, setting the <see cref="GameObject"/> <see cref="GameObject.activeSelf">inactive</see>
		/// and invoking the <see cref="onKilled"/> event.
		/// <para>
		/// The player can be reset back to being alive using the <see cref="IResettable.ResetState"/> method.
		/// </para>
		/// </summary>
		public void Kill()
		{
			if(!gameObject.activeSelf)
			{
				return;
			}

			Log(name + " was killed!");

			gameObject.SetActive(false);

			onKilled.Trigger();
		}

		private void OnTriggerEnter(Collider other)
		{
			if(other.TryGetComponent(out IDeadly _))
			{
				Kill();
			}
		}

		[Conditional("DEBUG")]
		private static void Log(string message) => Service<ILogger>.Instance.Log("<size=21><color=red>" + message + "</color></size>\n");
	}
}