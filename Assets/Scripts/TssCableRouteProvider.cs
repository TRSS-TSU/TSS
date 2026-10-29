using System.Collections.Generic;
using System.Linq;
using UnityEngine;

public sealed class TssCableRouteProvider : MonoBehaviour
{
    public static readonly Vector3 VisualBreak = new(float.NaN, float.NaN, float.NaN);

    [SerializeField] private TssCableRouteNode[] routeNodes;

    private void Awake()
    {
        EnsureRouteNodes();
    }

    [ContextMenu("Refresh Route Nodes")]
    public void RefreshRouteNodes()
    {
        routeNodes = GetComponentsInChildren<TssCableRouteNode>(true);
#if UNITY_EDITOR
        UnityEditor.EditorUtility.SetDirty(this);
#endif
    }

    [ContextMenu("Audit Route Topology")]
    public void AuditRouteTopology()
    {
        EnsureRouteNodes();
        Debug.Log(TssCableRouteTopologyAudit.Report(routeNodes));
    }

    public string GetRouteTopologyAuditReport()
    {
        EnsureRouteNodes();
        return TssCableRouteTopologyAudit.Report(routeNodes);
    }

    public Vector3[] GetRoute(TssPortEndpoint startEndpoint, TssPortEndpoint endEndpoint)
    {
        if (!startEndpoint || !endEndpoint)
            return new[] { startEndpoint ? startEndpoint.CableAnchorPosition : Vector3.zero, endEndpoint ? endEndpoint.CableAnchorPosition : Vector3.zero };

        EnsureRouteNodes();
        if (TryRackId(startEndpoint.EndpointId, out var startRackId)
            && TryRackId(endEndpoint.EndpointId, out var endRackId))
        {
            if (startRackId != endRackId)
                return GetRackBackboneStubRoute(startEndpoint, endEndpoint, startRackId, endRackId);

            return GetSameRackTrayRoute(startEndpoint, endEndpoint, startRackId);
        }

        return GetRoute(startEndpoint.CableAnchorPosition, endEndpoint.CableAnchorPosition, EntryCandidates(startEndpoint), EntryCandidates(endEndpoint));
    }

    public TssCableRouteResult GetRouteResult(TssPortEndpoint startEndpoint, TssPortEndpoint endEndpoint)
    {
        if (!startEndpoint || !endEndpoint)
            return TssCableRouteResult.Invalid(startEndpoint ? startEndpoint.EndpointId : string.Empty, endEndpoint ? endEndpoint.EndpointId : string.Empty, TssCableRouteKind.Unknown, "Missing route endpoint.");
        if (startEndpoint == endEndpoint || string.Equals(startEndpoint.EndpointId, endEndpoint.EndpointId, System.StringComparison.Ordinal))
            return TssCableRouteResult.Invalid(startEndpoint.EndpointId, endEndpoint.EndpointId, TssCableRouteKind.Unknown, "Route endpoints must be distinct.");

        EnsureRouteNodes();
        if (routeNodes == null || routeNodes.Length == 0)
            return TssCableRouteResult.Invalid(startEndpoint.EndpointId, endEndpoint.EndpointId, TssCableRouteKind.Unknown, "No authored route nodes are available.");

        if (TryRackId(startEndpoint.EndpointId, out var startRackId)
            && TryRackId(endEndpoint.EndpointId, out var endRackId)
            && !string.Equals(startRackId, endRackId, System.StringComparison.OrdinalIgnoreCase))
        {
            return TssCableRouteResult.Invalid(
                startEndpoint.EndpointId,
                endEndpoint.EndpointId,
                TssCableRouteKind.CrossRack,
                "Cross-rack student cable corridor is not enabled for the rounded renderer.",
                new TssCableRouteBoundary(TssCableRouteBoundaryKind.UnsupportedCrossRack, string.Empty, string.Empty, true));
        }

        var kind = TssCableRouteKind.Unknown;
        if (TryRackId(startEndpoint.EndpointId, out startRackId) && TryRackId(endEndpoint.EndpointId, out endRackId))
            kind = startEndpoint.InterfaceFace != endEndpoint.InterfaceFace ? TssCableRouteKind.FrontToRear : TssCableRouteKind.SameRack;
        else if (startEndpoint.EndpointId.StartsWith("Endpoint:", System.StringComparison.Ordinal)
            || endEndpoint.EndpointId.StartsWith("Endpoint:", System.StringComparison.Ordinal))
            kind = TssCableRouteKind.StationDrop;

        var selection = SelectRoute(
            startEndpoint.CableAnchorPosition,
            endEndpoint.CableAnchorPosition,
            EntryCandidates(startEndpoint),
            EntryCandidates(endEndpoint));
        if (!selection.IsValid)
            return TssCableRouteResult.Invalid(startEndpoint.EndpointId, endEndpoint.EndpointId, kind, "No valid authored graph route was found.");

        var points = BuildGraphRenderPoints(startEndpoint.CableAnchorPosition, endEndpoint.CableAnchorPosition, selection);
        if (points.Length < 2 || points.Any(point => !IsFinite(point)))
            return TssCableRouteResult.Invalid(startEndpoint.EndpointId, endEndpoint.EndpointId, kind, "The authored route produced non-finite render points.");

        var edges = BuildRouteEdges(selection.Middle);
        return TssCableRouteResult.Valid(startEndpoint.EndpointId, endEndpoint.EndpointId, kind, selection.Middle.ToArray(), edges, points);
    }

