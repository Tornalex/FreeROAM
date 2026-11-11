using System;
using System.Collections.Generic;
using System.Reflection;
using NUnit.Framework;
using SpearWander.Abilities;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// Integration tests for BaseEnemy's cached room key. Actual suppression and
/// destruction are Unity lifecycle behaviour and require PlayMode; these tests
/// verify the cached key that Start and Die use through BaseEnemy's small
/// protected helpers.
/// </summary>
[TestFixture]
public class BaseEnemyRoomTrackingTests
{
    private const string SourceRoomPath =
        "Assets/Scenes/Rooms/Yellow Forest/YellowForest_ToMOTHER.unity";
    private const string DestinationRoomPath =
        "Assets/Scenes/Rooms/Yellow Forest/YellowForest_TreeBranch.unity";
    private const string UnregisteredRoomPath = "Assets/Scenes/Rooms/BaseRoom.unity";

    private sealed class TestEnemy : BaseEnemy
    {
        public void RunStart() => Start();
        public bool CheckTrackedAsDead() => IsTrackedAsDead();
        public void RegisterDeath() => ReportDeath();
    }

    private sealed class Fixture : IDisposable
    {
        private readonly Scene _originalActiveScene;
        private readonly List<Scene> _openedScenes = new List<Scene>();
        private readonly List<GameObject> _createdEnemies = new List<GameObject>();
        private readonly GameSceneManager _previousManager;
        private readonly string _previousManagerRoom;

        private GameObject _createdManagerHost;
        private GameSceneManager _createdManager;
        private bool _disposed;

        public Fixture()
        {
            _originalActiveScene = SceneManager.GetActiveScene();
            _previousManager = GameSceneManager.Instance;
            if (_previousManager != null)
                _previousManagerRoom = GetPrivateField<string>(_previousManager, "_currentRoomScene");
        }

        public Scene OpenScene(string scenePath)
        {
            Scene scene = SceneManager.GetSceneByPath(scenePath);
            if (!scene.IsValid() || !scene.isLoaded)
            {
                scene = EditorSceneManager.OpenScene(scenePath, OpenSceneMode.Additive);
                _openedScenes.Add(scene);
            }

            RestoreOriginalActiveScene();
            return scene;
        }

        public Scene OpenUnregisteredScene()
        {
            Scene scene = OpenScene(UnregisteredRoomPath);
            Assert.AreEqual(
                RoomIdentityStatus.UnknownScenePath,
                RoomIdentityResolver.ResolveByScenePath(scene.path).Status,
                "The unregistered-scene fixture must not be present in the catalog.");
            return scene;
        }

        public TestEnemy CreateEnemy(Scene scene, string name, string stableIdentity)
        {
            GameObject gameObject = new GameObject(name);
            _createdEnemies.Add(gameObject);

            SceneManager.MoveGameObjectToScene(gameObject, scene);

            TestEnemy enemy = gameObject.AddComponent<TestEnemy>();
            SetPrivateField(enemy, "stableIdentity", stableIdentity);
            return enemy;
        }

        public void UseManagerWithCurrentRoom(string currentRoom)
        {
            if (GameSceneManager.Instance != null)
            {
                SetPrivateField(GameSceneManager.Instance, "_currentRoomScene", currentRoom);
                return;
            }

            if (_createdManager == null)
            {
                _createdManagerHost = new GameObject("Enemy Tracking Test Manager");
                _createdManagerHost.SetActive(false);
                _createdManager = _createdManagerHost.AddComponent<GameSceneManager>();
            }

            SetPrivateField(_createdManager, "_currentRoomScene", currentRoom);
            SetStaticInstance(_createdManager);
        }

        public void RemoveManager()
        {
            SetStaticInstance(null);
        }

