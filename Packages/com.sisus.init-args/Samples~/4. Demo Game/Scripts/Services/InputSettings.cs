using Sisus.Init;
using UnityEngine;

namespace Init.Demo
{
    /// <summary>
    /// SciptableObject asset that holds settings data for user inputs
    /// that the player can use to control the game.
    /// </summary>
    [Service(typeof(IInputSettings), typeof(IResetInputSettings), typeof(IMoveInputSettings), ResourcePath = Name)]
    [CreateAssetMenu(fileName = Name, menuName = "Init(args) Demo/" + Name)]
    public sealed class InputSettings : ScriptableObject, IInputSettings
    {
        private const string Name = "Input Settings";

        [SerializeField]
        private KeyCode resetKey = KeyCode.R;

        [SerializeField]
        private KeyCode moveLeftKey = KeyCode.LeftArrow;

        [SerializeField]
        private KeyCode moveRightKey = KeyCode.RightArrow;

        [SerializeField]
        private KeyCode moveUpKey = KeyCode.UpArrow;

        [SerializeField]
        private KeyCode moveDownKey = KeyCode.DownArrow;

        /// <inheritdoc/>
        public KeyCode ResetKey => resetKey;

        /// <inheritdoc/>
        public KeyCode MoveLeftKey => moveLeftKey;

        /// <inheritdoc/>
        public KeyCode MoveRightKey => moveRightKey;

        /// <inheritdoc/>
        public KeyCode MoveUpKey => moveUpKey;

        /// <inheritdoc/>
        public KeyCode MoveDownKey => moveDownKey;
    }
}