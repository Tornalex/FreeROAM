using System;
using System.Collections.Generic;
using UnityEngine;

public sealed class RoomGraphEdgeGeometry
{
    public RoomGraphEdgeGeometry(RoomGraphEdge edge)
    {
        Edge = edge;
    }

    public RoomGraphEdge Edge { get; }
    public Vector2 Start { get; internal set; }
    public Vector2 Control { get; internal set; }
    public Vector2 End { get; internal set; }
    public Vector2 Midpoint { get; internal set; }
    public bool ArrowAtStart { get; internal set; }
    public bool ArrowAtEnd { get; internal set; }

    public Vector2 Evaluate(float t)
    {
        float inverse = 1f - t;
        return inverse * inverse * Start +
               2f * inverse * t * Control +
               t * t * End;
    }
}

/// <summary>
/// Deterministic visual layout. It never reads or writes scene transforms,
/// coordinates or any connectivity data beyond the supplied graph model.
/// </summary>
public static class RoomConnectivityGraphLayout
{
    public const float NodeWidth = 230f;
    public const float NodeHeight = 150f;
    public const float HorizontalGap = 110f;
    public const float VerticalGap = 100f;
    public const float Margin = 60f;
    public const float MultiEdgeSpacing = 48f;
    public const float SocketSpacing = 15f;
    public const float PreviewTop = 42f;
    public const float PreviewBottom = 26f;
    public const float PreviewHorizontalPadding = 12f;
    public const float PreviewSocketPadding = 8f;

    public static RoomConnectivityGraphLayoutResult Compute(RoomConnectivityGraphModel model)
    {
        return Compute(model, null);
    }

