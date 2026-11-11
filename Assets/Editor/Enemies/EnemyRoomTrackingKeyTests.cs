using System;
using System.Collections.Generic;
using System.Text.RegularExpressions;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

/// <summary>
/// EditMode tests for the stateless room-key policy. Registered scenes are
/// exercised through a transient catalog; catalog failure statuses are passed
/// directly so no test reads or modifies the real Resources catalog.
/// </summary>
[TestFixture]
public class EnemyRoomTrackingKeyTests
{
    private const string RoomAId = "room-alpha";
    private const string RoomAPath = "Assets/TestRooms/Alpha.unity";
    private const string RegisteredRoomPath =
        "Assets/Scenes/Rooms/Yellow Forest/YellowForest_TreeBranch.unity";

    private sealed class Fixture : IDisposable
    {
        private readonly List<UnityEngine.Object> _objects = new List<UnityEngine.Object>();
        private bool _disposed;

        public readonly RoomDefinition RoomA;
        public readonly RuntimeRoomCatalog Catalog;

        public Fixture()
        {
            RoomA = ScriptableObject.CreateInstance<RoomDefinition>();
            RoomA.name = "Alpha";
            RoomA.AssignIdentity(RoomAId, "Alpha", "Alpha", "test-guid-alpha");
            _objects.Add(RoomA);

            Catalog = ScriptableObject.CreateInstance<RuntimeRoomCatalog>();
            Catalog.name = "Transient Enemy Tracking Test Catalog";
            _objects.Add(Catalog);
            Catalog.SetGeneratedEntries(new List<RuntimeRoomCatalogEntry>
            {
                new RuntimeRoomCatalogEntry(RoomA, RoomAPath)
            });
        }

        public void Dispose()
        {
            if (_disposed)
                return;

            _disposed = true;
            for (int i = _objects.Count - 1; i >= 0; i--)
            {
                if (_objects[i] != null)
                    UnityEngine.Object.DestroyImmediate(_objects[i]);
            }
        }
    }

    [Test]
    public void RegisteredScenePathUsesRoomId()
    {
        using (Fixture fixture = new Fixture())
        {
            RoomIdentityResult identity = RoomIdentityResolver.ResolveByScenePath(
                RoomAPath, fixture.Catalog);
            Assert.AreEqual(RoomIdentityStatus.Success, identity.Status, identity.Message);

            string key = EnemyRoomTrackingKey.Resolve(
                RoomAPath,
                "Alpha",
                "CommittedRoom",
                hasRoomManager: true,
                identity.Status,
                identity.Room,
                identity.Message);

            Assert.AreEqual(RoomAId, key);
        }
    }

    [Test]
    public void RegisteredSceneUsesRoomIdEvenWithoutManager()
    {
        using (Fixture fixture = new Fixture())
        {
            RoomIdentityResult identity = RoomIdentityResolver.ResolveByScenePath(
                RoomAPath, fixture.Catalog);
            Assert.AreEqual(RoomIdentityStatus.Success, identity.Status, identity.Message);

            string key = EnemyRoomTrackingKey.Resolve(
                RoomAPath,
                "Alpha",
                currentRoom: null,
                hasRoomManager: false,
                identity.Status,
                identity.Room,
                identity.Message);

            Assert.AreEqual(RoomAId, key);
        }
    }

    [Test]
    public void UnknownScenePathUsesSceneName()
    {
        using (Fixture fixture = new Fixture())
        {
            const string scenePath = "Assets/TestRooms/Unregistered.unity";
            RoomIdentityResult identity = RoomIdentityResolver.ResolveByScenePath(
                scenePath, fixture.Catalog);
            Assert.AreEqual(RoomIdentityStatus.UnknownScenePath, identity.Status, identity.Message);

            string key = EnemyRoomTrackingKey.Resolve(
                scenePath,
                "UnregisteredScene",
                "CommittedRoom",
                hasRoomManager: true,
                identity.Status,
                identity.Room,
                identity.Message);

            Assert.AreEqual("UnregisteredScene", key);
        }
    }

    [Test]
    public void EmptyScenePathUsesSceneName()
    {
        using (Fixture fixture = new Fixture())
        {
            RoomIdentityResult identity = RoomIdentityResolver.ResolveByScenePath(
                string.Empty, fixture.Catalog);
            Assert.AreEqual(RoomIdentityStatus.InvalidScenePath, identity.Status, identity.Message);

            string key = EnemyRoomTrackingKey.Resolve(
                string.Empty,
                "OrdinaryScene",
                "CommittedRoom",
                hasRoomManager: true,
                identity.Status,
                identity.Room,
                identity.Message);

            Assert.AreEqual("OrdinaryScene", key);
        }
    }

