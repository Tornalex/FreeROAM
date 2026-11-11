using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using NUnit.Framework;

/// <summary>
/// In-memory tests for RoomIdentityResolver. These tests use a transient
/// RuntimeRoomCatalog and never read or modify the real Resources catalog.
/// </summary>
[TestFixture]
public class RoomIdentityResolverTests
{
    private const string RoomAId = "room-alpha";
    private const string RoomAPath = "Assets/TestRooms/Alpha.unity";
    private const string RoomBId = "room-beta";
    private const string RoomBPath = "Assets/TestRooms/Subfolder/Beta.unity";

    private sealed class Fixture : IDisposable
    {
        private readonly List<UnityEngine.Object> _objects = new List<UnityEngine.Object>();
        private bool _disposed;

        public readonly RoomDefinition RoomA;
        public readonly RoomDefinition RoomB;
        public readonly RuntimeRoomCatalog Catalog;

        public Fixture()
        {
            RoomA = Definition(RoomAId, "Alpha");
            RoomB = Definition(RoomBId, "Beta");

            Catalog = ScriptableObject.CreateInstance<RuntimeRoomCatalog>();
            Catalog.name = "Transient Room Identity Test Catalog";
            _objects.Add(Catalog);
            Catalog.SetGeneratedEntries(new List<RuntimeRoomCatalogEntry>
            {
                new RuntimeRoomCatalogEntry(RoomA, RoomAPath),
                new RuntimeRoomCatalogEntry(RoomB, RoomBPath)
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

        private RoomDefinition Definition(string id, string sceneName)
        {
            RoomDefinition definition = ScriptableObject.CreateInstance<RoomDefinition>();
            definition.name = sceneName;
            definition.AssignIdentity(id, sceneName, sceneName, "test-guid-" + id);
            _objects.Add(definition);
            return definition;
        }
    }

    [Test]
    public void ExactScenePathResolvesRoomDefinition()
    {
        using (Fixture fixture = new Fixture())
        {
            RoomIdentityResult result = RoomIdentityResolver.ResolveByScenePath(RoomAPath, fixture.Catalog);

            Assert.AreEqual(RoomIdentityStatus.Success, result.Status);
            Assert.IsTrue(result.IsSuccess);
            Assert.AreSame(fixture.RoomA, result.Room);
            Assert.AreEqual(RoomAPath, result.ScenePath);
        }
    }

    [Test]
    public void RoomIdResolvesDefinitionAndScenePath()
    {
        using (Fixture fixture = new Fixture())
        {
            RoomIdentityResult result = RoomIdentityResolver.ResolveByRoomId(RoomBId, fixture.Catalog);

            Assert.AreEqual(RoomIdentityStatus.Success, result.Status);
            Assert.IsTrue(result.IsSuccess);
            Assert.AreSame(fixture.RoomB, result.Room);
            Assert.AreEqual(RoomBPath, result.ScenePath);
        }
    }

    [Test]
    public void UnknownScenePathReturnsUnknownStatus()
    {
        using (Fixture fixture = new Fixture())
        {
            RoomIdentityResult result = RoomIdentityResolver.ResolveByScenePath(
                "Assets/TestRooms/Other/Alpha.unity", fixture.Catalog);

            Assert.AreEqual(RoomIdentityStatus.UnknownScenePath, result.Status);
            Assert.IsFalse(result.IsSuccess);
            Assert.IsNull(result.Room);
            Assert.IsNull(result.ScenePath);
        }
    }

    [Test]
    public void UnknownRoomIdReturnsUnknownStatus()
    {
        using (Fixture fixture = new Fixture())
        {
            RoomIdentityResult result = RoomIdentityResolver.ResolveByRoomId("missing-room", fixture.Catalog);

            Assert.AreEqual(RoomIdentityStatus.UnknownRoomId, result.Status);
            Assert.IsFalse(result.IsSuccess);
            Assert.IsNull(result.Room);
            Assert.IsNull(result.ScenePath);
        }
    }

    [TestCase(null)]
    [TestCase("")]
    [TestCase("   ")]
    public void BlankScenePathReturnsInvalidScenePath(string scenePath)
    {
        using (Fixture fixture = new Fixture())
        {
            RoomIdentityResult result = RoomIdentityResolver.ResolveByScenePath(scenePath, fixture.Catalog);

            Assert.AreEqual(RoomIdentityStatus.InvalidScenePath, result.Status);
            Assert.IsFalse(result.IsSuccess);
            Assert.IsNull(result.Room);
        }
    }

    [TestCase(null)]
    [TestCase("")]
    [TestCase("   ")]
    public void BlankRoomIdReturnsInvalidRoomId(string roomId)
    {
        using (Fixture fixture = new Fixture())
        {
            RoomIdentityResult result = RoomIdentityResolver.ResolveByRoomId(roomId, fixture.Catalog);

            Assert.AreEqual(RoomIdentityStatus.InvalidRoomId, result.Status);
            Assert.IsFalse(result.IsSuccess);
            Assert.IsNull(result.Room);
        }
    }

    [Test]
    public void NullCatalogReturnsCatalogMissing()
    {
        RoomIdentityResult byPath = RoomIdentityResolver.ResolveByScenePath(RoomAPath, null);
        RoomIdentityResult byId = RoomIdentityResolver.ResolveByRoomId(RoomAId, null);

        Assert.AreEqual(RoomIdentityStatus.CatalogMissing, byPath.Status);
        Assert.AreEqual(RoomIdentityStatus.CatalogMissing, byId.Status);
        Assert.IsFalse(byPath.IsSuccess);
        Assert.IsFalse(byId.IsSuccess);
        Assert.IsNull(byPath.Room);
        Assert.IsNull(byId.Room);
    }

    [Test]
    public void InvalidCatalogReturnsCatalogInvalidAndPreservesMessage()
    {
        RuntimeRoomCatalog catalog = ScriptableObject.CreateInstance<RuntimeRoomCatalog>();
        catalog.name = "Transient Invalid Catalog";
        try
        {
            catalog.SetGeneratedEntries(new List<RuntimeRoomCatalogEntry>());

            RoomIdentityResult byPath = RoomIdentityResolver.ResolveByScenePath(RoomAPath, catalog);
            RoomIdentityResult byId = RoomIdentityResolver.ResolveByRoomId(RoomAId, catalog);

            Assert.AreEqual(RoomIdentityStatus.CatalogInvalid, byPath.Status);
            Assert.AreEqual(RoomIdentityStatus.CatalogInvalid, byId.Status);
            StringAssert.Contains(catalog.ValidationError, byPath.Message);
            StringAssert.Contains(catalog.ValidationError, byId.Message);
            Assert.IsNull(byPath.Room);
            Assert.IsNull(byId.Room);
        }
        finally
        {
            UnityEngine.Object.DestroyImmediate(catalog);
        }
    }

    [Test]
    public void LookupIsCaseSensitiveAndExact()
    {
        using (Fixture fixture = new Fixture())
        {
            RoomIdentityResult lowerPath = RoomIdentityResolver.ResolveByScenePath(
                "assets/testrooms/alpha.unity", fixture.Catalog);
            RoomIdentityResult wrongFileNameCase = RoomIdentityResolver.ResolveByScenePath(
                "Assets/TestRooms/alpha.unity", fixture.Catalog);
            RoomIdentityResult upperRoomId = RoomIdentityResolver.ResolveByRoomId(
                "ROOM-ALPHA", fixture.Catalog);

            Assert.AreEqual(RoomIdentityStatus.UnknownScenePath, lowerPath.Status);
            Assert.AreEqual(RoomIdentityStatus.UnknownScenePath, wrongFileNameCase.Status);
            Assert.AreEqual(RoomIdentityStatus.UnknownRoomId, upperRoomId.Status);

            RoomIdentityResult exact = RoomIdentityResolver.ResolveByScenePath(RoomAPath, fixture.Catalog);
            Assert.AreEqual(RoomIdentityStatus.Success, exact.Status);
        }
    }

    [Test]
    public void SceneNameIsNotAcceptedAsIdentity()
    {
        using (Fixture fixture = new Fixture())
        {
            Assert.AreEqual("Alpha", fixture.RoomA.SceneName);

            RoomIdentityResult bySceneName = RoomIdentityResolver.ResolveByScenePath("Alpha", fixture.Catalog);
            RoomIdentityResult byPathWithoutExtension = RoomIdentityResolver.ResolveByScenePath(
                "Assets/TestRooms/Alpha", fixture.Catalog);
            RoomIdentityResult roomIdAsSceneName = RoomIdentityResolver.ResolveByRoomId("Alpha", fixture.Catalog);

            Assert.AreEqual(RoomIdentityStatus.UnknownScenePath, bySceneName.Status);
            Assert.AreEqual(RoomIdentityStatus.UnknownScenePath, byPathWithoutExtension.Status);
            Assert.AreEqual(RoomIdentityStatus.UnknownRoomId, roomIdAsSceneName.Status);
        }
    }
}
