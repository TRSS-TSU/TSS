using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

// Developer-only orchestration over the same runtime actions used by the trainee.
public sealed class TssCapstoneSetup
{
    private readonly TssTrainingSession session;
    private TssNetworkRunProfile Profile => session.NetworkProfile;

    public TssCapstoneSetup(TssTrainingSession session) => this.session = session;

    public string Preview(out bool canApply)
    {
        var lines = new List<string>();
        canApply = session && session.Scenario && session.NetworkState != null && Profile != null;
        if (!canApply) return "Load a network-enabled scenario and wait for its runtime session.";
        if (session.HeldItem || session.HeldCable)
        {
            lines.Add("BLOCKED: return the carried item or cable first.");
            canApply = false;
        }
        var neededEquipment = new Dictionary<EquipmentCategory, int>();
        var racks = UnityEngine.Object.FindObjectsByType<RackMountController>(FindObjectsInactive.Include, FindObjectsSortMode.None);
        var stations = UnityEngine.Object.FindObjectsByType<EndpointPlacementStation>(FindObjectsInactive.Include, FindObjectsSortMode.None);

        foreach (var expected in session.Scenario.expectedRackPlacements ?? Array.Empty<ExpectedRackPlacement>())
        {
            if (expected == null) continue;
            var matches = session.Installed.Where(x => x.RackId == expected.rackId && x.Equipment
                && x.Equipment.category == expected.category && x.StartingU >= expected.startingU
                && x.StartingU <= Math.Max(expected.startingU, expected.endingU)).ToArray();
            if (matches.Length == 0)
            {
                lines.Add("ADD rack: " + expected.requiredName);
                neededEquipment[expected.category] = neededEquipment.GetValueOrDefault(expected.category) + 1;
                var rack = racks.SingleOrDefault(x => x && x.RackId == expected.rackId);
                var equipment = FindEquipment(expected.category);
                var firstU = Math.Min(expected.startingU, expected.endingU > 0 ? expected.endingU : expected.startingU);
                var lastU = Math.Max(expected.startingU, expected.endingU);
                if (!rack || !equipment || !Enumerable.Range(firstU, lastU - firstU + 1).Any(u => rack.CanInstall(equipment, u)))
                { lines.Add("BLOCKED rack position: " + expected.requiredName); canApply = false; }
            }
            else if (matches.Length == 1 && matches[0].DeviceName == expected.requiredName) lines.Add("OK rack: " + expected.requiredName);
            else { lines.Add("BLOCKED rack: " + expected.requiredName + " has a conflicting placement or name."); canApply = false; }
        }
        foreach (var expected in session.Scenario.expectedEndpointPlacements ?? Array.Empty<ExpectedEndpointPlacement>())
        {
            if (expected == null) continue;
            var matches = session.EndpointPlacements.Where(x => x.StationId == expected.stationId && x.Equipment
                && x.Equipment.category == expected.category).ToArray();
            if (matches.Length == 0)
            {
                lines.Add("ADD endpoint: " + expected.requiredName);
                neededEquipment[expected.category] = neededEquipment.GetValueOrDefault(expected.category) + 1;
                var station = stations.SingleOrDefault(x => x && x.StationId == expected.stationId);
                if (!station || !station.CanPlace(FindEquipment(expected.category)))
                { lines.Add("BLOCKED endpoint station: " + expected.requiredName); canApply = false; }
            }
            else if (matches.Length == 1 && matches[0].DeviceName == expected.requiredName) lines.Add("OK endpoint: " + expected.requiredName);
            else { lines.Add("BLOCKED endpoint: " + expected.requiredName + " has a conflicting placement or name."); canApply = false; }
        }
        foreach (var need in neededEquipment)
        {
            var available = session.GetScenarioInventory().Where(x => x?.equipment && x.equipment.category == need.Key)
                .Sum(x => session.GetRemaining(x.equipment));
            if (available < need.Value)
            { lines.Add($"BLOCKED inventory: {need.Key} needs {need.Value}, has {available}."); canApply = false; }
        }
        foreach (var link in session.Scenario.permanentConnections ?? Array.Empty<ScenarioPermanentCableConnection>())
        {
            if (link == null || !link.requiresPunchDownTermination) continue;
            if (ConflictingTermination(link.connectionId, link.patchPanelEndpointId)
                || ConflictingTermination(link.connectionId, link.wallportEndpointId))
            { lines.Add("BLOCKED termination: " + link.connectionId); canApply = false; }
        }
        var endpoints = UnityEngine.Object.FindObjectsByType<TssPortEndpoint>(FindObjectsInactive.Include, FindObjectsSortMode.None);
        var planned = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var neededCables = new Dictionary<TssCableType, int>();
        foreach (var cable in RequiredCables())
        {
            var a = Resolve(cable.a, endpoints);
            var b = Resolve(cable.b, endpoints);
            if (!a || !b)
            {
                // Endpoints for equipment not yet placed will appear during Apply.
                if (!RoleWillBePlaced(cable.a) && !RoleWillBePlaced(cable.b))
                { lines.Add("BLOCKED cable endpoint: " + cable.a + " / " + cable.b); canApply = false; }
                else lines.Add("ADD cable after placement: " + cable.a + " / " + cable.b);
                neededCables[cable.type] = neededCables.GetValueOrDefault(cable.type) + 1;
                continue;
            }
            var existing = session.PhysicalConnections.FirstOrDefault(x => !x.IsPermanent &&
                ((x.EndpointAId == a.EndpointId && x.EndpointBId == b.EndpointId) ||
                 (x.EndpointAId == b.EndpointId && x.EndpointBId == a.EndpointId)));
            if (existing.ConnectionId != null && existing.CableType == cable.type) { lines.Add("OK cable: " + cable.a + " / " + cable.b); continue; }
            if (existing.ConnectionId != null || session.IsEndpointConnected(a.EndpointId) || session.IsEndpointConnected(b.EndpointId)
                || !planned.Add(a.EndpointId) || !planned.Add(b.EndpointId))
            { lines.Add("BLOCKED cable conflict: " + cable.a + " / " + cable.b); canApply = false; }
            else lines.Add("ADD cable: " + cable.a + " / " + cable.b);
            if (existing.ConnectionId == null) neededCables[cable.type] = neededCables.GetValueOrDefault(cable.type) + 1;
        }
        foreach (var need in neededCables)
        {
            var available = session.GetScenarioCableInventory().Where(x => x?.cable && x.cable.cableType == need.Key)
                .Sum(x => session.GetRemaining(x.cable));
            if (available < need.Value)
            { lines.Add($"BLOCKED cable inventory: {need.Key} needs {need.Value}, has {available}."); canApply = false; }
        }
        foreach (var expected in ExpectedAddressablePlacements())
        {
            var role = NetworkRole(expected);
            if (!Profile.EndpointAddresses.TryGetValue(role, out var address))
            { lines.Add("BLOCKED missing profile address: " + role); canApply = false; continue; }
            var gateway = EndpointNetworkState.GatewayFor(address, Profile);
            if (string.IsNullOrEmpty(gateway)) { lines.Add("BLOCKED missing gateway: " + role); canApply = false; continue; }
            var key = DeviceKey(expected.requiredName);
            if (key != null && session.NetworkState.TryGetEndpoint(key, out var current)
                && (current.UseDhcp || !string.IsNullOrEmpty(current.Address) || !string.IsNullOrEmpty(current.Mask)
                    || !string.IsNullOrEmpty(current.Gateway) || !string.IsNullOrEmpty(current.Dns))
                && (current.UseDhcp || current.Address != address || current.Mask != "255.255.255.0"
                    || current.Gateway != gateway || current.Dns != Profile.WanProviderAddress))
            { lines.Add("BLOCKED address conflict: " + role); canApply = false; }
            else lines.Add($"STATIC {role}: {address} / 255.255.255.0, gateway {gateway}, DNS {Profile.WanProviderAddress}");
        }
        if (Profile.RequireAccessPointConfiguration && !session.NetworkState.AccessPoint.Configured)
            lines.Add("AP configuration will be attempted when a configured endpoint can reach its management address.");
        lines.Add("Router and switch CLI settings remain manual.");
        var laptopRole = LaptopRole();
        if (!string.IsNullOrWhiteSpace(laptopRole))
            lines.Add($"{laptopRole} wireless association and DHCP remain manual.");
        return string.Join("\n", lines);
    }

