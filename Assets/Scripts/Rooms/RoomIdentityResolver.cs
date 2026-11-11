using System;
using UnityEngine;

public enum RoomIdentityStatus
{
    Success = 0,
    InvalidScenePath = 1,
    CatalogMissing = 2,
    CatalogInvalid = 3,
    CatalogUnavailable = 4,
    UnknownScenePath = 5,
    InvalidRoomId = 6,
    UnknownRoomId = 7
}

/// <summary>
/// Explicit result for a runtime room identity lookup. The resolver never
/// throws for lookup failures; callers inspect <see cref="Status"/>.
/// </summary>
public sealed class RoomIdentityResult
{
    internal RoomIdentityResult(
        RoomIdentityStatus status,
        string message,
        RoomDefinition room = null,
        string scenePath = null)
    {
        Status = status;
        Message = message;
        Room = room;
        ScenePath = scenePath;
    }

    public RoomIdentityStatus Status { get; }
    public string Message { get; }
    public RoomDefinition Room { get; }
    public string ScenePath { get; }
    public bool IsSuccess => Status == RoomIdentityStatus.Success;
}

/// <summary>
/// Stateless runtime lookup from an exact scene path or RoomId to its
/// RoomDefinition, backed entirely by <see cref="RuntimeRoomCatalog"/>.
/// This class does not cache the catalog, normalize paths, or fall back to
/// scene names; those policy decisions belong to its callers.
/// </summary>
public static class RoomIdentityResolver
{
    public static RoomIdentityResult ResolveByScenePath(string scenePath)
    {
        if (string.IsNullOrWhiteSpace(scenePath))
            return Failure(RoomIdentityStatus.InvalidScenePath, "The scene path is null, empty or whitespace.");

        if (!TryLoadDefault(out RuntimeRoomCatalog catalog, out RoomIdentityResult failure))
            return failure;

        return ResolveByScenePath(scenePath, catalog);
    }

    public static RoomIdentityResult ResolveByRoomId(string roomId)
    {
        if (string.IsNullOrWhiteSpace(roomId))
            return Failure(RoomIdentityStatus.InvalidRoomId, "The RoomId is null, empty or whitespace.");

        if (!TryLoadDefault(out RuntimeRoomCatalog catalog, out RoomIdentityResult failure))
            return failure;

        return ResolveByRoomId(roomId, catalog);
    }

    public static RoomIdentityResult ResolveByScenePath(string scenePath, RuntimeRoomCatalog catalog)
    {
        if (string.IsNullOrWhiteSpace(scenePath))
            return Failure(RoomIdentityStatus.InvalidScenePath, "The scene path is null, empty or whitespace.");

        if (!TryValidateCatalog(catalog, out RoomIdentityResult failure))
            return failure;

        if (!catalog.TryGetRoomByScenePath(scenePath, out RoomDefinition definition))
        {
            return Failure(
                RoomIdentityStatus.UnknownScenePath,
                $"RuntimeRoomCatalog '{catalog.name}' has no room for scene path '{scenePath}'.");
        }

        return new RoomIdentityResult(RoomIdentityStatus.Success, string.Empty, definition, scenePath);
    }

    public static RoomIdentityResult ResolveByRoomId(string roomId, RuntimeRoomCatalog catalog)
    {
        if (string.IsNullOrWhiteSpace(roomId))
            return Failure(RoomIdentityStatus.InvalidRoomId, "The RoomId is null, empty or whitespace.");

        if (!TryValidateCatalog(catalog, out RoomIdentityResult failure))
            return failure;

        if (!catalog.TryGetRoom(roomId, out RoomDefinition definition, out string scenePath))
        {
            return Failure(
                RoomIdentityStatus.UnknownRoomId,
                $"RuntimeRoomCatalog '{catalog.name}' has no room with RoomId '{roomId}'.");
        }

        return new RoomIdentityResult(RoomIdentityStatus.Success, string.Empty, definition, scenePath);
    }

    private static bool TryLoadDefault(out RuntimeRoomCatalog catalog, out RoomIdentityResult failure)
    {
        try
        {
            catalog = RuntimeRoomCatalog.LoadDefault();
        }
        catch (InvalidOperationException exception)
        {
            catalog = null;
            failure = Failure(RoomIdentityStatus.CatalogUnavailable, exception.Message);
            return false;
        }

        failure = null;
        return true;
    }

    private static bool TryValidateCatalog(RuntimeRoomCatalog catalog, out RoomIdentityResult failure)
    {
        if (catalog == null)
        {
            failure = Failure(
                RoomIdentityStatus.CatalogMissing,
                "RuntimeRoomCatalog is missing; the room identity cannot be resolved.");
            return false;
        }

        if (!catalog.IsValid)
        {
            failure = Failure(
                RoomIdentityStatus.CatalogInvalid,
                $"RuntimeRoomCatalog '{catalog.name}' is invalid: {catalog.ValidationError}");
            return false;
        }

        failure = null;
        return true;
    }

    private static RoomIdentityResult Failure(RoomIdentityStatus status, string message)
    {
        return new RoomIdentityResult(status, message);
    }
}
