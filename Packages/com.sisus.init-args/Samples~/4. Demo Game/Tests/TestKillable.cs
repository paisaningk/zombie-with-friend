using NUnit.Framework;
using Sisus.Init;
using Sisus.Init.Testing;
using UnityEngine;
using Object = UnityEngine.Object;

namespace Init.Demo.Tests
{
	/// <summary>
	/// Unit tests for <see cref="Killable"/>.
	/// </summary>
	public sealed class TestKillable
	{
		private FakeEvent onKilledEvent;
		private Killable killable;
		private Testable testable;
		private ILogger loggerWas;

		[SetUp]
		public void Setup()
		{
			loggerWas = Service<ILogger>.Instance;
			Service.Set<ILogger>(new NullLogger());
			onKilledEvent = new();
			killable = new GameObject<Killable, BoxCollider>().Init1(onKilledEvent as IEventTrigger);
			testable = new Testable(killable.gameObject);
			Assert.IsFalse(onKilledEvent.HasBeenTriggered);
		}

		[TearDown]
		public void TearDown()
		{
			testable.Destroy();
			Service.Set(loggerWas);
			onKilledEvent = null;
		}

		[Test]
		public void Kill_Sets_GameObject_Inactive()
		{
			killable.Kill();
			Assert.IsFalse(killable.gameObject.activeSelf);
		}

		[Test]
		public void Kill_Triggers_Event()
		{
			killable.Kill();
			Assert.IsTrue(onKilledEvent.HasBeenTriggered);
		}

		[Test]
		public void ResetState_Sets_GameObject_Active()
		{
			killable.Kill();
			((IResettable)killable).ResetState();
			Assert.IsTrue(killable.gameObject.activeSelf);
		}

		[Test]
		public void OnTriggerEnter_With_Deadly_Object_Sets_GameObject_Inactive()
		{
			(MockDeadly deadly, BoxCollider collider) deadly = new GameObject<MockDeadly, BoxCollider>();

			testable.OnTriggerEnter(deadly.collider);
			Assert.IsFalse(killable.gameObject.activeSelf);

			Object.DestroyImmediate(deadly.collider.gameObject);
		}

		[Test]
		public void OnTriggerEnter_With_Deadly_Object_Triggers_Event()
		{
			(MockDeadly deadly, BoxCollider collider) deadly = new GameObject<MockDeadly, BoxCollider>();

			testable.OnTriggerEnter(deadly.collider);
			Assert.IsFalse(killable.gameObject.activeSelf);

			Object.DestroyImmediate(deadly.collider.gameObject);
		}
	}
}