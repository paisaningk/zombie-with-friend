using System;
using System.Collections.Generic;
using NUnit.Framework;
using Sisus.Init;
using Sisus.Init.Testing;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Events;
using static UnityEngine.Object;

namespace Init.Demo.Tests
{
    /// <summary>
    /// Unit tests for <see cref="ResetHandler"/>.
    /// </summary>
    public sealed class TestResetHandler
    {
        private InputManager inputManager;
        private ResetHandler resetHandler;
        private Testable testable;
        private readonly List<MockResettable> resettables = new List<MockResettable>();

        [SetUp]
        public void Setup()
        {
            EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);

            inputManager = new InputManager();
            resetHandler = new GameObject<ResetHandler>().Init(inputManager as IInputManager);
            testable = new Testable(resetHandler.gameObject);

            for(int i = 0; i < 3; i++)
            {
                resettables.Add(new GameObject<MockResettable>());
            }
        }

        [TearDown]
        public void TearDown()
        {
            testable.Destroy();

            foreach(var resettable in resettables)
            {
                DestroyImmediate(resettable);
            }

            resettables.Clear();
        }

        [Test]
        public void Reset_Input_Resets_All_Resettables_In_Scene()
        {
            foreach(var resettable in resettables)
            {
                Assert.IsFalse(resettable.HasBeenReset);
            }

            inputManager.InvokeResetInputGiven();

            foreach(var resettable in resettables)
            {
                Assert.IsTrue(resettable.HasBeenReset);
            }
        }

        [Test]
        public void Can_Reset_Multiple_Times()
        {
            foreach(var resettable in resettables)
            {
                Assert.IsFalse(resettable.HasBeenReset);
            }

            inputManager.InvokeResetInputGiven();

            foreach(var resettable in resettables)
            {
                Assert.IsTrue(resettable.HasBeenReset);
                resettable.HasBeenReset = false;
                Assert.IsFalse(resettable.HasBeenReset);
            }

            inputManager.InvokeResetInputGiven();

            foreach(var resettable in resettables)
            {
                Assert.IsTrue(resettable.HasBeenReset);
            }
        }

        private sealed class InputManager : IInputManager
        {
            public event UnityAction<Vector2> MoveInputChanged { add { } remove { } }
            public event Action ResetInputGiven;

            public void InvokeResetInputGiven() => ResetInputGiven();
        }
    }
}