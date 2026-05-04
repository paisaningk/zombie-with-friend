using Sisus.Init;
using UnityEngine;

namespace Init.Demo
{
	/// <summary>
	/// Can be used in place of a <see cref="IMoveSettings"/> Init argument to
	/// inject a randomly picked <see cref="IMoveSettings"/> asset.
	/// </summary>
	public sealed class RandomMoveSettingsProvider : ScriptableObject, IValueProvider<IMoveSettings>
    {
        [SerializeField]
        private MoveSettings[] options = new MoveSettings[0];

		public IMoveSettings Value => options.Length == 0 ? null : options[Random.Range(0, options.Length)];
	}
}