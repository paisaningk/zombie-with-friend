using UnityEngine;
using NUnit.Framework;
using Sisus.Init;
using Sisus.Init.Testing;

namespace Init.Demo.Tests
{
    /// <summary>
    /// Unit tests for <see cref="Movable"/>.
    /// </summary>
    public sealed class TestMovable
    {
        private Settings settings;
        private Level level;
        private TestTimeProvider timeProvider;
        private Movable movable;
        private Testable testable;

        [SetUp]
        public void Setup()
        {
            settings = new Settings();
            level = new Level();
            timeProvider = new TestTimeProvider();

            movable = new GameObject<Movable>().Init(settings as IMoveSettings, level as ILevel, timeProvider as ITimeProvider);
            testable = new Testable(movable.gameObject);
        }

        [TearDown]
        public void TearDown()
        {
            testable.Destroy();
        }

        [Test]
        public void Can_Move_Right()
        {
            settings.MoveSpeed = 1f;
            movable.transform.position = Vector3.zero;
            movable.MoveDirection = Vector2.right;
            timeProvider.DeltaTime = 1f;
            level.Bounds = new RectInt(0, 0, 3, 1);
            
            testable.Update();
            Assert.AreEqual(new Vector2(1f, 0f), movable.Position);
            
            testable.Update();
            Assert.AreEqual(new Vector2(2f, 0f), movable.Position);
            
            testable.Update();
            Assert.AreEqual(new Vector2(3f, 0f), movable.Position);
            
            testable.Update();
            Assert.AreEqual(new Vector2(3f, 0f), movable.Position);
        }

        [Test]
        public void Can_Move_Left()
        {
            settings.MoveSpeed = 1f;
            movable.transform.position = new Vector3(3f, 0f);
            movable.MoveDirection = Vector2.left;
            timeProvider.DeltaTime = 1f;
            level.Bounds = new RectInt(0, 0, 3, 1);
            
            testable.Update();
            Assert.AreEqual(new Vector2(2f, 0f), movable.Position);
            
            testable.Update();
            Assert.AreEqual(new Vector2(1f, 0f), movable.Position);
            
            testable.Update();
            Assert.AreEqual(Vector2.zero, movable.Position);
            
            testable.Update();
            Assert.AreEqual(Vector2.zero, movable.Position);
        }

        [Test]
        public void Can_Move_Up()
        {
            settings.MoveSpeed = 1f;
            movable.transform.position = Vector3.zero;
            movable.MoveDirection = Vector2.up;
            timeProvider.DeltaTime = 1f;
            level.Bounds = new RectInt(0, 0, 1, 3);
            
            testable.Update();
            Assert.AreEqual(new Vector2(0f, 1f), movable.Position);
            
            testable.Update();
            Assert.AreEqual(new Vector2(0f, 2f), movable.Position);

            testable.Update();            
            Assert.AreEqual(new Vector2(0f, 3f), movable.Position);

            testable.Update();            
            Assert.AreEqual(new Vector2(0f, 3f), movable.Position);
        }

        [Test]
        public void Can_Move_Down()
        {
            settings.MoveSpeed = 1f;
            movable.transform.position = new Vector3(0f, 3f);
            movable.MoveDirection = Vector2.down;
            timeProvider.DeltaTime = 1f;
            level.Bounds = new RectInt(0, 0, 1, 3);

            testable.Update();
            Assert.AreEqual(new Vector2(0f, 2f), movable.Position);

            testable.Update();
            Assert.AreEqual(new Vector2(0f, 1f), movable.Position);

            testable.Update();
            Assert.AreEqual(new Vector2(0f, 0f), movable.Position);

            testable.Update();
            Assert.AreEqual(new Vector2(0f, 0f), movable.Position);
        }

        [Test]
        public void Can_Not_Move_Left_Out_Of_Bounds()
        {
            settings.MoveSpeed = 1f;
            movable.transform.position = Vector3.zero;
            movable.MoveDirection = Vector2.left;
            timeProvider.DeltaTime = 1f;
            level.Bounds = new RectInt(0, 0, 1, 1);

            testable.Update();
            Assert.AreEqual(Vector2.zero, movable.Position);
        }

        [Test]
        public void Can_Not_Move_Right_Out_Of_Bounds()
        {
            settings.MoveSpeed = 1f;
            movable.transform.position = new Vector3(1f, 0f);
            movable.MoveDirection = Vector2.right;
            timeProvider.DeltaTime = 1f;
            level.Bounds = new RectInt(0, 0, 1, 1);

            testable.Update();
            Assert.AreEqual(new Vector2(1f, 0f), movable.Position);
        }