    [Test]
    public void MissingCatalogFallsBackToSceneNameAndReportsDiagnostic()
    {
        const string scenePath = "Assets/TestRooms/MissingCatalog.unity";
        RoomIdentityResult identity = RoomIdentityResolver.ResolveByScenePath(scenePath, null);
        Assert.AreEqual(RoomIdentityStatus.CatalogMissing, identity.Status, identity.Message);

        LogAssert.Expect(LogType.Warning, new Regex("CatalogMissing"));

        string key = EnemyRoomTrackingKey.Resolve(
            scenePath,
            "MissingCatalogScene",
            "CommittedRoom",
            hasRoomManager: true,
            identity.Status,
            identity.Room,
            identity.Message);

        Assert.AreEqual("MissingCatalogScene", key);
    }

    [Test]
    public void InvalidCatalogFallsBackToSceneNameAndReportsDiagnostic()
    {
        const string scenePath = "Assets/TestRooms/InvalidCatalog.unity";
        RuntimeRoomCatalog catalog = ScriptableObject.CreateInstance<RuntimeRoomCatalog>();
        catalog.name = "Transient Invalid Enemy Tracking Catalog";
        try
        {
            catalog.SetGeneratedEntries(new List<RuntimeRoomCatalogEntry>());

            RoomIdentityResult identity = RoomIdentityResolver.ResolveByScenePath(scenePath, catalog);
            Assert.AreEqual(RoomIdentityStatus.CatalogInvalid, identity.Status, identity.Message);

            LogAssert.Expect(LogType.Warning, new Regex("CatalogInvalid"));

            string key = EnemyRoomTrackingKey.Resolve(
                scenePath,
                "InvalidCatalogScene",
                "CommittedRoom",
                hasRoomManager: true,
                identity.Status,
                identity.Room,
                identity.Message);

            Assert.AreEqual("InvalidCatalogScene", key);
        }
        finally
        {
            UnityEngine.Object.DestroyImmediate(catalog);
        }
    }

    [Test]
    public void UnavailableCatalogFallsBackToSceneNameAndReportsDiagnostic()
    {
        const string scenePath = "Assets/TestRooms/UnavailableCatalog.unity";

        LogAssert.Expect(LogType.Warning, new Regex("CatalogUnavailable"));

        string key = EnemyRoomTrackingKey.Resolve(
            scenePath,
            "UnavailableCatalogScene",
            "CommittedRoom",
            hasRoomManager: true,
            RoomIdentityStatus.CatalogUnavailable,
            null,
            "The catalog could not be loaded.");

        Assert.AreEqual("UnavailableCatalogScene", key);
    }

    [TestCase("Master")]
    [TestCase("DontDestroyOnLoad")]
    public void PersistentScenesUseCurrentRoomFallback(string sceneName)
    {
        Assert.AreEqual(
            "CommittedRoom",
            EnemyRoomTrackingKey.ResolveLegacyKey(sceneName, "CommittedRoom", true));

        string key = EnemyRoomTrackingKey.Resolve(
            string.Empty,
            sceneName,
            "CommittedRoom",
            hasRoomManager: true,
            RoomIdentityStatus.InvalidScenePath,
            null,
            string.Empty);

        Assert.AreEqual("CommittedRoom", key);
    }

    [TestCase("Master")]
    [TestCase("DontDestroyOnLoad")]
    [TestCase("")]
    [TestCase(null)]
    public void PersistentOrUnnamedScenesKeepLegacyFallbackEvenWithRegisteredPath(string sceneName)
    {
        Assert.AreEqual(
            "CommittedRoom",
            EnemyRoomTrackingKey.ResolveFromScene(
                RegisteredRoomPath, sceneName, "CommittedRoom", hasRoomManager: true));
    }

    [Test]
    public void ManagerUnavailableFallbackUsesUnknown()
    {
        Assert.AreEqual(
            "Unknown",
            EnemyRoomTrackingKey.ResolveLegacyKey("OrdinaryScene", null, hasRoomManager: false));

        string key = EnemyRoomTrackingKey.Resolve(
            string.Empty,
            "OrdinaryScene",
            currentRoom: null,
            hasRoomManager: false,
            RoomIdentityStatus.InvalidScenePath,
            null,
            string.Empty);

        Assert.AreEqual("Unknown", key);
    }
}