    public string Apply()
    {
        var preview = Preview(out var ready);
        if (!ready) return preview;
        var automation = new TssScenarioAutomation(session);
        var placed = automation.PlaceAllEquipment();
        if (placed.failedPlacements != 0) return "Stopped during placement. Rerun after correction.\n" + placed;
        var terminated = automation.TerminateAll();
        if (terminated.failedTerminations != 0) return "Stopped during termination. Rerun after correction.\n" + terminated;
        var endpoints = UnityEngine.Object.FindObjectsByType<TssPortEndpoint>(FindObjectsInactive.Include, FindObjectsSortMode.None);
        var connected = 0;
        foreach (var cable in RequiredCables())
        {
            var a = Resolve(cable.a, endpoints);
            var b = Resolve(cable.b, endpoints);
            if (!a || !b) return $"Stopped: missing port for {cable.a} / {cable.b}. Rerun after correction.";
            var existing = session.PhysicalConnections.FirstOrDefault(x => !x.IsPermanent &&
                ((x.EndpointAId == a.EndpointId && x.EndpointBId == b.EndpointId) ||
                 (x.EndpointAId == b.EndpointId && x.EndpointBId == a.EndpointId)));
            if (existing.ConnectionId != null && existing.CableType == cable.type) continue;
            if (existing.ConnectionId != null || session.IsEndpointConnected(a.EndpointId) || session.IsEndpointConnected(b.EndpointId))
                return $"Stopped: conflicting cable at {cable.a} / {cable.b}. Rerun after correction.";
            var definition = session.GetScenarioCableInventory().FirstOrDefault(x => x?.cable && x.cable.cableType == cable.type && session.GetRemaining(x.cable) > 0)?.cable;
            var error = "cable inventory unavailable";
            if (!definition || !session.TryCheckoutCable(definition, out error, false)
                || !session.TryUsePort(a, out error, false) || !session.TryUsePort(b, out error, false))
            {
                if (session.HeldCable) session.ReturnHeldCable(out _);
                return $"Stopped while connecting {cable.a} / {cable.b}: {error}. Rerun after correction.";
            }
            connected++;
        }
        session.NotifyNetworkStateChanged();
        var commands = new TssEndpointCommandProcessor(session.NetworkState);
        var addressed = 0;
        foreach (var expected in ExpectedAddressablePlacements())
        {
            var role = NetworkRole(expected);
            if (!Profile.EndpointAddresses.TryGetValue(role, out var address))
                return $"Stopped: missing profile address for {role}.";
            var key = DeviceKey(expected.requiredName);
            if (string.IsNullOrEmpty(key)) return "Stopped: missing placed device " + expected.requiredName;
            var state = session.NetworkState.GetOrCreateEndpoint(key, role, expected.category);
            var port = Resolve(expected.requiredName, endpoints);
            if (port)
            {
                state.Layer1Connected = session.IsEndpointOnEffectivePhysicalPath(port.EndpointId);
                state.Layer2Connected = state.Layer1Connected;
            }
            var gateway = EndpointNetworkState.GatewayFor(address, Profile);
            if (!state.UseDhcp && state.Address == address && state.Mask == "255.255.255.0"
                && state.Gateway == gateway && state.Dns == Profile.WanProviderAddress) continue;
            if (!commands.TryApply(key, false, address, "255.255.255.0", gateway, Profile.WanProviderAddress, out var error))
                return $"Stopped while addressing {role}: {error}. Rerun after correction.";
            addressed++;
        }
        session.NotifyNetworkStateChanged();
        session.NetworkState.SetAccessPointUplink(session.IsAccessPointUplinkConnected());
        var apResult = "AP configuration not required by the scenario.";
        if (Profile.RequireAccessPointConfiguration && !session.NetworkState.AccessPoint.Configured)
        {
            apResult = "AP settings deferred: no configured endpoint can reach the AP.";
            foreach (var expected in ExpectedAddressablePlacements())
            {
                if (expected.category == EquipmentCategory.AccessPoint) continue;
                var key = DeviceKey(expected.requiredName);
                if (string.IsNullOrEmpty(key)) continue;
                if (!session.NetworkState.TryConfigureAccessPoint(key, Profile.Ssid, Profile.ApAdminPassword,
                    Profile.WifiPassphrase, Profile.WirelessChannel.ToString(), true, out var error))
                {
                    apResult = "AP settings deferred: " + error;
                    continue;
                }

                apResult = "AP wireless settings configured.";
                break;
            }
        }
        session.NotifyNetworkStateChanged();
        return $"Placed {placed.placed}; terminated {terminated.terminated}; connected {connected}; addressed {addressed}.\n{apResult}\nRouter/switch CLI and laptop association/DHCP untouched.";
    }