        [Test]
        public void Can_Not_Move_Up_Out_Of_Bounds()
        {
            settings.MoveSpeed = 1f;
            movable.transform.position = new Vector3(0f, 1f);
            movable.MoveDirection = Vector2.up;
            timeProvider.DeltaTime = 1f;
            level.Bounds = new RectInt(0, 0, 1, 1);

            testable.Update();
            Assert.AreEqual(new Vector2(0f, 1f), movable.Position);
        }

        [Test]
        public void Can_Not_Move_Down_Out_Of_Bounds()
        {
            settings.MoveSpeed = 1f;
            movable.transform.position = new Vector3(0f, 0f);
            movable.MoveDirection = Vector2.down;
            timeProvider.DeltaTime = 1f;
            level.Bounds = new RectInt(0, 0, 0, 0);

            testable.Update();
            Assert.AreEqual(new Vector2(0f, 0f), movable.Position);
        }

        [Test]
        public void Can_Not_Move_Out_Of_Bounds_With_High_Velocity()
        {
            settings.MoveSpeed = 1000f;
            timeProvider.DeltaTime = 1000f;
            level.Bounds = new RectInt(0, 0, 10, 10);

            movable.transform.position = new Vector3(5f, 5f);
            movable.MoveDirection = Vector2.down;
            testable.Update();
            Assert.AreEqual(new Vector2(5f, 0f), movable.Position);

            movable.transform.position = new Vector3(5f, 5f);
            movable.MoveDirection = Vector2.up;
            testable.Update();
            Assert.AreEqual(new Vector2(5f, 10f), movable.Position);

            movable.transform.position = new Vector3(5f, 5f);
            movable.MoveDirection = Vector2.right;
            testable.Update();
            Assert.AreEqual(new Vector2(10f, 5f), movable.Position);

            movable.transform.position = new Vector3(5f, 5f);
            movable.MoveDirection = Vector2.left;
            testable.Update();
            Assert.AreEqual(new Vector2(0f, 5f), movable.Position);
        }

        [Test]
        public void Can_Not_Move_With_Zero_Speed()
        {
            settings.MoveSpeed = 0f;
            timeProvider.DeltaTime = 1000f;
            level.Bounds = new RectInt(0, 0, 10, 10);
            movable.transform.position = Vector3.zero;

            movable.MoveDirection = Vector2.down;
            testable.Update();
            Assert.AreEqual(Vector2.zero, movable.Position);
            
            movable.MoveDirection = Vector2.up;
            testable.Update();
            Assert.AreEqual(Vector2.zero, movable.Position);
            
            movable.MoveDirection = Vector2.right;
            testable.Update();
            Assert.AreEqual(Vector2.zero, movable.Position);
            
            movable.MoveDirection = Vector2.left;
            testable.Update();
            Assert.AreEqual(Vector2.zero, movable.Position);
        }

        [Test]
        public void Can_Not_Move_In_Zero_Size_Level()
        {
            settings.MoveSpeed = 1000f;
            timeProvider.DeltaTime = 1000f;
            level.Bounds = new RectInt(0, 0, 0, 0);
            movable.transform.position = Vector3.zero;

            movable.MoveDirection = Vector2.down;
            testable.Update();
            Assert.AreEqual(Vector2.zero, movable.Position);

            movable.MoveDirection = Vector2.up;
            testable.Update();
            Assert.AreEqual(Vector2.zero, movable.Position);

            movable.MoveDirection = Vector2.right;
            testable.Update();
            Assert.AreEqual(Vector2.zero, movable.Position);

            movable.MoveDirection = Vector2.left;
            testable.Update();
            Assert.AreEqual(Vector2.zero, movable.Position);
        }

        [Test]
        public void Reset_Sets_Position_To_Level_Bounds_Min_Position()
        {
            level.Bounds = new RectInt(0, 0, 10, 10);
            movable.transform.position = new Vector3(10f, 10f);
            ((IResettable)movable).ResetState();
            Assert.AreEqual(Vector2.zero, movable.Position);

            level.Bounds = new RectInt(-10, -10, 10, 10);
            movable.transform.position = new Vector3(10f, 10f);
            ((IResettable)movable).ResetState();
            Assert.AreEqual(new Vector2(-10f, -10f), movable.Position);
        }

        private sealed class Settings : IMoveSettings
        {
            public float MoveSpeed { get; set; }
        }

        private sealed class Level : ILevel
        {
            public RectInt Bounds { get; set; }
        }
    }
}