    public Vector3[] GetRoute(Vector3 start, Vector3 end)
    {
        return GetRoute(start, end, new RouteEntryContext(), new RouteEntryContext());
    }

    private Vector3[] GetRoute(Vector3 start, Vector3 end, RouteEntryContext preferredStart, RouteEntryContext preferredEnd)
    {
        return GetRoute(start, end, new[] { preferredStart }, new[] { preferredEnd });
    }

    private Vector3[] GetRoute(Vector3 start, Vector3 end, RouteEntryContext[] startCandidates, RouteEntryContext[] endCandidates)
    {
        EnsureRouteNodes();
        if (routeNodes == null || routeNodes.Length == 0)
            return new[] { start, end };

        var route = SelectRoute(start, end, startCandidates, endCandidates);
        if (!route.IsValid)
            return new[] { start, end };

        var points = new List<Vector3>(route.Middle.Count + 8) { start };
        AddOrthogonal(points, start, route.StartPoint);
        if (route.Middle.Count > 0)
        {
            foreach (var node in route.Middle)
                AddIfUseful(points, node.transform.position);
        }
        else
        {
            AddOrthogonal(points, route.StartPoint, route.EndPoint);
        }

        AddEndpointApproach(points, route.EndPoint, end);
        AddIfUseful(points, end);
        return points.ToArray();
    }

    private static Vector3[] BuildGraphRenderPoints(Vector3 start, Vector3 end, RouteSelection route)
    {
        var points = new List<Vector3>(route.Middle.Count + 8) { start };
        AddOrthogonal(points, start, route.StartPoint);
        if (route.Middle.Count > 0)
        {
            foreach (var node in route.Middle)
                if (node)
                    AddIfUseful(points, node.transform.position);
        }
        else
        {
            AddOrthogonal(points, route.StartPoint, route.EndPoint);
        }

        AddEndpointApproach(points, route.EndPoint, end);
        AddIfUseful(points, end);
        return points.ToArray();
    }