    private IEnumerable<(string a, string b, TssCableType type)> RequiredCables()
    {
        foreach (var row in session.Scenario.cableTypeRequirements ?? Array.Empty<ScenarioCableTypeRequirement>())
            if (row != null) yield return (row.endpointAId, row.endpointBId, row.requiredCableType);
    }

    private bool RoleWillBePlaced(string id)
    {
        if (string.IsNullOrEmpty(id)) return false;
        if (id.StartsWith("Rack:", StringComparison.Ordinal)) return true;
        var role = id.Split(':')[0];
        return (session.Scenario.expectedRackPlacements ?? Array.Empty<ExpectedRackPlacement>()).Any(x => x != null && (x.requiredName == role || x.networkRole == role))
            || (session.Scenario.expectedEndpointPlacements ?? Array.Empty<ExpectedEndpointPlacement>()).Any(x => x != null && (x.requiredName == role || x.networkRole == role));
    }

    private string DeviceKey(string role)
    {
        var placementName = PlacementNameForRole(role);
        foreach (var item in session.EndpointPlacements)
            if (item.DeviceName == placementName) return TssTrainingSession.GetEndpointOwnerPrefix(item.PlacementId);
        foreach (var item in session.Installed)
            if (item.DeviceName == placementName) return TssTrainingSession.GetRackOwnerPrefix(item.RackId, item.StartingU, item.Equipment);
        return null;
    }

