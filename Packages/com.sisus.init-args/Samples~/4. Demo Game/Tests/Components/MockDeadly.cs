using UnityEngine;

namespace Init.Demo.Tests
{
    /// <summary>
    /// Mock implementation of <see cref="IDeadly"/> used during unit testing.
    /// </summary>
    [AddComponentMenu("")]
    public sealed class MockDeadly : MonoBehaviour, IDeadly { }
}