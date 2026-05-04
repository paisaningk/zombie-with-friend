using UnityEngine;

namespace Init.Demo.Tests
{
    /// <summary>
    /// Mock implementation of <see cref="IResettable"/> used during unit testing.
    /// </summary>
    [AddComponentMenu("")]
    public sealed class MockResettable : MonoBehaviour, IResettable
    {
        public bool HasBeenReset { get; set; }

        public void ResetState() => HasBeenReset = true;
    }
}