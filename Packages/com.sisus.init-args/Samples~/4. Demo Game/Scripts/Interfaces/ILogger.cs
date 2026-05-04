using System;

namespace Init.Demo
{
    /// <summary>
    /// Represents an object responsible for logging messages to the Unity Console.
    /// </summary>
    public interface ILogger
    {
        /// <summary>
        /// Logs a message to the Unity Console.
        /// </summary>
        /// <param name="message"> String or object to be converted to string representation for display. </param>
        /// <param name="context"> Object to which the message applies. </param>
        void Log(object message, object context = null);

        /// <summary>
        /// Logs a warning message to the Unity Console.
        /// </summary>
        /// <param name="message"> String or object to be converted to string representation for display. </param>
        /// <param name="context"> Object to which the message applies. </param>
        void LogWarning(object message, object context = null);

        /// <summary>
        /// Logs an error message to the Unity Console.
        /// </summary>
        /// <param name="message"> String or object to be converted to string representation for display. </param>
        /// <param name="context"> Object to which the message applies. </param>
        void LogError(object message, object context = null);

        /// <summary>
        /// Logs an exception as an error message to the Unity Console.
        /// </summary>
        /// <param name="exception"> Runtime <see cref="Exception"/>. </param>
        /// <param name="context"> Object to which the message applies. </param>
        void LogException(Exception exception, object context = null);
    }
}