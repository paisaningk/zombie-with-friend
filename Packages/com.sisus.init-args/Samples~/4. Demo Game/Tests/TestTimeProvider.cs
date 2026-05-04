using System;

namespace Init.Demo.Tests
{
    /// <summary>
    /// Implementation of <see cref="ITimeProvider"/> and allows
    /// for free manipulation of the values returned by the properties.
    /// <para>
    /// This can be useful for unit tests.
    /// </para>
    /// </summary>
    public sealed class TestTimeProvider : ITimeProvider
    {
        /// <inheritdoc/>
        public float Time { get; set; }

        /// <inheritdoc/>
        public float DeltaTime { get; set; } = 0.3333f;

        /// <inheritdoc/>
        public float RealtimeSinceStartup { get; set; }

        /// <inheritdoc/>
        public DateTime Now { get; set; } = DateTime.UtcNow;

        /// <inheritdoc/>
        public object WaitForSeconds(float seconds) => null;
    }
}