    private static IReadOnlyList<TssCableRouteEdge> BuildRouteEdges(IReadOnlyList<TssCableRouteNode> nodes)
    {
        if (nodes == null || nodes.Count < 2)
            return System.Array.Empty<TssCableRouteEdge>();

        var edges = new List<TssCableRouteEdge>(nodes.Count - 1);
        for (var i = 0; i < nodes.Count - 1; i++)
        {
            var from = nodes[i];
            var to = nodes[i + 1];
            if (!from || !to)
                continue;

            var fromKey = StableNodeKey(from);
            var toKey = StableNodeKey(to);
            var reciprocal = (to.neighbors ?? System.Array.Empty<TssCableRouteNode>()).Contains(from);
            var key = reciprocal && string.CompareOrdinal(fromKey, toKey) > 0
                ? $"{toKey}<->{fromKey}"
                : reciprocal
                    ? $"{fromKey}<->{toKey}"
                    : $"{fromKey}->{toKey}";
            edges.Add(new TssCableRouteEdge(from, to, key, reciprocal));
        }

        return edges;
    }

    private static string StableNodeKey(TssCableRouteNode node)
    {
        var path = NodePath(node.transform);
        return string.IsNullOrWhiteSpace(path) ? node.name : path;
    }

    private Vector3[] GetRackBackboneStubRoute(TssPortEndpoint startEndpoint, TssPortEndpoint endEndpoint, string startRackId, string endRackId)
    {
        var startEntries = EntryCandidates(startEndpoint);
        var endEntries = EntryCandidates(endEndpoint);
        var startStub = GetStubRoute(startEndpoint.CableAnchorPosition, startEntries, TrayCandidates(startRackId, PreferredSide(startEntries, startEndpoint.CableAnchorPosition)));
        var endStub = GetStubRoute(endEndpoint.CableAnchorPosition, endEntries, TrayCandidates(endRackId, PreferredSide(endEntries, endEndpoint.CableAnchorPosition)));
        if (startStub.Length == 0 || endStub.Length == 0)
            return GetRoute(startEndpoint.CableAnchorPosition, endEndpoint.CableAnchorPosition, EntryCandidates(startEndpoint), EntryCandidates(endEndpoint));

        var points = new List<Vector3>(startStub.Length + endStub.Length + 1);
        points.AddRange(startStub);
        points.Add(VisualBreak);
        points.AddRange(endStub);
        return points.ToArray();
    }

    private Vector3[] GetSameRackTrayRoute(TssPortEndpoint startEndpoint, TssPortEndpoint endEndpoint, string rackId)
    {
        var startEntries = EntryCandidates(startEndpoint);
        var endEntries = EntryCandidates(endEndpoint);
        var startStub = GetStubRoute(startEndpoint.CableAnchorPosition, startEntries, TrayCandidates(rackId, PreferredSide(startEntries, startEndpoint.CableAnchorPosition)));
        var endStub = GetStubRoute(endEndpoint.CableAnchorPosition, endEntries, TrayCandidates(rackId, PreferredSide(endEntries, endEndpoint.CableAnchorPosition)));
        if (startStub.Length == 0 || endStub.Length == 0)
            return GetRoute(startEndpoint.CableAnchorPosition, endEndpoint.CableAnchorPosition, EntryCandidates(startEndpoint), EntryCandidates(endEndpoint));

        var points = new List<Vector3>(startStub.Length + endStub.Length + 1);
        points.AddRange(startStub);
        points.Add(VisualBreak);
        points.AddRange(endStub);
        return points.ToArray();
    }

    private Vector3[] GetStubRoute(Vector3 endpoint, RouteEntryContext[] entryCandidates, RouteEntryContext[] trayCandidates)
    {
        var route = SelectRoute(endpoint, endpoint, entryCandidates, trayCandidates);
        if (!route.IsValid)
            return System.Array.Empty<Vector3>();

        var points = new List<Vector3>(route.Middle.Count + 4) { endpoint };
        var trayPoint = route.EndPoint;
        var sideDrop = new Vector3(route.StartPoint.x, trayPoint.y, endpoint.z);
        AddOrthogonal(points, endpoint, sideDrop);
        foreach (var node in route.Middle)
        {
            if (node && node.role == TssCableRouteNodeRole.Tray)
                AddIfUseful(points, node.transform.position);
        }
        AddIfUseful(points, route.EndPoint);
        return points.ToArray();
    }

