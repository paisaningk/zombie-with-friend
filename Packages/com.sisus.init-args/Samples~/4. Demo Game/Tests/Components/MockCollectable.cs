using UnityEngine;

namespace Init.Demo.Tests
{
    /// <summary>
    /// Mock implementation of <see cref="ICollectable"/> used during unit testing.
    /// </summary>
    [AddComponentMenu("")]
    public sealed class MockCollectable : MonoBehaviour, ICollectable
    {
        public bool HasBeenCollected { get; set; }

        public void Collect() => HasBeenCollected = true;
    }
}