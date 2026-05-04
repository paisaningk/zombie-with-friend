using System;
using System.Diagnostics;

namespace Init.Demo
{
    /// <summary>
    /// An implementation of <see cref="ILogger"/> that does nothing when
    /// any of its methods are called.
    /// <para>
    /// This can be useful for suppressing all logging during unit testing
    /// and in builds when no logging is desired.
    /// </para>
    /// </summary>
    /// <example>
    /// <code>
    /// public class MyTests
    /// {
    ///     private ILogger loggerWas;
    /// 
    ///     [SetUp]
    ///     public void Setup()
    ///     {
    ///         loggerWas = Service<ILogger>.Instance;
    ///         Service{ILogger}.SetInstance(new NullLogger());
    ///     }
    ///     
    ///     [Test]
    ///     public void Test()
    ///     {
    ///         // Add test code here.
    ///         // All logging performed inside tested classes via Service<ILogger>.Instance
    ///         // will be suppressed to avoid cluttering the Console unnecessarily.
    ///     }
    ///     
    ///     [TearDown]
    ///     public void TearDown()
    ///     {
    ///         Service<ILogger>.SetInstance(loggerWas);
    ///     }
    /// }
    /// </code>
    /// </example>
    public sealed class NullLogger : ILogger
    {
        [DebuggerStepThrough]
        public void Log(object message, object context = null) { }

        [DebuggerStepThrough]
        public void LogWarning(object message, object context = null) { }

        [DebuggerStepThrough]
        public void LogError(object message, object context = null) { }

        [DebuggerStepThrough]
        public void LogException(Exception exception, object context = null) { }
    }
}