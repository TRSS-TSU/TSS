using System;
using System.Collections.Generic;
using UnityEngine;

public sealed class TssScenarioAutomation
{
    private static readonly TssConductorColor[] T568B =
    {
        TssConductorColor.WhiteOrange, TssConductorColor.Orange,
        TssConductorColor.WhiteGreen, TssConductorColor.Blue,
        TssConductorColor.WhiteBlue, TssConductorColor.Green,
        TssConductorColor.WhiteBrown, TssConductorColor.Brown
    };

    public sealed class Result
    {
        public int placed, skippedPlacements, failedPlacements;
        public int connected, skippedConnections, failedConnections;
        public int terminated, skippedTerminations, failedTerminations;
        public readonly List<string> messages = new();

        public void Add(string message) { if (!string.IsNullOrWhiteSpace(message)) messages.Add(message); }

        public override string ToString()
        {
            return $"Equipment {placed} placed, {skippedPlacements} skipped, {failedPlacements} failed\n" +
                   $"Cables {connected} connected, {skippedConnections} skipped, {failedConnections} failed\n" +
                   $"Terminations {terminated} saved, {skippedTerminations} skipped, {failedTerminations} failed" +
                   (messages.Count == 0 ? string.Empty : "\n" + string.Join("\n", messages));
        }
    }

    private readonly TssTrainingSession _session;

    public TssScenarioAutomation(TssTrainingSession session)
    {
        _session = session;
    }

    public Result PlaceAllEquipment()
    {
        var result = new Result();
        if (!Ready(result)) return result;
        ReturnHeld();

        foreach (var expected in _session.Scenario.expectedRackPlacements ?? Array.Empty<ExpectedRackPlacement>())
        {
            if (expected == null) continue;
            var first = Mathf.Min(expected.startingU, expected.endingU == 0 ? expected.startingU : expected.endingU);
            var last = Mathf.Max(expected.startingU, expected.endingU == 0 ? expected.startingU : expected.endingU);
            if (MatchesRack(expected, first, last)) { result.skippedPlacements++; continue; }

            var rack = FindRack(expected.rackId);
            var equipment = FindEquipment(expected.category);
            var installU = FindAvailableU(rack, equipment, first, last);
            if (!rack || !equipment || installU < first || !_session.TryCheckout(equipment, out _))
            {
                result.failedPlacements++;
                result.Add($"{expected.requiredName}: rack or inventory unavailable.");
                continue;
            }

            if (!_session.TryInstallHeld(rack, installU, expected.requiredName, out var message))
            {
                ReturnHeld();
                result.failedPlacements++;
                result.Add($"{expected.requiredName}: {message}");
                continue;
            }
            result.placed++;
        }

        foreach (var expected in _session.Scenario.expectedEndpointPlacements ?? Array.Empty<ExpectedEndpointPlacement>())
        {
            if (expected == null) continue;
            if (MatchesEndpoint(expected)) { result.skippedPlacements++; continue; }

            var station = FindStation(expected.stationId);
            var equipment = FindEquipment(expected.category);
            if (!station || !equipment || !_session.TryCheckout(equipment, out _))
            {
                result.failedPlacements++;
                result.Add($"{expected.requiredName}: station or inventory unavailable.");
                continue;
            }

            if (!station.TryPlaceHeldForDeveloper(expected.requiredName, out _, out var message))
            {
                ReturnHeld();
                result.failedPlacements++;
                result.Add($"{expected.requiredName}: {message}");
                continue;
            }
            result.placed++;
        }

        _session.NotifyStateChanged();
        return result;
    }

