using System;
using System.Diagnostics;
using Sisus.Init;
using System.Diagnostics.CodeAnalysis;

namespace Init.Demo
{
    /// <summary>
    /// Extension methods for objects that implement <see cref="ILog"/>
    /// that make it possible to log messages to the Console in the editor
    /// and in debug builds.
    /// </summary>
    public static class LogExtensions
    {
        private static ILogger logger;

        static LogExtensions()
        {
            logger = Service<ILogger>.Instance;

            if(logger is null)
            {
                #if DEBUG
                logger = new DebugLogger();
                #else
                logger = new NullLogger();
                #endif
            }

            Service.RemoveInstanceChangedListener<ILogger>(OnLoggerChanged);
            Service.AddInstanceChangedListener<ILogger>(OnLoggerChanged);
        }

        private static void OnLoggerChanged(Clients clients, [AllowNull] ILogger oldInstance, [AllowNull] ILogger newInstance) => logger = newInstance;

        /// <summary>
	    /// Logs a <paramref name="message"/> to the Console.
        /// <para>
        /// <example>
        /// <code>
        /// public class Actor : MonoBehaviour, ILog
        /// {
        ///     public void PrintName()
        ///     {
        ///         this.Log("Name: " + name);
        ///     }
        /// }
        /// </code>
        /// </example>
        /// </para>
	    /// </summary>
        /// <param name="context">
	    /// <see cref="object"/> which is logging the message.
	    /// <para>
	    /// If the object is of type <see cref="UnityEngine.Object"/> it will be momentarily highlighted
        /// in the Hierarchy window when you click the log message in the Console.
	    /// </para>
	    /// </param>
	    /// <param name="message"> <see cref="string"/> or <see cref="object"/> to be converted to string representation for display. </param>
        [Conditional("DEBUG"), DebuggerStepThrough]
        public static void Log<T>(this T context, object message) where T : ILog
        {
            #if DEBUG
            logger.Log(message, context);
            #endif
        }

        /// <summary>
	    /// Logs a warning <paramref name="message"/> to the Console.
	    /// </summary>
        /// <param name="context">
	    /// <see cref="object"/> which is logging the message.
	    /// <para>
	    /// If the object is of type <see cref="UnityEngine.Object"/> it will be momentarily highlighted
        /// in the Hierarchy window when you click the log message in the Console.
	    /// </para>
	    /// </param>
	    /// <param name="message"> <see cref="string"/> or <see cref="object"/> to be converted to string representation for display. </param>
        [Conditional("DEBUG"), DebuggerStepThrough]
        public static void LogWarning<T>(this T context, object message) where T : ILog
        {
            #if DEBUG
            logger.LogWarning(message, context);
            #endif
        }

        /// <summary>
        /// Logs an <paramref name="exception"/> to the Console.
        /// </summary>
        /// <param name="context">
	    /// <see cref="object"/> which is logging the message.
	    /// <para>
	    /// If the object is of type <see cref="UnityEngine.Object"/> it will be momentarily highlighted
        /// in the Hierarchy window when you click the log message in the Console.
	    /// </para>
	    /// </param>
        /// <param name="exception"> Runtime exception to display. </param>
        [Conditional("DEBUG"), DebuggerStepThrough]
        public static void LogException<T>(this T context, Exception exception) where T : ILog
        {
            #if DEBUG
            logger.LogException(exception, context);
            #endif
        }

        /// <summary>
        /// Logs a <paramref name="message"/> to the Console.
        /// <para>
        /// This can be used instead of <see cref="Log(object, object)"/> when
        /// logging from the context of a static method.
        /// </para>
        /// <para>
        /// <example>
        /// <code>
        /// public class Actor : MonoBehaviour, ILog
        /// {
        ///     public static void PrintInstanceCount()
        ///     {
        ///         typeof(Actor).Log("Instances of " + nameof(Actor) + " in scene: " + FindObjectsOfType<Actor>().Length);
        ///     }
        /// }
        /// </code>
        /// </example>
        /// </para>
        /// </summary>
        /// <param name="context"> <see cref="Type"/> of the class which is logging the message. </param>
        /// <param name="message"> <see cref="string"/> or <see cref="object"/> to be converted to string representation for display. </param>
        [Conditional("DEBUG"), DebuggerStepThrough]
        public static void Log(this Type context, object message)
        {
            #if DEBUG
            logger.Log(message);
            #endif
        }

        /// <summary>
        /// Logs a warning <paramref name="message"/> to the Console.
        /// <para>
        /// This can be used instead of <see cref="Log(object, object)"/> when
        /// logging from the context of a static method.
        /// </para>
        /// </summary>
        /// <param name="context"> <see cref="Type"/> of the class which is logging the message. </param>
        /// <param name="message"> <see cref="string"/> or <see cref="object"/> to be converted to string representation for display. </param>
        [Conditional("DEBUG"), DebuggerStepThrough]
        public static void LogWarning(this Type context, object message)
        {
            #if DEBUG
            logger.LogWarning(message);
            #endif
        }

        /// <summary>
        /// Logs an error <paramref name="message"/> to the Console.
        /// <para>
        /// This can be used instead of <see cref="Log(object, object)"/> when
        /// logging from the context of a static method.
        /// </para>
        /// </summary>
        /// <param name="context"> <see cref="Type"/> of the class which is logging the message. </param>
        /// <param name="message"> <see cref="string"/> or <see cref="object"/> to be converted to string representation for display. </param>
        [Conditional("DEBUG"), DebuggerStepThrough]
        public static void LogError(this Type context, object message)
        {
            #if DEBUG
            logger.LogError(message);
            #endif
        }

        /// <summary>
        /// Logs an <paramref name="exception"/> to the Console.
        /// <para>
        /// This can be used instead of <see cref="Log(object, object)"/> when
        /// logging from the context of a static method.
        /// </para>
        /// </summary>
        /// <param name="context"> <see cref="Type"/> of the class which is logging the message. </param>
        /// <param name="exception"> Runtime exception to display. </param>
        [Conditional("DEBUG"), DebuggerStepThrough]
        public static void LogException(this Type context, Exception exception)
        {
            #if DEBUG
            logger.LogException(exception);
            #endif
        }
    }
}