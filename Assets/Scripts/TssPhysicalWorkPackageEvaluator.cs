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
        foreach (var requirement in requirements)
        {
            if (requirement == null)
                continue;

            hasRequirements = true;
            var status = _session.GetCableTypeStatuses().FirstOrDefault(x =>
                string.Equals(x.ConnectionId, requirement.requirementId, StringComparison.OrdinalIgnoreCase)
                || EndpointPairMatches(x.EndpointAId, x.EndpointBId, requirement.endpointAId, requirement.endpointBId));
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

        var requiresTermination = false;
        var terminationsComplete = true;
        foreach (var connection in _scenario.permanentConnections ?? Array.Empty<ScenarioPermanentCableConnection>())
        {
            if (connection == null)
                continue;

            hasRequirements = true;
            var id = string.IsNullOrWhiteSpace(connection.connectionId) ? "Required" : connection.connectionId;
            var connected = _session.PhysicalConnections.Any(x => x.IsPermanent && string.Equals(x.ConnectionId, connection.connectionId, StringComparison.OrdinalIgnoreCase));
            Add(result, connected, "WP1.PermanentConnections." + id, $"Permanent connection {id} is installed.");

            if (!connection.requiresPunchDownTermination)
                continue;

            requiresTermination = true;
            terminationsComplete &= _session.IsTerminationCorrectForResults(connection.connectionId, connection.patchPanelEndpointId)
                && _session.IsTerminationCorrectForResults(connection.connectionId, connection.wallportEndpointId);
        }

        if (requiresTermination)
            Add(result, terminationsComplete, "WP1.Terminations", "Permanent cable runs are terminated.");
        if (!hasRequirements)
            result.Add("WP1.Requirements", "No physical installation requirements are defined for this scenario.");
        return result;
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