    /// <summary>
    /// Deterministic layout with optional manual overrides. Nodes with a
    /// saved position keyed by <see cref="RoomGraphNode.NodeKey"/> use that
    /// position; every other node falls back to its deterministic grid cell.
    /// Reading the layout never modifies the supplied position data.
    /// </summary>
    public static RoomConnectivityGraphLayoutResult Compute(
        RoomConnectivityGraphModel model,
        IReadOnlyDictionary<string, Vector2> savedPositions)
    {
        RoomConnectivityGraphLayoutResult result = new RoomConnectivityGraphLayoutResult();

        if (model == null || model.Nodes.Count == 0)
        {
            result.ContentSize = new Vector2(800f, 600f);
            return result;
        }

        List<RoomGraphNode> sorted = SortNodesForLayout(model);
        Dictionary<RoomGraphNode, Vector2> automaticPositions =
            ComputeAutomaticPositions(sorted);

        float maxX = 0f;
        float maxY = 0f;

        for (int i = 0; i < sorted.Count; i++)
        {
            Vector2 position = automaticPositions[sorted[i]];

            if (savedPositions != null &&
                !string.IsNullOrEmpty(sorted[i].NodeKey) &&
                savedPositions.TryGetValue(sorted[i].NodeKey, out Vector2 saved) &&
                IsFinite(saved))
            {
                // Saved positions are written non-negative by the editor. A
                // corrupted or hand-edited negative position is clamped for
                // display without modifying the asset.
                position = new Vector2(Mathf.Max(0f, saved.x), Mathf.Max(0f, saved.y));
            }

            Vector2 nodeSize = GetNodeSize(sorted[i]);
            Rect rect = new Rect(position.x, position.y, nodeSize.x, nodeSize.y);
            result.NodeRects[sorted[i]] = rect;

            Rect previewRect = GetPreviewRect(rect);
            result.PreviewRects[sorted[i]] = previewRect;
            foreach (KeyValuePair<RoomGraphDoor, Vector2> socket in
                     ComputeDoorSocketPositions(sorted[i], previewRect))
            {
                result.DoorSocketPositions[socket.Key] = socket.Value;
            }

            maxX = Mathf.Max(maxX, rect.xMax);
            maxY = Mathf.Max(maxY, rect.yMax);
        }

        int contentWidth = Mathf.CeilToInt(maxX + Margin);
        int contentHeight = Mathf.CeilToInt(maxY + Margin);

        result.ContentSize = new Vector2(
            Mathf.Max(800f, contentWidth),
            Mathf.Max(600f, contentHeight));

        Dictionary<string, List<RoomGraphEdge>> groups =
            new Dictionary<string, List<RoomGraphEdge>>(StringComparer.Ordinal);

        foreach (RoomGraphEdge edge in model.Edges)
        {
            if (edge.SourceNode == null || edge.TargetNode == null ||
                ReferenceEquals(edge.SourceNode, edge.TargetNode))
            {
                continue;
            }

            if (!result.NodeRects.ContainsKey(edge.SourceNode) ||
                !result.NodeRects.ContainsKey(edge.TargetNode))
            {
                continue;
            }

            string key = PairKey(edge.SourceNode, edge.TargetNode);

            if (!groups.TryGetValue(key, out List<RoomGraphEdge> list))
            {
                list = new List<RoomGraphEdge>();
                groups.Add(key, list);
            }

            list.Add(edge);
        }

        foreach (KeyValuePair<string, List<RoomGraphEdge>> pair in groups)
        {
            pair.Value.Sort((a, b) => string.CompareOrdinal(a.AssetPath, b.AssetPath));

            int count = pair.Value.Count;

            for (int i = 0; i < count; i++)
            {
                RoomGraphEdge edge = pair.Value[i];
                Rect sourceRect = result.NodeRects[edge.SourceNode];
                Rect targetRect = result.NodeRects[edge.TargetNode];

                Vector2 sourceCenter = sourceRect.center;
                Vector2 targetCenter = targetRect.center;

                Vector2 start = edge.EndpointADoor != null && result.DoorSocketPositions.TryGetValue(
                    edge.EndpointADoor,
                    out Vector2 socketA)
                    ? socketA
                    : BoundaryPoint(sourceRect, targetCenter);
                Vector2 end = edge.EndpointBDoor != null && result.DoorSocketPositions.TryGetValue(
                    edge.EndpointBDoor,
                    out Vector2 socketB)
                    ? socketB
                    : BoundaryPoint(targetRect, sourceCenter);

                RoomGraphEdgeGeometry geometry = new RoomGraphEdgeGeometry(edge)
                {
                    Start = start,
                    End = end
                };

                Vector2 direction = targetCenter - sourceCenter;

                if (direction.sqrMagnitude < 0.0001f)
                    direction = Vector2.right;

                Vector2 normal = new Vector2(-direction.y, direction.x).normalized;
                float offset = (i - (count - 1) * 0.5f) * MultiEdgeSpacing;

                geometry.Control = (geometry.Start + geometry.End) * 0.5f + normal * offset;
                geometry.Midpoint = geometry.Evaluate(0.5f);

                if (!edge.HasDirectionError)
                {
                    geometry.ArrowAtEnd = true;
                    geometry.ArrowAtStart = edge.Direction == RoomConnectionDirection.Bidirectional;
                }

                result.EdgeGeometries[edge.Connection] = geometry;
            }
        }

        return result;
    }

    public static Vector2 Evaluate(RoomGraphEdgeGeometry geometry, float t)
    {
        return geometry.Evaluate(t);
    }

    /// <summary>
    /// Stable node ordering used by the automatic grid. The ordering is based
    /// only on the node's own display data and stable key, never on model
    /// instance identity or list order.
    /// </summary>
    public static List<RoomGraphNode> SortNodesForLayout(
        RoomConnectivityGraphModel model)
    {
        List<RoomGraphNode> sorted = new List<RoomGraphNode>();

        if (model == null)
            return sorted;

        sorted.AddRange(model.Nodes);
        sorted.Sort(CompareNodes);
        return sorted;
    }

    /// <summary>
    /// Deterministic grid cell for a node index, using the same spacing as the
    /// original automatic layout.
    /// </summary>
    public static Vector2 GetGridPosition(int index, int count)
    {
        int columns = Mathf.Max(1, Mathf.CeilToInt(Mathf.Sqrt(Mathf.Max(1, count))));
        int column = Mathf.Max(0, index) % columns;
        int row = Mathf.Max(0, index) / columns;

        return new Vector2(
            Margin + column * (NodeWidth + HorizontalGap),
            Margin + row * (NodeHeight + VerticalGap));
    }