    private void EnsureRouteNodes()
    {
        if (routeNodes == null || routeNodes.Length == 0)
            routeNodes = GetComponentsInChildren<TssCableRouteNode>(true);
    }

    private RouteSelection SelectRoute(Vector3 start, Vector3 end, RouteEntryContext[] startCandidates, RouteEntryContext[] endCandidates)
    {
        RouteSelection best = default;
        var starts = NormalizeCandidates(startCandidates, start);
        var ends = NormalizeCandidates(endCandidates, end);

        foreach (var startContext in starts)
        {
            foreach (var endContext in ends)
            {
                var middle = startContext.Node && endContext.Node ? ShortestPath(startContext.Node, endContext.Node) : new List<TssCableRouteNode>();
                if (startContext.Node && endContext.Node && middle.Count == 0)
                    continue;

                var startPoint = startContext.Point;
                var endPoint = endContext.Point;
                var cost = OrthogonalDistance(start, startPoint) + OrthogonalDistance(endPoint, end) + PathDistance(middle, startPoint, endPoint);
                if (IsNonNearestEntry(startContext, starts, start) || IsNonNearestEntry(endContext, ends, end))
                    cost += 10f;
                if (!best.IsValid || cost < best.Cost)
                    best = new RouteSelection(startPoint, endPoint, middle, cost);
            }
        }

        return best;
    }

    private static bool IsNonNearestEntry(RouteEntryContext context, RouteEntryContext[] candidates, Vector3 point)
    {
        if (candidates == null || candidates.Length < 2)
            return false;

        var distance = (context.Point - point).sqrMagnitude;
        foreach (var candidate in candidates)
            if ((candidate.Point - point).sqrMagnitude + 0.000001f < distance)
                return true;

        return false;
    }

    private RouteEntryContext[] NormalizeCandidates(RouteEntryContext[] candidates, Vector3 point)
    {
        if (candidates != null && candidates.Length > 0 && (candidates[0].Node || candidates[0].HasFallback))
            return candidates;

        var node = Nearest(point);
        return node ? new[] { new RouteEntryContext(node) } : System.Array.Empty<RouteEntryContext>();
    }

    private static float PathDistance(List<TssCableRouteNode> middle, Vector3 startPoint, Vector3 endPoint)
    {
        if (middle.Count == 0)
            return OrthogonalDistance(startPoint, endPoint);

        var total = OrthogonalDistance(startPoint, middle[0].transform.position);
        for (var i = 1; i < middle.Count; i++)
            total += OrthogonalDistance(middle[i - 1].transform.position, middle[i].transform.position);
        return total + OrthogonalDistance(middle[^1].transform.position, endPoint);
    }

    private static float OrthogonalDistance(Vector3 a, Vector3 b)
    {
        var delta = a - b;
        return Mathf.Abs(delta.x) + Mathf.Abs(delta.y) + Mathf.Abs(delta.z);
    }

    private static void AddOrthogonal(List<Vector3> points, Vector3 start, Vector3 end)
    {
        AddIfUseful(points, new Vector3(start.x, start.y, end.z));
        AddIfUseful(points, new Vector3(end.x, start.y, end.z));
        AddIfUseful(points, end);
    }

    private static void AddEndpointApproach(List<Vector3> points, Vector3 start, Vector3 end)
    {
        AddIfUseful(points, new Vector3(start.x, end.y, start.z));
        AddIfUseful(points, new Vector3(end.x, end.y, start.z));
        AddIfUseful(points, end);
    }

    private static void AddIfUseful(List<Vector3> points, Vector3 point)
    {
        if (points.Count == 0 || (points[^1] - point).sqrMagnitude > 0.0001f)
            points.Add(point);
    }

