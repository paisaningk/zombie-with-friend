using NUnit.Framework;
using Sisus.Init;
using UnityEngine;

namespace Init.Demo.Tests
{
    /// <summary>
    /// Unit tests for <see cref="Collectable"/>.
    /// </summary>
    public sealed class TestCollectable
    {
        private FakeEvent onCollectedEvent;
        private Collectable collectable;

        [SetUp]
        public void Setup()
        {
            onCollectedEvent = new();
            collectable = new GameObject<Collectable>().Init(onCollectedEvent as IEventTrigger);
        }

        [TearDown]
        public void TearDown()
        {
            Object.DestroyImmediate(collectable.gameObject);
            onCollectedEvent = null;
        }

        [Test]
        public void Collect_Triggers_Event()
        {
            collectable.Collect();
            Assert.IsTrue(onCollectedEvent.HasBeenTriggered);
        }

        [Test]
        public void Collect_Sets_GameObject_Inactive()
        {
            collectable.Collect();
            Assert.IsFalse(collectable.gameObject.activeSelf);
        }
    }
}