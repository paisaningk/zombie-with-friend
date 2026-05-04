using Sisus.Init;
using UnityEngine;
using UnityEngine.UI;

namespace Init.Demo
{
	/// <summary>
	/// Class responsible for keeping track of the current score
	/// and displaying it in the UI.
	/// </summary>
	[AddComponentMenu("Initialization/Demo/Score")]
	[RequireComponent(typeof(Text))]
	// Hide the Init section in the inspector and disable Null Argument Guard
	// and Wait For Services behaviour using the [Init] attribute.
	// The ability to inject the Text dependency is only meant to be used when
	// this component is initialized using code (e.g. to accomodate easy testing),
	// not when it is attached to a GameObject using the Inspector in Edit Mode.
	[Init(Enabled = false)]
	public sealed class Score : MonoBehaviour<Text>, IResettable, ILog
	{
		[SerializeField, Tooltip("Text component that will display the current score.")]
		private Text text;

		private int score;

		/// <summary>
		/// Increases the displayed score by one.
		/// </summary>
		public void Increment()
		{
			score++;
			text.text = score.ToString();

			this.Log("Score: " + text.text);
		}

		/// <inheritdoc/>
		protected override void Init(Text text)
		{
			this.text = text;
		}

		/// <inheritdoc/>
		void IResettable.ResetState()
		{
			score = 0;
			text.text = "0";
		}
	}
}