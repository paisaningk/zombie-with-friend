using UnityEngine;

namespace Init.Demo
{
    /// <summary>
    /// Represents an object that holds settings data for user inputs
    /// that can be used to move the player character.
    /// </summary>
    public interface IMoveInputSettings
    {
        /// <summary>
        /// Key that can be used to move the player character left.
        /// </summary>
        KeyCode MoveLeftKey { get; }

        /// <summary>
        /// Key that can be used to move the player character right.
        /// </summary>
        KeyCode MoveRightKey { get; }

        /// <summary>
        /// Key that can be used to move the player character up.
        /// </summary>
        KeyCode MoveUpKey { get; }

        /// <summary>
        /// Key that can be used to move the player character down.
        /// </summary>
        KeyCode MoveDownKey { get; }
    }
}