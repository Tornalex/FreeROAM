using System;
using System.Collections;
using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

namespace SpearWander.PlayModeTests
{
    /// <summary>
    /// Verifies the PlayMode harness foundation itself: reflection access,
    /// static-state reset, additive scene loading, active-scene restoration,
    /// log capture and cleanup behaviour.
    /// </summary>
    public class PlayModeHarnessTests : PlayModeTestBase
    {
        private const string RegisteredRoomScenePath =
            "Assets/Scenes/Rooms/Yellow Forest/YellowForest_TreeBranch.unity";

        [Test]
        public void ReflectionUtilitiesResolveExpectedRuntimeTypes()
        {
            Assert.That(
                Application.isPlaying,
                Is.True,
                "Harness tests must execute in PlayMode.");

            AssertRuntimeType("GameSceneManager");
            AssertRuntimeType("RoomTransition");
            AssertRuntimeType("BaseEnemy");
            AssertRuntimeType("SpearWander.Abilities.DeadEnemyTracker");
            AssertRuntimeType("EnemyRoomTrackingKey");
        }

        [Test]
        public void ReflectionUtilitiesReadWriteInstanceMembers()
        {
            Type doorType = ReflectionTestUtilities.ResolveRuntimeType("RoomTransition");
            GameObject probe = Fixture.CreateGameObject("ReflectionInstanceProbe");

            Component door = probe.AddComponent(doorType);
            Assert.That(door, Is.Not.Null);

            ReflectionTestUtilities.SetInstanceField(door, "doorID", "ProbeDoor");
            Assert.That(
                ReflectionTestUtilities.GetInstanceField<string>(door, "doorID"),
                Is.EqualTo("ProbeDoor"));
            Assert.That(
                ReflectionTestUtilities.GetProperty<string>(door, "DoorID"),
                Is.EqualTo("ProbeDoor"));

            ReflectionTestUtilities.SetInstanceField(door, "targetSceneName", "ProbeScene");
            Assert.That(
                ReflectionTestUtilities.GetInstanceField<string>(door, "targetSceneName"),
                Is.EqualTo("ProbeScene"));
            Assert.That(
                ReflectionTestUtilities.GetProperty<string>(door, "TargetSceneName"),
                Is.EqualTo("ProbeScene"));
        }

        [Test]
        public void ReflectionUtilitiesFailClearlyForMissingMembers()
        {
            Type managerType = ReflectionTestUtilities.ResolveRuntimeType("GameSceneManager");

            AssertionException fieldFailure = Assert.Throws<AssertionException>(() =>
            {
                ReflectionTestUtilities.GetStaticField(managerType, "NoSuchStaticField");
            });
            StringAssert.Contains("NoSuchStaticField", fieldFailure.Message);
            StringAssert.Contains("GameSceneManager", fieldFailure.Message);

            AssertionException typeFailure = Assert.Throws<AssertionException>(() =>
            {
                ReflectionTestUtilities.ResolveRuntimeType("SpearWander.DoesNotExist");
            });
            StringAssert.Contains("SpearWander.DoesNotExist", typeFailure.Message);
        }

        [Test]
        public void StaticTrackingStateCanBeReset()
        {
            Type deadEnemyTracker =
                ReflectionTestUtilities.ResolveRuntimeType("SpearWander.Abilities.DeadEnemyTracker");

            ReflectionTestUtilities.InvokeStatic(
                deadEnemyTracker,
                "MarkDead",
                "HarnessProbeRoom",
                "HarnessProbeEnemy");
            Assert.That(
                ReflectionTestUtilities.InvokeStatic<bool>(
                    deadEnemyTracker,
                    "IsDead",
                    "HarnessProbeRoom",
                    "HarnessProbeEnemy"),
                Is.True);

            Type trackingKey = ReflectionTestUtilities.ResolveRuntimeType("EnemyRoomTrackingKey");
            object rawFailureScenes = ReflectionTestUtilities.GetStaticField(
                trackingKey,
                "CatalogFailureScenes");
            Assert.That(rawFailureScenes, Is.InstanceOf<ICollection<string>>());

            ICollection<string> catalogFailureScenes = (ICollection<string>)rawFailureScenes;
            catalogFailureScenes.Add("Assets/HarnessProbe/Scene.unity");
            Assert.That(catalogFailureScenes, Does.Contain("Assets/HarnessProbe/Scene.unity"));

            Time.timeScale = 0.5f;

            PlayModeTestFixture.ResetStaticState();

            Assert.That(
                ReflectionTestUtilities.InvokeStatic<bool>(
                    deadEnemyTracker,
                    "IsDead",
                    "HarnessProbeRoom",
                    "HarnessProbeEnemy"),
                Is.False);
            Assert.That(catalogFailureScenes, Is.Empty);
            Assert.That(Time.timeScale, Is.EqualTo(1f).Within(0.0001f));
        }

