using System.Collections.Generic;
using UnityEngine;

public sealed class TssCableRouteProvider : MonoBehaviour
{
    [SerializeField] private TssCableRouteNode[] routeNodes;

    private void Awake()
    {
        if (routeNodes == null || routeNodes.Length == 0)
            routeNodes = GetComponentsInChildren<TssCableRouteNode>(true);
    }

    public Vector3[] GetRoute(Vector3 start, Vector3 end)
    {
        if (routeNodes == null || routeNodes.Length == 0)
            return new[] { start, end };

        var startNode = Nearest(start);
        var endNode = Nearest(end);
        if (!startNode || !endNode)
            return new[] { start, end };

        var middle = ShortestPath(startNode, endNode);
        if (middle.Count == 0)
            return new[] { start, end };

        var points = new List<Vector3>(middle.Count + 2) { start };
        foreach (var node in middle)
            points.Add(node.transform.position);
        points.Add(end);
        return points.ToArray();
    }

    private TssCableRouteNode Nearest(Vector3 point)
    {
        TssCableRouteNode nearest = null;
        var best = float.PositiveInfinity;
        foreach (var node in routeNodes)
        {
            if (!node)
                continue;

            var distance = (node.transform.position - point).sqrMagnitude;
            if (distance >= best)
                continue;

            best = distance;
            nearest = node;
        }

        return nearest;
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
}
