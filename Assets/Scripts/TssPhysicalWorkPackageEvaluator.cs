using System;
using System.Linq;

public sealed class TssPhysicalWorkPackageEvaluator
{
    private readonly TssTrainingSession _session;
    private readonly TssScenarioDefinition _scenario;

    public TssPhysicalWorkPackageEvaluator(TssTrainingSession session)
    {
        _session = session;
        _scenario = session ? session.Scenario : null;
    }

    public TssWorkPackageResult Evaluate()
    {
        var result = new TssWorkPackageResult();
        if (!_session || !_scenario)
        {
            result.Add("WP1.Session", "No scenario or training session is loaded.");
            return result;
        }

        var hasRequirements = false;
        foreach (var status in TssPlacementEvaluator.EvaluateRequirements(_scenario, _session.Installed, _session.EndpointPlacements))
        {
            hasRequirements = true;
            var isRack = status.Location.IndexOf(" U", StringComparison.Ordinal) > 0;
            var id = isRack
                ? $"WP1.Placement.Rack:{status.Location.Split(' ')[0]}:{status.RequiredName}"
                : $"WP1.Placement.Endpoint:{status.Location}:{status.RequiredName}";
            Add(result, status.IsComplete, id, $"{status.RequiredName} at {status.Location}");
        }

        var requirements = _scenario.cableTypeRequirements ?? Array.Empty<ScenarioCableTypeRequirement>();
        var statuses = _session.GetCableTypeStatuses();
        foreach (var requirement in requirements)
        {
            if (requirement == null || !string.IsNullOrWhiteSpace(requirement.crossoverGroupId))
                continue;

            hasRequirements = true;
            var status = FindStatus(requirement, statuses);
            var id = "WP1.CableTypes." + (string.IsNullOrWhiteSpace(requirement.requirementId) ? "Required" : requirement.requirementId);
            var endpoints = requirement.endpointAId + " to " + requirement.endpointBId;
            var complete = status.IsConnected && status.IsCorrect;
            if (complete)
                Add(result, true, id, $"{endpoints} uses the required cable type.");
            else
                result.Add(id, status.IsConnected
                    ? $"{endpoints} uses {status.ActualCableType}; required {status.RequiredCableType}."
                    : $"{endpoints} is not connected with the required {requirement.requiredCableType} cable.");
        }

        var groupedConnections = new System.Collections.Generic.HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var crossoverGroups = requirements.Where(x => x != null && !string.IsNullOrWhiteSpace(x.crossoverGroupId))
            .GroupBy(x => x.crossoverGroupId, StringComparer.OrdinalIgnoreCase).ToArray();
        foreach (var group in crossoverGroups)
        {
            hasRequirements = true;
            var pair = group.Select(x => FindStatus(x, statuses)).ToArray();
            foreach (var status in pair)
                if (status.IsConnected) groupedConnections.Add(status.ConnectionId);
            var complete = pair.Length == 2 && pair.All(x => x.IsConnected)
                && pair.Count(x => x.ActualCableType == TssCableType.Crossover) == 1
                && pair.Count(x => x.ActualCableType == TssCableType.StraightThrough) == 1;
            Add(result, complete, "WP1.CableTypes.CrossoverGroup." + group.Key,
                $"{group.Key} uses one crossover and one straight-through patch cord.");
        }
        if (crossoverGroups.Length > 0)
        {
            var othersStraight = statuses.Where(x => x.IsConnected && !groupedConnections.Contains(x.ConnectionId))
                .All(x => x.ActualCableType == TssCableType.StraightThrough);
            Add(result, othersStraight, "WP1.CableTypes.OtherCables", "All other player cables are straight-through.");
        }

        foreach (var path in _scenario.expectedEffectivePaths ?? Array.Empty<ScenarioEffectivePathRequirement>())
        {
            if (path == null)
                continue;

            hasRequirements = true;
            var id = string.IsNullOrWhiteSpace(path.requirementId) ? "Required" : path.requirementId;
            Add(result, _session.HasEffectivePhysicalPath(path.endpointAId, path.endpointBId),
                "WP1.EffectivePaths." + id, $"Effective path {path.endpointAId} to {path.endpointBId} is connected.");
        }

        if (!hasRequirements)
            result.Add("WP1.Requirements", "No physical installation requirements are defined for this scenario.");
        return result;
    }

    private TssCableTypeStatus FindStatus(ScenarioCableTypeRequirement requirement, System.Collections.Generic.IReadOnlyList<TssCableTypeStatus> statuses)
    {
        var a = _session.ResolveScenarioEndpointId(requirement.endpointAId);
        var b = _session.ResolveScenarioEndpointId(requirement.endpointBId);
        return statuses.FirstOrDefault(x =>
            string.Equals(x.ConnectionId, requirement.requirementId, StringComparison.OrdinalIgnoreCase)
            || EndpointPairMatches(x.EndpointAId, x.EndpointBId, a, b));
    }

    private static void Add(TssWorkPackageResult result, bool complete, string id, string label)
    {
        if (complete)
        {
            result.Attempt(id);
            result.Pass(id, label);
        }
        else
            result.Add(id, label);
    }

    private static bool EndpointPairMatches(string firstA, string firstB, string secondA, string secondB)
    {
        return (string.Equals(firstA, secondA, StringComparison.OrdinalIgnoreCase)
                && string.Equals(firstB, secondB, StringComparison.OrdinalIgnoreCase))
            || (string.Equals(firstA, secondB, StringComparison.OrdinalIgnoreCase)
                && string.Equals(firstB, secondA, StringComparison.OrdinalIgnoreCase));
    }
}
