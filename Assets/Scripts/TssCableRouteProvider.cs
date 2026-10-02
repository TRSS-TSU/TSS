using System.Collections.Generic;
using UnityEngine;

public sealed class TssCableRouteProvider : MonoBehaviour
{
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
        var startContext = EntryContext(startEndpoint);
        var endContext = EntryContext(endEndpoint);
        return GetRoute(
            startEndpoint.CableAnchorPosition,
            endEndpoint.CableAnchorPosition,
            startContext,
            endContext);
    }

    public Vector3[] GetRoute(Vector3 start, Vector3 end)
    {
        return GetRoute(start, end, default, default);
    }

    private Vector3[] GetRoute(Vector3 start, Vector3 end, RouteEntryContext preferredStart, RouteEntryContext preferredEnd)
    {
        EnsureRouteNodes();
        if (routeNodes == null || routeNodes.Length == 0)
            return new[] { start, end };

        var startNode = preferredStart.Node ? preferredStart.Node : preferredStart.HasFallback ? null : Nearest(start);
        var endNode = preferredEnd.Node ? preferredEnd.Node : preferredEnd.HasFallback ? null : Nearest(end);
        var startRoutePoint = startNode ? startNode.transform.position : preferredStart.FallbackPoint;
        var endRoutePoint = endNode ? endNode.transform.position : preferredEnd.FallbackPoint;
        if ((!startNode && !preferredStart.HasFallback) || (!endNode && !preferredEnd.HasFallback))
            return new[] { start, end };

        var middle = startNode && endNode ? ShortestPath(startNode, endNode) : new List<TssCableRouteNode>();
        if (startNode && endNode && middle.Count == 0)
            return new[] { start, end };

        var points = new List<Vector3>(middle.Count + 8) { start };
        AddEntryDress(points, start, startRoutePoint, preferredStart.IsRear);
        if (middle.Count > 0)
        {
            foreach (var node in middle)
                AddIfUseful(points, node.transform.position);
        }
        else
        {
            AddBridge(points, startRoutePoint, endRoutePoint);
        }

        AddExitDress(points, end, endRoutePoint, preferredEnd.IsRear);
        points.Add(end);
        return points.ToArray();
    }

    private void EnsureRouteNodes()
    {
        if (routeNodes == null || routeNodes.Length == 0)
            routeNodes = GetComponentsInChildren<TssCableRouteNode>(true);
    }

    private static void AddEntryDress(List<Vector3> points, Vector3 endpoint, Vector3 routeNode, bool isRear)
    {
        if (isRear)
        {
            AddIfUseful(points, new Vector3(routeNode.x, endpoint.y, endpoint.z));
            AddIfUseful(points, new Vector3(routeNode.x, routeNode.y, endpoint.z));
            return;
        }

        AddIfUseful(points, new Vector3(routeNode.x, endpoint.y, routeNode.z));
        AddIfUseful(points, routeNode);
    }

    private static void AddExitDress(List<Vector3> points, Vector3 endpoint, Vector3 routeNode, bool isRear)
    {
        if (isRear)
        {
            AddIfUseful(points, new Vector3(routeNode.x, routeNode.y, endpoint.z));
            AddIfUseful(points, new Vector3(routeNode.x, endpoint.y, endpoint.z));
            return;
        }

        AddIfUseful(points, routeNode);
        AddIfUseful(points, new Vector3(routeNode.x, endpoint.y, routeNode.z));
    }

    private static void AddBridge(List<Vector3> points, Vector3 startRoutePoint, Vector3 endRoutePoint)
    {
        AddIfUseful(points, startRoutePoint);
        AddIfUseful(points, new Vector3(startRoutePoint.x, endRoutePoint.y, startRoutePoint.z));
        AddIfUseful(points, new Vector3(endRoutePoint.x, endRoutePoint.y, startRoutePoint.z));
        AddIfUseful(points, endRoutePoint);
    }

    private static void AddIfUseful(List<Vector3> points, Vector3 point)
    {
        if (points.Count == 0 || (points[^1] - point).sqrMagnitude > 0.0001f)
            points.Add(point);
    }

    private RouteEntryContext EntryContext(TssPortEndpoint endpoint)
    {
        var endpointId = endpoint.EndpointId;
        if (TryRackId(endpointId, out var rackId))
        {
            var isRear = IsRearRackEndpoint(endpoint);
            var role = isRear ? TssCableRouteNodeRole.RackRear : TssCableRouteNodeRole.RackFront;
            var node = Nearest(endpoint.CableAnchorPosition, candidate => MatchesRack(candidate, rackId) && MatchesRole(candidate, role))
                ?? Nearest(endpoint.CableAnchorPosition, candidate => MatchesRack(candidate, rackId));
            if (!node)
                return new RouteEntryContext(RackFallbackPoint(endpoint, rackId, isRear), isRear);

            return new RouteEntryContext(node, isRear);
        }

        if (TryStationId(endpointId, out var stationId))
        {
            var node = Nearest(endpoint.CableAnchorPosition, candidate => MatchesStation(candidate, stationId) && MatchesRole(candidate, TssCableRouteNodeRole.StationDrop))
                ?? Nearest(endpoint.CableAnchorPosition, candidate => MatchesStation(candidate, stationId));
            return new RouteEntryContext(node, false);
        }

        return default;
    }

    private static Vector3 RackFallbackPoint(TssPortEndpoint endpoint, string rackId, bool isRear)
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
        var frontOffset = isRear ? 0f : 0.12f;
        return new Vector3(sideX, anchor.y, anchor.z + frontOffset);
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

    private static bool IsRearRackEndpoint(TssPortEndpoint endpoint)
    {
        if (endpoint.EndpointId.StartsWith("PatchPanel:", System.StringComparison.Ordinal))
            return false;

        for (var current = endpoint.transform; current; current = current.parent)
        {
            var name = current.name;
            if (Contains(name, "Router") || Contains(name, "Server") || Contains(name, "PDU"))
                return true;
            if (Contains(name, "Switch") || Contains(name, "Cisco") || Contains(name, "PatchPanel"))
                return false;
        }

        return false;
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
        public readonly bool IsRear;
        public readonly bool HasFallback;
        public readonly Vector3 FallbackPoint;

        public RouteEntryContext(TssCableRouteNode node, bool isRear)
        {
            Node = node;
            IsRear = isRear;
            HasFallback = false;
            FallbackPoint = default;
        }

        public RouteEntryContext(Vector3 fallbackPoint, bool isRear)
        {
            Node = null;
            IsRear = isRear;
            HasFallback = true;
            FallbackPoint = fallbackPoint;
        }
    }
}