    public static Vector2 GetNodeSize(RoomGraphNode node)
    {
        if (node == null || node.Doors == null || node.Doors.Count == 0)
            return new Vector2(NodeWidth, NodeHeight);

        GetSocketSideCounts(node, out int top, out int right, out int bottom, out int left);
        int horizontalSockets = Mathf.Max(top, bottom);
        int verticalSockets = Mathf.Max(left, right);

        float width = Mathf.Max(
            NodeWidth,
            PreviewHorizontalPadding * 2f +
            PreviewSocketPadding * 2f +
            Mathf.Max(0, horizontalSockets - 1) * SocketSpacing);
        float previewHeight = Mathf.Max(
            NodeHeight - PreviewTop - PreviewBottom,
            PreviewSocketPadding * 2f +
            Mathf.Max(0, verticalSockets - 1) * SocketSpacing);
        float height = Mathf.Max(NodeHeight, PreviewTop + previewHeight + PreviewBottom);

        return new Vector2(width, height);
    }

    public static Rect GetPreviewRect(Rect nodeRect)
    {
        return new Rect(
            nodeRect.x + PreviewHorizontalPadding,
            nodeRect.y + PreviewTop,
            Mathf.Max(1f, nodeRect.width - PreviewHorizontalPadding * 2f),
            Mathf.Max(1f, nodeRect.height - PreviewTop - PreviewBottom));
    }

    /// <summary>
    /// Returns node-local socket positions. Scene-derived normalized positions
    /// retain their relative order along each side; missing spatial data uses a
    /// stable DoorId/hierarchy-name fallback and remains explicitly flagged on
    /// the RoomGraphDoor for the UI to report.
    /// </summary>
    public static Dictionary<RoomGraphDoor, Vector2> ComputeDoorSocketPositions(
        RoomGraphNode node,
        Rect previewRect)
    {
        Dictionary<RoomGraphDoor, Vector2> result =
            new Dictionary<RoomGraphDoor, Vector2>();
        if (node == null || node.Doors == null || node.Doors.Count == 0)
            return result;

        Dictionary<RoomGraphDoor, Vector2> normalized = GetEffectiveDoorPositions(node);
        List<SocketRecord> top = new List<SocketRecord>();
        List<SocketRecord> right = new List<SocketRecord>();
        List<SocketRecord> bottom = new List<SocketRecord>();
        List<SocketRecord> left = new List<SocketRecord>();

        foreach (RoomGraphDoor door in node.Doors)
        {
            Vector2 position = normalized[door];
            float leftDistance = position.x;
            float rightDistance = 1f - position.x;
            float topDistance = 1f - position.y;
            float bottomDistance = position.y;
            float minimum = Mathf.Min(leftDistance, rightDistance, topDistance, bottomDistance);

            if (Mathf.Approximately(minimum, topDistance))
                top.Add(new SocketRecord(door, position.x));
            else if (Mathf.Approximately(minimum, rightDistance))
                right.Add(new SocketRecord(door, 1f - position.y));
            else if (Mathf.Approximately(minimum, bottomDistance))
                bottom.Add(new SocketRecord(door, 1f - position.x));
            else
                left.Add(new SocketRecord(door, 1f - position.y));
        }

        PlaceSide(top, previewRect.x, previewRect.xMax, previewRect.y, horizontal: true, result);
        PlaceSide(right, previewRect.y, previewRect.yMax, previewRect.xMax, horizontal: false, result);
        PlaceSide(bottom, previewRect.x, previewRect.xMax, previewRect.yMax, horizontal: true, result);
        PlaceSide(left, previewRect.y, previewRect.yMax, previewRect.x, horizontal: false, result);
        return result;
    }

    internal static Dictionary<RoomGraphNode, Vector2> ComputeAutomaticPositions(
        RoomConnectivityGraphModel model)
    {
        return ComputeAutomaticPositions(SortNodesForLayout(model));
    }