    public Result ConnectAllCables()
    {
        var result = new Result();
        if (!Ready(result)) return result;
        ReturnHeld();
        var endpoints = UnityEngine.Object.FindObjectsByType<TssPortEndpoint>(FindObjectsInactive.Include, FindObjectsSortMode.None);

        foreach (var requirement in _session.Scenario.cableTypeRequirements ?? Array.Empty<ScenarioCableTypeRequirement>())
        {
            if (requirement == null) continue;
            Debug.Log($"[TSS Automation] Connecting {requirement.requirementId}.");
            var endpointA = FindEndpoint(ResolveEndpoint(requirement.endpointAId, endpoints), endpoints);
            var endpointB = FindEndpoint(ResolveEndpoint(requirement.endpointBId, endpoints), endpoints);
            if (!endpointA || !endpointB)
            {
                result.failedConnections++;
                result.Add($"{requirement.requirementId}: endpoint not found.");
                continue;
            }

            if (HasPlayerConnection(endpointA.EndpointId, endpointB.EndpointId))
            {
                result.skippedConnections++;
                continue;
            }

            var cable = FindCable(requirement.requiredCableType);
            if (!cable)
            {
                result.failedConnections++;
                result.Add($"{requirement.requirementId}: cable inventory unavailable.");
                continue;
            }

            if (!_session.TryCheckoutCable(cable, out var checkoutMessage, false))
            {
                result.failedConnections++;
                result.Add($"{requirement.requirementId}: {checkoutMessage}");
                continue;
            }

            if (!_session.TryUsePort(endpointA, out var firstMessage, false))
            {
                ReturnHeld();
                result.failedConnections++;
                result.Add($"{requirement.requirementId}: {firstMessage}");
                continue;
            }

            if (!_session.TryUsePort(endpointB, out var secondMessage, false))
            {
                ReturnHeld();
                result.failedConnections++;
                result.Add($"{requirement.requirementId}: {secondMessage}");
                continue;
            }
            result.connected++;
            Debug.Log($"[TSS Automation] Connected {requirement.requirementId}.");
        }

        _session.NotifyNetworkStateChanged();
        return result;
    }

    public Result TerminateAll()
    {
        var result = new Result();
        if (!Ready(result)) return result;

        foreach (var link in _session.Scenario.permanentConnections ?? Array.Empty<ScenarioPermanentCableConnection>())
        {
            if (link == null || !link.requiresPunchDownTermination) continue;
            Terminate(link, link.patchPanelEndpointId, true, result);
            Terminate(link, link.wallportEndpointId, false, result);
        }

        _session.NotifyStateChanged();
        return result;
    }

    public Result RunAll()
    {
        var result = PlaceAllEquipment();
        Merge(result, ConnectAllCables());
        Merge(result, TerminateAll());
        return result;
    }

    public static TssTerminationModuleState CreateT568BState(string connectionId, string endpointId, string moduleId)
    {
        var terminals = new TssTerminationTerminalState[8];
        for (var i = 0; i < terminals.Length; i++)
            terminals[i] = new TssTerminationTerminalState { pin = i + 1, conductor = T568B[i], hasConductor = true, isPunched = true, operationOrder = i + 1 };
        return new TssTerminationModuleState { connectionId = connectionId, endpointId = endpointId, moduleId = moduleId, terminals = terminals };
    }

    private void Terminate(ScenarioPermanentCableConnection link, string endpointId, bool endpointA, Result result)
    {
        var moduleId = endpointA ? link.endpointATerminationModuleId : link.endpointBTerminationModuleId;
        if (string.IsNullOrWhiteSpace(moduleId)) moduleId = $"{link.connectionId}:{(endpointA ? "A" : "B")}";
        if (_session.IsTerminationCorrectForResults(link.connectionId, endpointId)) { result.skippedTerminations++; return; }
        var state = CreateT568BState(link.connectionId, endpointId, moduleId);
        if (_session.SetTerminationModuleState(state, true, out var message, false)) result.terminated++;
        else { result.failedTerminations++; result.Add($"{link.connectionId} {endpointId}: {message}"); }
    }

    private bool Ready(Result result)
    {
        if (_session == null || !_session.Scenario) { result.Add("Select a scenario first."); return false; }
        return true;
    }

    private void ReturnHeld()
    {
        if (_session.HeldItem) _session.ReturnHeldItem(out _);
        if (_session.HeldCable) _session.ReturnHeldCable(out _);
    }

    private EquipmentDefinition FindEquipment(EquipmentCategory category)
    {
        foreach (var item in _session.GetScenarioInventory())
            if (item != null && item.equipment && item.equipment.category == category && _session.GetRemaining(item.equipment) > 0)
                return item.equipment;
        return null;
    }

