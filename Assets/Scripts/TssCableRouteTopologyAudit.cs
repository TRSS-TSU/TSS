using System.Collections.Generic;
using System.Linq;
using System.Text;
using UnityEngine;

public static class TssCableRouteTopologyAudit
{
    public static string Report(IEnumerable<TssCableRouteNode> source)
    {
        var nodes = (source ?? Enumerable.Empty<TssCableRouteNode>()).Where(x => x).ToArray();
        var index = nodes.Select((node, i) => new { node, i }).ToDictionary(x => x.node, x => x.i);
        var directedEdges = 0;
        var nullNeighbors = 0;
        var duplicateNeighbors = 0;
        var missingReverse = 0;
        var selfLinks = 0;
        var nonFinitePositions = 0;
        var untypedRackNodes = 0;
        var roleTransitions = new Dictionary<string, int>();
        var adjacency = nodes.ToDictionary(x => x, _ => new HashSet<TssCableRouteNode>());

        foreach (var node in nodes)
        {
            if (!IsFinite(node.transform.position))
                nonFinitePositions++;

            var neighbors = node.neighbors ?? System.Array.Empty<TssCableRouteNode>();
            var seen = new HashSet<TssCableRouteNode>();
            foreach (var neighbor in neighbors)
            {
                if (!neighbor)
                {
                    nullNeighbors++;
                    continue;
                }

                if (!seen.Add(neighbor))
                    duplicateNeighbors++;
                if (neighbor == node)
                    selfLinks++;
                if (!adjacency.ContainsKey(neighbor))
                    continue;

                directedEdges++;
                adjacency[node].Add(neighbor);
                adjacency[neighbor].Add(node);
                if (!(neighbor.neighbors ?? System.Array.Empty<TssCableRouteNode>()).Contains(node))
                    missingReverse++;

                var transition = $"{node.role}->{neighbor.role}";
                if (node.role != neighbor.role)
                    roleTransitions[transition] = roleTransitions.TryGetValue(transition, out var count) ? count + 1 : 1;
            }

            if (IsRackGrid(node.transform) && (string.IsNullOrWhiteSpace(node.rackId) || node.role == TssCableRouteNodeRole.Any || node.side == TssCableRouteSide.Any))
                untypedRackNodes++;
        }

        var components = CountComponents(nodes, adjacency, out var largestComponent);
        var grouped = nodes
            .GroupBy(node => $"{node.rackId}|{node.role}|{node.side}")
            .OrderBy(group => group.Key)
            .Select(group => $"  {group.Key}={group.Count()}");

        var report = new StringBuilder();
        report.AppendLine("TSS CABLE ROUTE TOPOLOGY AUDIT");
        report.AppendLine($"Nodes: {nodes.Length}");
        report.AppendLine($"Directed neighbor entries: {directedEdges}");
        report.AppendLine($"Null neighbors: {nullNeighbors}");
        report.AppendLine($"Duplicate neighbor entries: {duplicateNeighbors}");
        report.AppendLine($"Missing reverse entries: {missingReverse}");
        report.AppendLine($"Self links: {selfLinks}");
        report.AppendLine($"Non-finite positions: {nonFinitePositions}");
        report.AppendLine($"Untyped rack-grid nodes: {untypedRackNodes}");
        report.AppendLine($"Undirected connected components: {components}");
        report.AppendLine($"Largest component: {largestComponent}");
        report.AppendLine("Role transitions:");
        foreach (var transition in roleTransitions.OrderBy(x => x.Key))
            report.AppendLine($"  {transition.Key}={transition.Value}");
        report.AppendLine("Rack/role/side groups:");
        foreach (var group in grouped)
            report.AppendLine(group);
        return report.ToString();
    }

    private static int CountComponents(TssCableRouteNode[] nodes, Dictionary<TssCableRouteNode, HashSet<TssCableRouteNode>> adjacency, out int largest)
    {
        var unseen = new HashSet<TssCableRouteNode>(nodes);
        var components = 0;
        largest = 0;
        while (unseen.Count > 0)
        {
            var queue = new Queue<TssCableRouteNode>();
            var start = unseen.First();
            unseen.Remove(start);
            queue.Enqueue(start);
            var size = 0;
            while (queue.Count > 0)
            {
                var current = queue.Dequeue();
                size++;
                foreach (var next in adjacency[current])
                    if (unseen.Remove(next))
                        queue.Enqueue(next);
            }

            components++;
            largest = Mathf.Max(largest, size);
        }

        return components;
    }

    private static bool IsRackGrid(Transform transform)
    {
        var path = transform.name;
        for (var current = transform.parent; current; current = current.parent)
            path = current.name + "/" + path;
        return path.IndexOf("Rack01_FrontGrid", System.StringComparison.OrdinalIgnoreCase) >= 0
            || path.IndexOf("Rack01_RearGrid", System.StringComparison.OrdinalIgnoreCase) >= 0
            || path.IndexOf("Rack02_FrontGrid", System.StringComparison.OrdinalIgnoreCase) >= 0
            || path.IndexOf("Rack02_RearGrid", System.StringComparison.OrdinalIgnoreCase) >= 0;
    }

    private static bool IsFinite(Vector3 point)
    {
        return float.IsFinite(point.x) && float.IsFinite(point.y) && float.IsFinite(point.z);
    }
}