    private static Dictionary<RoomGraphNode, Vector2> ComputeAutomaticPositions(
        List<RoomGraphNode> sorted)
    {
        Dictionary<RoomGraphNode, Vector2> result =
            new Dictionary<RoomGraphNode, Vector2>();
        if (sorted == null || sorted.Count == 0)
            return result;

        int columns = Mathf.Max(1, Mathf.CeilToInt(Mathf.Sqrt(sorted.Count)));
        int rows = Mathf.CeilToInt(sorted.Count / (float)columns);
        float[] columnWidths = new float[columns];
        float[] rowHeights = new float[rows];

        for (int i = 0; i < sorted.Count; i++)
        {
            int column = i % columns;
            int row = i / columns;
            Vector2 size = GetNodeSize(sorted[i]);
            columnWidths[column] = Mathf.Max(columnWidths[column], size.x);
            rowHeights[row] = Mathf.Max(rowHeights[row], size.y);
        }

        float[] columnOffsets = new float[columns];
        float[] rowOffsets = new float[rows];
        for (int column = 1; column < columns; column++)
            columnOffsets[column] = columnOffsets[column - 1] + columnWidths[column - 1] + HorizontalGap;
        for (int row = 1; row < rows; row++)
            rowOffsets[row] = rowOffsets[row - 1] + rowHeights[row - 1] + VerticalGap;

        for (int i = 0; i < sorted.Count; i++)
        {
            int column = i % columns;
            int row = i / columns;
            result[sorted[i]] = new Vector2(Margin + columnOffsets[column], Margin + rowOffsets[row]);
        }

        return result;
    }

    private static Dictionary<RoomGraphDoor, Vector2> GetEffectiveDoorPositions(RoomGraphNode node)
    {
        Dictionary<RoomGraphDoor, Vector2> positions = new Dictionary<RoomGraphDoor, Vector2>();
        List<RoomGraphDoor> fallbackDoors = new List<RoomGraphDoor>();
        foreach (RoomGraphDoor door in node.Doors)
        {
            if (door.HasSpatialData && IsFinite(door.NormalizedScenePosition))
            {
                positions[door] = new Vector2(
                    Mathf.Clamp01(door.NormalizedScenePosition.x),
                    Mathf.Clamp01(door.NormalizedScenePosition.y));
            }
            else
            {
                fallbackDoors.Add(door);
            }
        }

        fallbackDoors.Sort((a, b) =>
        {
            int id = string.Compare(a.StableDoorId, b.StableDoorId, StringComparison.Ordinal);
            return id != 0
                ? id
                : string.Compare(a.GameObjectName, b.GameObjectName, StringComparison.Ordinal);
        });

        for (int i = 0; i < fallbackDoors.Count; i++)
            positions[fallbackDoors[i]] = GetFallbackPosition(i, fallbackDoors.Count);

        return positions;
    }

    private static Vector2 GetFallbackPosition(int index, int count)
    {
        if (count <= 0)
            return new Vector2(0.5f, 0.5f);

        float perimeter = (index + 0.5f) * 4f / count;
        int side = Mathf.Min(3, Mathf.FloorToInt(perimeter));
        float along = perimeter - side;
        switch (side)
        {
            case 0: return new Vector2(along, 1f);
            case 1: return new Vector2(1f, 1f - along);
            case 2: return new Vector2(1f - along, 0f);
            default: return new Vector2(0f, along);
        }
    }

    private static void GetSocketSideCounts(
        RoomGraphNode node,
        out int top,
        out int right,
        out int bottom,
        out int left)
    {
        top = right = bottom = left = 0;
        if (node == null)
            return;

        Dictionary<RoomGraphDoor, Vector2> normalized = GetEffectiveDoorPositions(node);
        foreach (Vector2 position in normalized.Values)
        {
            float leftDistance = position.x;
            float rightDistance = 1f - position.x;
            float topDistance = 1f - position.y;
            float bottomDistance = position.y;
            float minimum = Mathf.Min(leftDistance, rightDistance, topDistance, bottomDistance);
            if (Mathf.Approximately(minimum, topDistance)) top++;
            else if (Mathf.Approximately(minimum, rightDistance)) right++;
            else if (Mathf.Approximately(minimum, bottomDistance)) bottom++;
            else left++;
        }
    }