    private static bool IsFinite(Vector3 point)
    {
        return float.IsFinite(point.x) && float.IsFinite(point.y) && float.IsFinite(point.z);
    }

    private RouteEntryContext[] EntryCandidates(TssPortEndpoint endpoint)
    {
        var endpointId = endpoint.EndpointId;
        if (TryRackId(endpointId, out var rackId))
        {
            var role = endpoint.InterfaceFace == EquipmentInterfaceFace.Rear ? TssCableRouteNodeRole.RackRear : TssCableRouteNodeRole.RackFront;
            var candidates = new List<RouteEntryContext>();
            AddRackSideCandidate(candidates, endpoint.CableAnchorPosition, rackId, role, TssCableRouteSide.Left);
            AddRackSideCandidate(candidates, endpoint.CableAnchorPosition, rackId, role, TssCableRouteSide.Right);
            if (candidates.Count > 0)
                return candidates.ToArray();

            var node = Nearest(endpoint.CableAnchorPosition, candidate => MatchesRack(candidate, rackId) && MatchesRole(candidate, role))
                ?? Nearest(endpoint.CableAnchorPosition, candidate => MatchesRack(candidate, rackId));
            if (!node)
                return new[] { new RouteEntryContext(RackFallbackPoint(endpoint, rackId)) };

            return new[] { new RouteEntryContext(node) };
        }

        if (TryStationId(endpointId, out var stationId))
        {
            var node = Nearest(endpoint.CableAnchorPosition, candidate => MatchesStation(candidate, stationId) && MatchesRole(candidate, TssCableRouteNodeRole.StationDrop))
                ?? Nearest(endpoint.CableAnchorPosition, candidate => MatchesStation(candidate, stationId));
            return node ? new[] { new RouteEntryContext(node) } : System.Array.Empty<RouteEntryContext>();
        }

        if (endpointId.StartsWith("WallPort:", System.StringComparison.Ordinal))
        {
            // Wallports can terminate either the normal room tray or a dedicated device riser.
            // Keep both nearest candidates; SelectRoute discards disconnected paths.
            var candidates = new List<RouteEntryContext>();
            var normal = Nearest(endpoint.CableAnchorPosition, candidate => candidate.role != TssCableRouteNodeRole.StationDrop);
            var stationDrop = Nearest(endpoint.CableAnchorPosition, candidate => candidate.role == TssCableRouteNodeRole.StationDrop);
            if (normal)
                candidates.Add(new RouteEntryContext(normal));
            if (stationDrop && stationDrop != normal)
                candidates.Add(new RouteEntryContext(stationDrop));
            return candidates.ToArray();
        }

        return System.Array.Empty<RouteEntryContext>();
    }

    private static TssCableRouteSide PreferredSide(RouteEntryContext[] candidates, Vector3 point)
    {
        if (candidates == null || candidates.Length == 0)
            return TssCableRouteSide.Any;

        var bestSide = candidates[0].Side;
        var bestDist = (candidates[0].Point - point).sqrMagnitude;
        for (var i = 1; i < candidates.Length; i++)
        {
            var dist = (candidates[i].Point - point).sqrMagnitude;
            if (dist < bestDist)
            {
                bestDist = dist;
                bestSide = candidates[i].Side;
            }
        }
        return bestSide;
    }

    private RouteEntryContext[] TrayCandidates(string rackId, TssCableRouteSide side)
    {
        var candidates = new List<RouteEntryContext>();
        foreach (var node in routeNodes)
        {
            if (node && MatchesRack(node, rackId) && MatchesRole(node, TssCableRouteNodeRole.Tray) && MatchesSide(node, side))
                candidates.Add(new RouteEntryContext(node));
        }

        return candidates.ToArray();
    }