    private string PlacementNameForRole(string role)
    {
        foreach (var expected in session.Scenario.expectedEndpointPlacements ?? Array.Empty<ExpectedEndpointPlacement>())
            if (expected != null && (expected.networkRole == role || expected.requiredName == role)) return expected.requiredName;
        foreach (var expected in session.Scenario.expectedRackPlacements ?? Array.Empty<ExpectedRackPlacement>())
            if (expected != null && (expected.networkRole == role || expected.requiredName == role)) return expected.requiredName;
        return role;
    }

    private IEnumerable<ExpectedEndpointPlacement> ExpectedAddressablePlacements()
    {
        foreach (var expected in session.Scenario.expectedEndpointPlacements ?? Array.Empty<ExpectedEndpointPlacement>())
        {
            if (expected == null || expected.category == EquipmentCategory.Laptop) continue;
            var role = NetworkRole(expected);
            if (string.Equals(role, Profile.TestLaptopRole, StringComparison.OrdinalIgnoreCase)) continue;
            if (Profile.EndpointAddresses.ContainsKey(role)) yield return expected;
        }
    }

    private string LaptopRole()
    {
        var laptop = (session.Scenario.expectedEndpointPlacements ?? Array.Empty<ExpectedEndpointPlacement>())
            .FirstOrDefault(x => x != null && (x.category == EquipmentCategory.Laptop
                || string.Equals(NetworkRole(x), Profile.TestLaptopRole, StringComparison.OrdinalIgnoreCase)));
        return laptop == null ? string.Empty : NetworkRole(laptop);
    }

    private static string NetworkRole(ExpectedEndpointPlacement expected) =>
        string.IsNullOrWhiteSpace(expected.networkRole) ? expected.requiredName : expected.networkRole;

    private EquipmentDefinition FindEquipment(EquipmentCategory category) => session.GetScenarioInventory()
        .FirstOrDefault(x => x?.equipment && x.equipment.category == category)?.equipment;

    private TssPortEndpoint Resolve(string id, TssPortEndpoint[] endpoints)
    {
        if (string.IsNullOrEmpty(id)) return null;
        var exact = endpoints.Where(x => x && x.EndpointId == id).ToArray();
        if (exact.Length == 1) return exact[0];
        if (id.StartsWith("Rack:", StringComparison.Ordinal))
        {
            var resolved = session.ResolveScenarioEndpointId(id);
            var rackMatches = endpoints.Where(x => x && x.EndpointId == resolved).ToArray();
            return rackMatches.Length == 1 ? rackMatches[0] : null;
        }
        var split = id.Split(':');
        var key = DeviceKey(split[0]);
        if (key == null) return null;
        var prefix = key + ":";
        var found = endpoints.Where(x => x && x.EndpointId.StartsWith(prefix, StringComparison.OrdinalIgnoreCase)
            && (split.Length == 1 || x.EndpointId == prefix + split[1])).ToArray();
        return found.Length == 1 ? found[0] : null;
    }

    private bool ConflictingTermination(string connectionId, string endpointId) =>
        !session.IsTerminationCorrectForResults(connectionId, endpointId)
        && session.TryGetTerminationModuleState(connectionId, endpointId, out _);
}
