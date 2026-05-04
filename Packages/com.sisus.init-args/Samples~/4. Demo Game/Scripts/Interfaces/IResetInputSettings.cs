using UnityEngine;

namespace Init.Demo
{
    /// <summary>
    /// Represents an object that holds settings data for user input
    /// that can be used to reset the game.
    /// </summary>
    public interface IResetInputSettings
    {
        /// <summary>
        /// Key that can be used to reset the game.
        /// </summary>
        KeyCode ResetKey { get; }
    }
}