    private void AddRackSideCandidate(List<RouteEntryContext> candidates, Vector3 point, string rackId, TssCableRouteNodeRole role, TssCableRouteSide side)
    {
        var node = Nearest(point, candidate => MatchesRack(candidate, rackId) && MatchesRole(candidate, role) && MatchesSide(candidate, side));
        if (node)
            candidates.Add(new RouteEntryContext(node));
    }

    private static Vector3 RackFallbackPoint(TssPortEndpoint endpoint, string rackId)
    {
        var anchor = endpoint.CableAnchorPosition;
        var minX = anchor.x;
        var maxX = anchor.x;
        foreach (var candidate in FindObjectsByType<TssPortEndpoint>(FindObjectsInactive.Include, FindObjectsSortMode.None))
        {
            if (!candidate || !TryRackId(candidate.EndpointId, out var candidateRackId) || candidateRackId != rackId)
                continue;

            var candidateX = candidate.CableAnchorPosition.x;
            minX = Mathf.Min(minX, candidateX);
            maxX = Mathf.Max(maxX, candidateX);
        }

        var sideX = Mathf.Abs(anchor.x - minX) <= Mathf.Abs(anchor.x - maxX) ? minX : maxX;
        return new Vector3(sideX, anchor.y, anchor.z);
    }

    private TssCableRouteNode Nearest(Vector3 point)
    {
        return Nearest(point, null);
    }

    private TssCableRouteNode Nearest(Vector3 point, System.Predicate<TssCableRouteNode> filter)
    {
        TssCableRouteNode nearest = null;
        var best = float.PositiveInfinity;
        foreach (var node in routeNodes)
        {
            if (!node)
                continue;
            if (filter != null && !filter(node))
                continue;

            var distance = (node.transform.position - point).sqrMagnitude;
            if (distance >= best)
                continue;

            best = distance;
            nearest = node;
        }

        return nearest;
    }

    private static bool TryRackId(string endpointId, out string rackId)
    {
        rackId = string.Empty;
        if (string.IsNullOrWhiteSpace(endpointId))
            return false;

        const string rackPrefix = "Rack:";
        const string patchPrefix = "PatchPanel:";
        if (endpointId.StartsWith(rackPrefix, System.StringComparison.Ordinal))
            return TrySegment(endpointId, rackPrefix.Length, out rackId);
        if (endpointId.StartsWith(patchPrefix, System.StringComparison.Ordinal))
            return TrySegment(endpointId, patchPrefix.Length, out rackId);

        return false;
    }

    private static bool TryStationId(string endpointId, out string stationId)
    {
        stationId = string.Empty;
        const string prefix = "Endpoint:";
        return !string.IsNullOrWhiteSpace(endpointId)
            && endpointId.StartsWith(prefix, System.StringComparison.Ordinal)
            && TrySegment(endpointId, prefix.Length, out stationId);
    }

    private static bool TrySegment(string value, int start, out string segment)
    {
        var end = value.IndexOf(':', start);
        segment = (end < 0 ? value[start..] : value[start..end]).Trim();
        return !string.IsNullOrWhiteSpace(segment);
    }

    private static bool MatchesRack(TssCableRouteNode node, string rackId)
    {
        if (!string.IsNullOrWhiteSpace(node.rackId) && node.rackId == rackId)
            return true;

        return Contains(NodePath(node.transform), rackId);
    }

    private static bool MatchesStation(TssCableRouteNode node, string stationId)
    {
        if (!string.IsNullOrWhiteSpace(node.stationId) && node.stationId == stationId)
            return true;

        return Contains(NodePath(node.transform), stationId);
    }

    private static bool MatchesRole(TssCableRouteNode node, TssCableRouteNodeRole role)
    {
        if (node.role == role)
            return true;

        var path = NodePath(node.transform);
        return role switch
        {
            TssCableRouteNodeRole.RackFront => Contains(path, "Front") || Contains(path, "Tray"),
            TssCableRouteNodeRole.RackRear => Contains(path, "Rear"),
            TssCableRouteNodeRole.StationDrop => Contains(path, "OverheadDrop") || Contains(path, "Device"),
            TssCableRouteNodeRole.Tray => Contains(path, "Tray"),
            _ => true
        };
    }