    private static void PlaceSide(
        List<SocketRecord> records,
        float start,
        float end,
        float fixedCoordinate,
        bool horizontal,
        Dictionary<RoomGraphDoor, Vector2> output)
    {
        if (records.Count == 0)
            return;

        records.Sort((a, b) =>
        {
            int position = a.NormalizedAlong.CompareTo(b.NormalizedAlong);
            if (position != 0)
                return position;
            int id = string.Compare(a.Door.StableDoorId, b.Door.StableDoorId, StringComparison.Ordinal);
            return id != 0 ? id : string.Compare(a.Door.GameObjectName, b.Door.GameObjectName, StringComparison.Ordinal);
        });

        float length = Mathf.Max(1f, end - start);
        float[] positions = new float[records.Count];
        for (int i = 0; i < records.Count; i++)
        {
            float desired = Mathf.Clamp01(records[i].NormalizedAlong) * length;
            float minimum = i == 0
                ? PreviewSocketPadding
                : positions[i - 1] + SocketSpacing;
            positions[i] = Mathf.Max(desired, minimum);
        }

        float maximum = length - PreviewSocketPadding;
        float overflow = positions[positions.Length - 1] - maximum;
        if (overflow > 0f)
        {
            for (int i = 0; i < positions.Length; i++)
                positions[i] -= overflow;
        }

        for (int i = 0; i < records.Count; i++)
        {
            float along = start + Mathf.Clamp(positions[i], PreviewSocketPadding, maximum);
            output[records[i].Door] = horizontal
                ? new Vector2(along, fixedCoordinate)
                : new Vector2(fixedCoordinate, along);
        }
    }

    private readonly struct SocketRecord
    {
        public SocketRecord(RoomGraphDoor door, float normalizedAlong)
        {
            Door = door;
            NormalizedAlong = normalizedAlong;
        }

        public RoomGraphDoor Door { get; }
        public float NormalizedAlong { get; }
    }

    private static bool IsFinite(Vector2 position)
    {
        return !float.IsNaN(position.x) && !float.IsInfinity(position.x) &&
               !float.IsNaN(position.y) && !float.IsInfinity(position.y);
    }

    private static int CompareNodes(RoomGraphNode a, RoomGraphNode b)
    {
        int display = string.CompareOrdinal(a.DisplayName, b.DisplayName);

        if (display != 0)
            return display;

        int roomId = string.CompareOrdinal(a.RoomId, b.RoomId);

        if (roomId != 0)
            return roomId;

        return string.CompareOrdinal(a.NodeKey, b.NodeKey);
    }

    private static string PairKey(RoomGraphNode a, RoomGraphNode b)
    {
        string first = a.NodeKey;
        string second = b.NodeKey;

        return string.CompareOrdinal(first, second) <= 0
            ? first + "\u001e" + second
            : second + "\u001e" + first;
    }

    private static Vector2 BoundaryPoint(Rect rect, Vector2 toward)
    {
        Vector2 center = rect.center;
        Vector2 direction = toward - center;

        if (direction.sqrMagnitude < 0.0001f)
            return center;

        float halfWidth = rect.width * 0.5f;
        float halfHeight = rect.height * 0.5f;

        float scaleX = Mathf.Abs(direction.x) < 0.0001f
            ? float.PositiveInfinity
            : halfWidth / Mathf.Abs(direction.x);

        float scaleY = Mathf.Abs(direction.y) < 0.0001f
            ? float.PositiveInfinity
            : halfHeight / Mathf.Abs(direction.y);

        float scale = Mathf.Min(scaleX, scaleY);
        return center + direction * scale;
    }
}

public sealed class RoomConnectivityGraphLayoutResult
{
    public Dictionary<RoomGraphNode, Rect> NodeRects { get; } =
        new Dictionary<RoomGraphNode, Rect>();

    public Dictionary<RoomGraphNode, Rect> PreviewRects { get; } =
        new Dictionary<RoomGraphNode, Rect>();

    public Dictionary<RoomGraphDoor, Vector2> DoorSocketPositions { get; } =
        new Dictionary<RoomGraphDoor, Vector2>();

    public Dictionary<RoomConnection, RoomGraphEdgeGeometry> EdgeGeometries { get; } =
        new Dictionary<RoomConnection, RoomGraphEdgeGeometry>();

    public Vector2 ContentSize { get; internal set; }
}
