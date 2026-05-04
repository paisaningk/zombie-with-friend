using Sisus.Init;
using UnityEngine;

namespace Init.Demo
{
	/// <summary>
	/// Class responsible for resetting all objects to their initial states on demand.
	/// </summary>
	[AddComponentMenu("Initialization/Demo/Reset Handler")]
	public sealed class ResetHandler : MonoBehaviour<IInputManager>
	{
		private readonly IInputManager inputManager = null;

		/// <inheritdoc/>
		protected override void Init(IInputManager inputManager)
		{
			this[nameof(inputManager)] = inputManager;
		}

		private void OnEnable()
		{
			inputManager.ResetInputGiven += OnResetInputGiven;
		}

		private void OnDisable()
		{
			if(inputManager != Null)
			{
				inputManager.ResetInputGiven -= OnResetInputGiven;
			}
		}

		private void OnResetInputGiven()
		{
			foreach(var resettable in Find.All<IResettable>(true))
			{
				resettable.ResetState();
			}
		}
	}
}
