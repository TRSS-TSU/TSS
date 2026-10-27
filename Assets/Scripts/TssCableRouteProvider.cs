using System.Collections.Generic;
using UnityEngine;

public sealed class TssCableRouteProvider : MonoBehaviour
{
    public static readonly Vector3 VisualBreak = new(float.NaN, float.NaN, float.NaN);

    [SerializeField] private TssCableRouteNode[] routeNodes;

    private void Awake()
    {
        EnsureRouteNodes();
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

        AddOrthogonal(points, route.EndPoint, end);
        AddIfUseful(points, end);
        return points.ToArray();
    }

    private Vector3[] GetRackBackboneStubRoute(TssPortEndpoint startEndpoint, TssPortEndpoint endEndpoint, string startRackId, string endRackId)
    {
        var startEntries = EntryCandidates(startEndpoint);
        var endEntries = EntryCandidates(endEndpoint);
        var startStub = GetStubRoute(startEndpoint.CableAnchorPosition, startEntries, TrayCandidates(startRackId, PreferredSide(startEntries)));
        var endStub = GetStubRoute(endEndpoint.CableAnchorPosition, endEntries, TrayCandidates(endRackId, PreferredSide(endEntries)));
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
        var startStub = GetStubRoute(startEndpoint.CableAnchorPosition, startEntries, TrayCandidates(rackId, PreferredSide(startEntries)));
        var endStub = GetStubRoute(endEndpoint.CableAnchorPosition, endEntries, TrayCandidates(rackId, PreferredSide(endEntries)));
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
                if (!best.IsValid || cost < best.Cost)
                    best = new RouteSelection(startPoint, endPoint, middle, cost);
            }
        }

        return best;
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
        AddIfUseful(points, new Vector3(end.x, start.y, start.z));
        AddIfUseful(points, new Vector3(end.x, end.y, start.z));
        AddIfUseful(points, end);
    }

    private static void AddIfUseful(List<Vector3> points, Vector3 point)
    {
        if (points.Count == 0 || (points[^1] - point).sqrMagnitude > 0.0001f)
            points.Add(point);
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

    private static TssCableRouteSide PreferredSide(RouteEntryContext[] candidates)
    {
        return candidates != null && candidates.Length > 0 ? candidates[0].Side : TssCableRouteSide.Any;
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

            foreach (var neighbor in current.neighbors)
            {
                if (!neighbor)
                    continue;

                var nextDistance = distance[current] + Vector3.Distance(current.transform.position, neighbor.transform.position);
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
        while (previous.TryGetValue(path[0], out var parent))
            path.Insert(0, parent);
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