        public void Dispose()
        {
            if (_disposed)
                return;

            _disposed = true;

            for (int i = _createdEnemies.Count - 1; i >= 0; i--)
            {
                if (_createdEnemies[i] != null)
                    UnityEngine.Object.DestroyImmediate(_createdEnemies[i]);
            }
            _createdEnemies.Clear();

            RestoreOriginalActiveScene();

            for (int i = _openedScenes.Count - 1; i >= 0; i--)
            {
                Scene scene = _openedScenes[i];
                if (scene.IsValid() && scene.isLoaded)
                    EditorSceneManager.CloseScene(scene, removeScene: true);
            }
            _openedScenes.Clear();

            if (_createdManagerHost != null)
            {
                SetStaticInstance(null);
                UnityEngine.Object.DestroyImmediate(_createdManagerHost);
                _createdManagerHost = null;
                _createdManager = null;
            }

            if (_previousManager != null)
            {
                SetPrivateField(_previousManager, "_currentRoomScene", _previousManagerRoom);
                SetStaticInstance(_previousManager);
            }
            else
            {
                SetStaticInstance(null);
            }

            RestoreOriginalActiveScene();
        }

        private void RestoreOriginalActiveScene()
        {
            if (_originalActiveScene.IsValid() && SceneManager.GetActiveScene() != _originalActiveScene)
                SceneManager.SetActiveScene(_originalActiveScene);
        }

        private static void SetStaticInstance(GameSceneManager manager)
        {
            PropertyInfo property = typeof(GameSceneManager).GetProperty(
                "Instance", BindingFlags.Public | BindingFlags.Static);
            Assert.IsNotNull(property, "GameSceneManager.Instance was not found.");

            MethodInfo setter = property.GetSetMethod(nonPublic: true);
            Assert.IsNotNull(setter, "GameSceneManager.Instance has no accessible setter.");

            setter.Invoke(null, new object[] { manager });
        }
    }

    [SetUp]
    public void SetUp()
    {
        DeadEnemyTracker.ClearAll();
    }

    [TearDown]
    public void TearDown()
    {
        DeadEnemyTracker.ClearAll();
    }

    [Test]
    public void StartUsesDestinationRoomIdWhileManagerReportsSource()
    {
        using (Fixture fixture = new Fixture())
        {
            Scene destination = fixture.OpenScene(DestinationRoomPath);

            RoomIdentityResult sourceIdentity = RoomIdentityResolver.ResolveByScenePath(SourceRoomPath);
            RoomIdentityResult destinationIdentity = RoomIdentityResolver.ResolveByScenePath(destination.path);
            Assert.AreEqual(RoomIdentityStatus.Success, sourceIdentity.Status, sourceIdentity.Message);
            Assert.AreEqual(RoomIdentityStatus.Success, destinationIdentity.Status, destinationIdentity.Message);

            // During an additive transition the manager can still report the
            // source room while destination-scene objects are already loaded.
            // The source scene is not opened here: its identity is only needed
            // to prove that the manager's committed room is ignored.
            string sourceSceneName = System.IO.Path.GetFileNameWithoutExtension(SourceRoomPath);
            fixture.UseManagerWithCurrentRoom(sourceSceneName);

            TestEnemy enemy = fixture.CreateEnemy(destination, "Destination Enemy", "destination-enemy");
            enemy.RunStart();

            Assert.AreEqual(
                destination.name,
                enemy.roomName,
                "The legacy roomName field must keep describing the owning scene.");

            DeadEnemyTracker.ClearAll();
            DeadEnemyTracker.MarkDead(destinationIdentity.Room.RoomId, "destination-enemy");
            Assert.IsTrue(
                enemy.CheckTrackedAsDead(),
                "Start must key suppression to the owning destination RoomId.");

            DeadEnemyTracker.ClearAll();
            DeadEnemyTracker.MarkDead(sourceIdentity.Room.RoomId, "destination-enemy");
            Assert.IsFalse(
                enemy.CheckTrackedAsDead(),
                "The manager's committed source RoomId must not suppress a destination enemy.");

            DeadEnemyTracker.ClearAll();
            DeadEnemyTracker.MarkDead(destination.name, "destination-enemy");
            Assert.IsFalse(
                enemy.CheckTrackedAsDead(),
                "A registered room must not fall back to its scene name.");
        }
    }

