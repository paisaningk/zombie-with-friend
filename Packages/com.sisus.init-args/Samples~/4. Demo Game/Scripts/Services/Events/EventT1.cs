#if DEBUG
using System.Text;
using static Sisus.NullExtensions;
#endif
using UnityEngine;
using UnityEngine.Events;

namespace Init.Demo
{
	/// <summary>
	/// A <see cref="ScriptableObject"/> asset that represents an event
	/// with an argument of type <typeparamref name="TArgument"/>.
	/// <para>
	/// Whenever the event is <see cref="Trigger">triggered</see> all methods
	/// that are listening for the event are invoked.
	/// </para>
	/// </summary>
	/// <typeparam name="TArgument">
	/// Type of the argument that gets passed to all listener methods when the event occurs.
	/// </typeparam>
	public abstract class Event<TArgument> : ScriptableObject, IEvent<TArgument>, IEventTrigger<TArgument>
	{
		protected const string CreateAssetMenuDirectory = "Init(args) Demo/Events/";

		private event UnityAction<TArgument> listeners = null;

		#if DEBUG
		[SerializeField]
		private bool debugTrigger = false;
		#endif

		/// <inheritdoc/>
		public void AddListener(UnityAction<TArgument> method) => listeners += method;

		/// <inheritdoc/>
		public void RemoveListener(UnityAction<TArgument> method) => listeners -= method;

		/// <inheritdoc/>
		public void Trigger(TArgument argument)
		{
			#if DEBUG
			if(debugTrigger) Debug.Log(GenerateOnTriggeredDebugMessage(argument), this);
			#endif

			listeners?.Invoke(argument);
		}

		#if DEBUG
		private string GenerateOnTriggeredDebugMessage(TArgument argument)
		{
			if(listeners is null)
			{
				return name + " Triggered with argument " + (argument == Null ? "null" : argument.ToString()) + " and no listeners.";
			}

			var invocationList = listeners.GetInvocationList();
			var sb = new StringBuilder();
			sb.Append(name);
			sb.Append(" Triggered with argument ");
			sb.Append(argument == Null ? "null" : argument.ToString());
			sb.Append(" and ");
			sb.Append(invocationList.Length);
			sb.Append(" listeners.");

			foreach(var invocation in invocationList)
			{
				if(invocation.Method is null)
				{
					continue;
				}

				sb.AppendLine();

				if(invocation.Method.DeclaringType != null)
				{
					sb.Append(invocation.Method.DeclaringType.Name);
					sb.Append(".");
				}

				sb.Append(invocation.Method.Name);
			}

			return sb.ToString();
		}
		#endif
	}
}