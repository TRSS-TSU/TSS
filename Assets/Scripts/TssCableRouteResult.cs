using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

public enum TssCableRouteKind
{
    Unknown,
    SameRack,
    FrontToRear,
    StationDrop,
    CrossRack,
}

public enum TssCableRouteBoundaryKind
{
    None,
    LegacyVisualBreak,
    UnsupportedCrossRack,
}

public readonly struct TssCableRouteEdge
{
    public readonly TssCableRouteNode From;
    public readonly TssCableRouteNode To;
    public readonly string Key;
    public readonly bool IsBidirectional;

    public TssCableRouteEdge(TssCableRouteNode from, TssCableRouteNode to, string key, bool isBidirectional)
    {
        From = from;
        To = to;
        Key = key;
        IsBidirectional = isBidirectional;
    }
}

public readonly struct TssCableRouteBoundary
{
    public readonly TssCableRouteBoundaryKind Kind;
    public readonly string PreviousEdgeKey;
    public readonly string NextEdgeKey;
    public readonly bool RenderContinuityRequired;

    public TssCableRouteBoundary(TssCableRouteBoundaryKind kind, string previousEdgeKey, string nextEdgeKey, bool renderContinuityRequired)
    {
        Kind = kind;
        PreviousEdgeKey = previousEdgeKey;
        NextEdgeKey = nextEdgeKey;
        RenderContinuityRequired = renderContinuityRequired;
    }
}

public sealed class TssCableRouteResult
{
    public bool IsValid { get; }
    public string FailureReason { get; }
    public string StartEndpointId { get; }
    public string EndEndpointId { get; }
    public TssCableRouteKind Kind { get; }
    public IReadOnlyList<TssCableRouteNode> OrderedNodes { get; }
    public IReadOnlyList<TssCableRouteEdge> Edges { get; }
    public IReadOnlyList<Vector3> RenderPoints { get; }
    public IReadOnlyList<TssCableRouteBoundary> Boundaries { get; }

    public bool HasRenderContinuityBoundary => Boundaries.Any(x => x.RenderContinuityRequired);

    private TssCableRouteResult(
        bool isValid,
        string failureReason,
        string startEndpointId,
        string endEndpointId,
        TssCableRouteKind kind,
        IReadOnlyList<TssCableRouteNode> orderedNodes,
        IReadOnlyList<TssCableRouteEdge> edges,
        IReadOnlyList<Vector3> renderPoints,
        IReadOnlyList<TssCableRouteBoundary> boundaries)
    {
        IsValid = isValid;
        FailureReason = failureReason ?? string.Empty;
        StartEndpointId = startEndpointId ?? string.Empty;
        EndEndpointId = endEndpointId ?? string.Empty;
        Kind = kind;
        OrderedNodes = orderedNodes ?? Array.Empty<TssCableRouteNode>();
        Edges = edges ?? Array.Empty<TssCableRouteEdge>();
        RenderPoints = renderPoints ?? Array.Empty<Vector3>();
        Boundaries = boundaries ?? Array.Empty<TssCableRouteBoundary>();
    }

    public static TssCableRouteResult Valid(
        string startEndpointId,
        string endEndpointId,
        TssCableRouteKind kind,
        IReadOnlyList<TssCableRouteNode> nodes,
        IReadOnlyList<TssCableRouteEdge> edges,
        IReadOnlyList<Vector3> renderPoints)
    {
        return new TssCableRouteResult(true, string.Empty, startEndpointId, endEndpointId, kind, nodes, edges, renderPoints, Array.Empty<TssCableRouteBoundary>());
    }

    public static TssCableRouteResult Invalid(
        string startEndpointId,
        string endEndpointId,
        TssCableRouteKind kind,
        string failureReason,
        TssCableRouteBoundary boundary = default)
    {
        var boundaries = boundary.Kind == TssCableRouteBoundaryKind.None
            ? Array.Empty<TssCableRouteBoundary>()
            : new[] { boundary };
        return new TssCableRouteResult(false, failureReason, startEndpointId, endEndpointId, kind, Array.Empty<TssCableRouteNode>(), Array.Empty<TssCableRouteEdge>(), Array.Empty<Vector3>(), boundaries);
    }
}
