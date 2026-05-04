using System;
using UnityEngine;
using UnityEngine.Events;

namespace Init.Demo
{
	/// <summary>
	/// Represents a manager responsible for detecting changes in user input
	/// and broadcasting events in reponse.
	/// </summary>
	public interface IInputManager
	{
		/// <summary>
		/// Event broadcast when move input given by the user has changed.
		/// </summary>
		event UnityAction<Vector2> MoveInputChanged;

		/// <summary>
		/// Event broadcast when reset input has been given by the user.
		/// </summary>
		event Action ResetInputGiven;
	}
}