        [UnityTest]
        public IEnumerator SceneUtilitiesLoadAndUnloadRegisteredRoom()
        {
            Scene originalActive = SceneManager.GetActiveScene();
            Scene loaded = default;

            yield return Fixture.LoadSceneAdditive(
                RegisteredRoomScenePath,
                scene => loaded = scene);

            Assert.That(loaded.IsValid(), Is.True);
            Assert.That(loaded.isLoaded, Is.True);
            Assert.That(loaded.path, Is.EqualTo(RegisteredRoomScenePath));
            Assert.That(Fixture.IsSceneLoaded(RegisteredRoomScenePath), Is.True);

            Scene resolved = Fixture.GetLoadedScene(RegisteredRoomScenePath);
            Assert.That(resolved.handle, Is.EqualTo(loaded.handle));

            Assert.That(originalActive.IsValid(), Is.True);
            Assert.That(originalActive.isLoaded, Is.True);

            yield return Fixture.UnloadScene(loaded);

            Assert.That(loaded.isLoaded, Is.False);
            Assert.That(Fixture.IsSceneLoaded(RegisteredRoomScenePath), Is.False);
            Assert.That(SceneManager.GetSceneByPath(RegisteredRoomScenePath).isLoaded, Is.False);
        }

        [UnityTest]
        public IEnumerator CleanupRestoresPreviousActiveScene()
        {
            Scene originalActive = SceneManager.GetActiveScene();
            Scene loaded = default;

            yield return Fixture.LoadSceneAdditive(
                RegisteredRoomScenePath,
                scene => loaded = scene);

            Assert.That(SceneManager.SetActiveScene(loaded), Is.True);
            Assert.That(
                SceneManager.GetActiveScene().handle,
                Is.EqualTo(loaded.handle));

            yield return Fixture.Cleanup();

            Assert.That(
                SceneManager.GetActiveScene().handle,
                Is.EqualTo(originalActive.handle));
            Assert.That(Fixture.IsSceneLoaded(RegisteredRoomScenePath), Is.False);
        }

        [UnityTest]
        public IEnumerator CleanupCompletesAfterControlledAssertionException()
        {
            Type deadEnemyTracker =
                ReflectionTestUtilities.ResolveRuntimeType("SpearWander.Abilities.DeadEnemyTracker");
            ReflectionTestUtilities.InvokeStatic(
                deadEnemyTracker,
                "MarkDead",
                "AssertionProbeRoom",
                "AssertionProbeEnemy");

            Scene loaded = default;
            yield return Fixture.LoadSceneAdditive(
                RegisteredRoomScenePath,
                scene => loaded = scene);
            Assert.That(loaded.isLoaded, Is.True);

            Time.timeScale = 0.25f;

            bool assertionObserved = false;
            try
            {
                Assert.Fail("Controlled assertion failure used to verify fixture teardown.");
            }
            catch (AssertionException)
            {
                assertionObserved = true;
            }

            Assert.That(assertionObserved, Is.True);

            yield return Fixture.Cleanup();

            Assert.That(
                ReflectionTestUtilities.InvokeStatic<bool>(
                    deadEnemyTracker,
                    "IsDead",
                    "AssertionProbeRoom",
                    "AssertionProbeEnemy"),
                Is.False);
            Assert.That(Fixture.IsSceneLoaded(RegisteredRoomScenePath), Is.False);
            Assert.That(Time.timeScale, Is.EqualTo(1f).Within(0.0001f));
        }

        [UnityTest]
        public IEnumerator LogCaptureRecordsAndUnsubscribes()
        {
            PlayModeLogCapture capture = Fixture.StartLogCapture();
            Assert.That(capture.IsSubscribed, Is.True);

            string token = Guid.NewGuid().ToString("N");
            string logMessage = "harness-log-" + token;
            string warningMessage = "harness-warning-" + token;

            Debug.Log(logMessage);
            Debug.LogWarning(warningMessage);
            yield return null;

            IReadOnlyList<PlayModeLogCapture.Entry> logs = capture.Find(logMessage);
            Assert.That(logs, Has.Count.EqualTo(1));
            Assert.That(logs[0].Type, Is.EqualTo(LogType.Log));
            Assert.That(logs[0].Message, Is.EqualTo(logMessage));
            Assert.That(logs[0].StackTrace, Is.Not.Null);

            IReadOnlyList<PlayModeLogCapture.Entry> warnings = capture.Find(warningMessage);
            Assert.That(warnings, Has.Count.EqualTo(1));
            Assert.That(warnings[0].Type, Is.EqualTo(LogType.Warning));

            Fixture.StopLogCapture();
            Assert.That(capture.IsSubscribed, Is.False);

            // Idempotent unsubscribe must not throw.
            capture.Dispose();
            Assert.That(capture.IsSubscribed, Is.False);

            string afterToken = Guid.NewGuid().ToString("N");
            string afterMessage = "harness-after-" + afterToken;
            Debug.Log(afterMessage);
            yield return null;

            Assert.That(capture.Find(afterMessage), Is.Empty);
        }

        private static void AssertRuntimeType(string typeName)
        {
            Type type = ReflectionTestUtilities.ResolveRuntimeType(typeName);

            Assert.That(type, Is.Not.Null);
            Assert.That(type.Name, Is.EqualTo(LastSegment(typeName)));
            Assert.That(
                type.Assembly.GetName().Name,
                Is.EqualTo("Assembly-CSharp"),
                $"Type '{typeName}' must come from Assembly-CSharp.");
            TestContext.WriteLine(
                $"[PlayModeHarness] {typeName} -> {type.FullName} [{type.Assembly.GetName().Name}]");
        }

        private static string LastSegment(string typeName)
        {
            int lastDot = typeName.LastIndexOf('.');
            return lastDot < 0 ? typeName : typeName.Substring(lastDot + 1);
        }
    }
}
