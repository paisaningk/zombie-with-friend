using NUnit.Framework;
using Sisus.Init;
using Sisus.Init.Testing;
using UnityEngine;
using UnityEngine.Events;
using static Sisus.NullExtensions;
using Object = UnityEngine.Object;

namespace Init.Demo.Tests
{
    /// <summary>
    /// Unit tests for <see cref="Player"/>.
    /// </summary>
    public sealed class TestPlayer
    {
        private Player player;
        private Testable testable;

        [SetUp]
        public void Setup()
        {
            player = new GameObject<Player>().Init(new Trackable() as ITrackable);
            testable = new Testable(player.gameObject);
        }

        [TearDown]
        public void TearDown()
        {
            testable.Destroy();
        }

        [Test]
        public void Trackable_Is_Not_Null()
        {
            Assert.IsNotNull(player.Trackable);
            Assert.IsTrue(player.Trackable != Null);
        }

        [Test]
        public void OnTriggerEnter_With_Collectable_Object_Invokes_Collected()
        {
            (MockCollectable collectable, BoxCollider collider) collectable = new GameObject<MockCollectable, BoxCollider>();

            collectable.collectable.HasBeenCollected = false;
            testable.OnTriggerEnter(collectable.collider);
            Assert.IsTrue(collectable.collectable.HasBeenCollected);

            Object.DestroyImmediate(collectable.collider.gameObject);
        }

        private class Trackable : ITrackable
        {
            public Vector2 Position => Vector2.zero;
            public event UnityAction PositionChanged { add { } remove { } }
        }
    }
}