    private static int FindAvailableU(RackMountController rack, EquipmentDefinition equipment, int first, int last)
    {
        if (!rack || !equipment)
            return -1;

        for (var u = first; u <= last; u++)
            if (rack.CanInstall(equipment, u))
                return u;

        return -1;
    }

    private TssCableDefinition FindCable(TssCableType type)
    {
        foreach (var item in _session.GetScenarioCableInventory())
            if (item != null && item.cable && item.cable.cableType == type && _session.GetRemaining(item.cable) > 0)
                return item.cable;
        return null;
    }

    private bool MatchesRack(ExpectedRackPlacement expected, int first, int last)
    {
        foreach (var item in _session.Installed)
            if (item.RackId == expected.rackId && item.StartingU >= first && item.StartingU <= last && item.Equipment && item.Equipment.category == expected.category && item.DeviceName == expected.requiredName)
                return true;
        return false;
    }

    private bool MatchesEndpoint(ExpectedEndpointPlacement expected)
    {
        foreach (var item in _session.EndpointPlacements)
            if (item.StationId == expected.stationId && item.Equipment && item.Equipment.category == expected.category && item.DeviceName == expected.requiredName)
                return true;
        return false;
    }

    private string ResolveEndpoint(string authored, TssPortEndpoint[] endpoints)
    {
        if (string.IsNullOrWhiteSpace(authored) || FindEndpoint(authored, endpoints)) return authored;
        foreach (var endpoint in endpoints)
            if (endpoint && string.Equals(endpoint.DeviceName, authored.Trim(), StringComparison.OrdinalIgnoreCase))
                return endpoint.EndpointId;
        foreach (var binding in _session.Scenario.networkScenario?.roleBindings ?? Array.Empty<NetworkRoleBinding>())
        {
            if (binding == null || string.IsNullOrWhiteSpace(binding.deviceKey) || !authored.StartsWith(binding.deviceKey + ":", StringComparison.Ordinal)) continue;
            foreach (var expected in _session.Scenario.expectedRackPlacements ?? Array.Empty<ExpectedRackPlacement>())
            {
                if (expected == null || expected.requiredName != binding.role || expected.category != binding.category || !binding.deviceKey.StartsWith($"Rack:{expected.rackId}:U", StringComparison.Ordinal)) continue;
                foreach (var installed in _session.Installed)
                    if (installed.RackId == expected.rackId && installed.DeviceName == expected.requiredName && installed.Equipment && installed.Equipment.category == expected.category)
                        return TssTrainingSession.GetRackOwnerPrefix(installed.RackId, installed.StartingU) + authored.Substring(binding.deviceKey.Length);
            }
        }
        return authored;
    }

    private static RackMountController FindRack(string id)
    {
        foreach (var rack in UnityEngine.Object.FindObjectsByType<RackMountController>(FindObjectsInactive.Include, FindObjectsSortMode.None))
            if (rack && rack.RackId == id) return rack;
        return null;
    }

    private static EndpointPlacementStation FindStation(string id)
    {
        foreach (var station in UnityEngine.Object.FindObjectsByType<EndpointPlacementStation>(FindObjectsInactive.Include, FindObjectsSortMode.None))
            if (station && station.StationId == id) return station;
        return null;
    }

    private static TssPortEndpoint FindEndpoint(string id, TssPortEndpoint[] endpoints)
    {
        foreach (var endpoint in endpoints)
            if (endpoint && endpoint.EndpointId == id) return endpoint;
        return null;
    }

    private bool HasPlayerConnection(string a, string b)
    {
        foreach (var connection in _session.PhysicalConnections)
            if (!connection.IsPermanent && ((connection.EndpointAId == a && connection.EndpointBId == b) || (connection.EndpointAId == b && connection.EndpointBId == a))) return true;
        return false;
    }

    private static void Merge(Result into, Result from)
    {
        into.placed += from.placed; into.skippedPlacements += from.skippedPlacements; into.failedPlacements += from.failedPlacements;
        into.connected += from.connected; into.skippedConnections += from.skippedConnections; into.failedConnections += from.failedConnections;
        into.terminated += from.terminated; into.skippedTerminations += from.skippedTerminations; into.failedTerminations += from.failedTerminations;
        into.messages.AddRange(from.messages);
    }
}