    [Test]
    public void StartUsesSceneNameForUnregisteredScene()
    {
        using (Fixture fixture = new Fixture())
        {
            Scene scene = fixture.OpenUnregisteredScene();
            fixture.UseManagerWithCurrentRoom("CommittedSourceRoom");

            TestEnemy enemy = fixture.CreateEnemy(scene, "Unregistered Enemy", "unregistered-enemy");
            enemy.RunStart();

            Assert.AreEqual(scene.name, enemy.roomName);

            DeadEnemyTracker.ClearAll();
            DeadEnemyTracker.MarkDead(scene.name, "unregistered-enemy");
            Assert.IsTrue(
                enemy.CheckTrackedAsDead(),
                "An unregistered scene must keep using its scene name.");

            DeadEnemyTracker.ClearAll();
            DeadEnemyTracker.MarkDead("CommittedSourceRoom", "unregistered-enemy");
            Assert.IsFalse(
                enemy.CheckTrackedAsDead(),
                "The manager's current room must not replace an unregistered scene name.");
        }
    }

    [Test]
    public void StartUsesUnknownWhenManagerIsUnavailable()
    {
        using (Fixture fixture = new Fixture())
        {
            Scene scene = fixture.OpenUnregisteredScene();
            fixture.RemoveManager();

            TestEnemy enemy = fixture.CreateEnemy(scene, "No Manager Enemy", "no-manager-enemy");
            enemy.RunStart();

            Assert.AreEqual("Unknown", enemy.roomName);

            DeadEnemyTracker.ClearAll();
            DeadEnemyTracker.MarkDead("Unknown", "no-manager-enemy");
            Assert.IsTrue(enemy.CheckTrackedAsDead());
        }
    }

    [Test]
    public void ReportDeathUsesCachedKeyNotMutableRoomName()
    {
        using (Fixture fixture = new Fixture())
        {
            Scene scene = fixture.OpenUnregisteredScene();
            fixture.UseManagerWithCurrentRoom("CommittedSourceRoom");

            TestEnemy enemy = fixture.CreateEnemy(scene, "Death Enemy", "death-enemy");
            enemy.RunStart();

            string expectedKey = scene.name;
            enemy.roomName = "MutatedRoomName";

            enemy.RegisterDeath();

            Assert.IsTrue(
                DeadEnemyTracker.IsDead(expectedKey, "death-enemy"),
                "Death registration must use the key cached in Start.");
            Assert.IsFalse(
                DeadEnemyTracker.IsDead("MutatedRoomName", "death-enemy"),
                "Death registration must not read the mutable roomName field.");
            Assert.IsFalse(
                DeadEnemyTracker.IsDead("CommittedSourceRoom", "death-enemy"),
                "Death registration must not read the manager's current room.");
        }
    }

    [Test]
    public void EmptyStableIdentityIsNeverTreatedAsDead()
    {
        using (Fixture fixture = new Fixture())
        {
            Scene scene = fixture.OpenUnregisteredScene();
            fixture.UseManagerWithCurrentRoom("CommittedSourceRoom");

            TestEnemy enemy = fixture.CreateEnemy(scene, "Untracked Enemy", string.Empty);
            enemy.RunStart();

            DeadEnemyTracker.MarkDead(scene.name, string.Empty);

            Assert.IsFalse(
                enemy.CheckTrackedAsDead(),
                "An empty stable identity must disable individual death tracking.");
        }
    }

    private static T GetPrivateField<T>(object target, string fieldName)
    {
        FieldInfo field = FindPrivateField(target.GetType(), fieldName);
        Assert.IsNotNull(field, $"Expected private field '{fieldName}'.");
        return (T)field.GetValue(target);
    }

    private static void SetPrivateField(object target, string fieldName, object value)
    {
        FieldInfo field = FindPrivateField(target.GetType(), fieldName);
        Assert.IsNotNull(field, $"Expected private field '{fieldName}'.");
        field.SetValue(target, value);
    }

    private static FieldInfo FindPrivateField(Type type, string fieldName)
    {
        while (type != null)
        {
            FieldInfo field = type.GetField(
                fieldName, BindingFlags.Instance | BindingFlags.NonPublic);
            if (field != null)
                return field;

            type = type.BaseType;
        }

        return null;
    }
}