    private static bool MatchesSide(TssCableRouteNode node, TssCableRouteSide side)
    {
        return side == TssCableRouteSide.Any || node.side == side;
    }

    private static string NodePath(Transform transform)
    {
        var path = transform.name;
        for (var current = transform.parent; current; current = current.parent)
            path = current.name + "/" + path;
        return path;
    }

    private static bool Contains(string value, string match)
    {
        return !string.IsNullOrWhiteSpace(value)
            && !string.IsNullOrWhiteSpace(match)
            && value.IndexOf(match, System.StringComparison.OrdinalIgnoreCase) >= 0;
    }

    private static List<TssCableRouteNode> ShortestPath(TssCableRouteNode start, TssCableRouteNode end)
    {
        var open = new List<TssCableRouteNode> { start };
        var distance = new Dictionary<TssCableRouteNode, float> { [start] = 0f };
        var previous = new Dictionary<TssCableRouteNode, TssCableRouteNode>();

        while (open.Count > 0)
        {
            open.Sort((left, right) => distance[left].CompareTo(distance[right]));
            var current = open[0];
            open.RemoveAt(0);

            if (current == end)
                return RebuildPath(previous, end);

            foreach (var neighbor in current.neighbors ?? System.Array.Empty<TssCableRouteNode>())
            {
                if (!neighbor)
                    continue;

                var edgeDistance = Vector3.Distance(current.transform.position, neighbor.transform.position);
                if (!float.IsFinite(edgeDistance))
                    continue;

                var nextDistance = distance[current] + edgeDistance;
                if (distance.TryGetValue(neighbor, out var knownDistance) && nextDistance >= knownDistance)
                    continue;

                distance[neighbor] = nextDistance;
                previous[neighbor] = current;
                if (!open.Contains(neighbor))
                    open.Add(neighbor);
            }
        }

        return new List<TssCableRouteNode>();
    }

    private static List<TssCableRouteNode> RebuildPath(Dictionary<TssCableRouteNode, TssCableRouteNode> previous, TssCableRouteNode end)
    {
        var path = new List<TssCableRouteNode> { end };
        var seen = new HashSet<TssCableRouteNode> { end };
        while (previous.TryGetValue(path[0], out var parent))
        {
            if (!seen.Add(parent))
                return new List<TssCableRouteNode>();
            path.Insert(0, parent);
        }
        return path;
    }

    private readonly struct RouteEntryContext
    {
        public readonly TssCableRouteNode Node;
        public readonly bool HasFallback;
        public readonly Vector3 FallbackPoint;
        public readonly TssCableRouteSide Side;
        public Vector3 Point => Node ? Node.transform.position : FallbackPoint;

        public RouteEntryContext(TssCableRouteNode node)
        {
            Node = node;
            HasFallback = false;
            FallbackPoint = default;
            Side = node ? node.side : TssCableRouteSide.Any;
        }

        public RouteEntryContext(Vector3 fallbackPoint)
        {
            Node = null;
            HasFallback = true;
            FallbackPoint = fallbackPoint;
            Side = TssCableRouteSide.Any;
        }
    }

    private readonly struct RouteSelection
    {
        public readonly Vector3 StartPoint;
        public readonly Vector3 EndPoint;
        public readonly List<TssCableRouteNode> Middle;
        public readonly float Cost;
        public bool IsValid => Middle != null;

        public RouteSelection(Vector3 startPoint, Vector3 endPoint, List<TssCableRouteNode> middle, float cost)
        {
            StartPoint = startPoint;
            EndPoint = endPoint;
            Middle = middle;
            Cost = cost;
        }
    }
}
