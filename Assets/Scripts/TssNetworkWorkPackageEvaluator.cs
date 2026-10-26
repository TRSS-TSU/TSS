using System;
using System.Collections.Generic;
using System.Linq;

public sealed class TssWorkPackageResult
{
    public readonly List<TssWorkPackageCondition> UnmetCategories = new();
    public readonly List<TssWorkPackageCondition> Conditions = new();
    public readonly HashSet<string> AttemptedCategories = new(StringComparer.OrdinalIgnoreCase);
    public readonly TssNetworkCompletionSnapshot CompletionSnapshot;
    public IReadOnlyList<string> UnmetConditions => UnmetCategories.ConvertAll(x => x.Label);
    public bool Complete => UnmetCategories.Count == 0;
    public TssWorkPackageResult(TssNetworkCompletionSnapshot snapshot = null)
    {
        CompletionSnapshot = snapshot;
        if (snapshot != null)
            AttemptedCategories.Add("*");
    }
    public void Add(string id, string label)
    {
        var condition = new TssWorkPackageCondition(id, label, false);
        Conditions.Add(condition);
        UnmetCategories.Add(condition);
    }
    public void Pass(string id, string label) => Conditions.Add(new TssWorkPackageCondition(id, label, true));
    public void Attempt(string id) => AttemptedCategories.Add(id);
    public bool WasAttempted(string id, bool prefix = false)
    {
        if (AttemptedCategories.Contains("*"))
            return true;
        foreach (var attempt in AttemptedCategories)
            if (prefix ? attempt.StartsWith(id, StringComparison.OrdinalIgnoreCase) : attempt.Equals(id, StringComparison.OrdinalIgnoreCase))
                return true;
        return false;
    }
}

public sealed class TssWorkPackageCondition
{
    public readonly string Id;
    public readonly string Label;
    public readonly bool IsComplete;
    public TssWorkPackageCondition(string id, string label, bool isComplete = false) { Id = id; Label = label; IsComplete = isComplete; }
}

public sealed class TssNetworkWorkPackageEvaluator
{
    private readonly TssNetworkSessionState _state;
    private readonly TssTrainingSession _session;
    // Cable correctness stays scenario-driven while all connector-compatible cable types remain placeable.
    public TssNetworkWorkPackageEvaluator(TssNetworkSessionState state)
        : this(state, TssTrainingSession.Instance) { }
    public TssNetworkWorkPackageEvaluator(TssNetworkSessionState state, TssTrainingSession session)
    {
        _state = state;
        _session = session;
    }

    public TssWorkPackageResult EvaluateLayer2(bool useCompletionHistory = true)
    {
        if (_state == null) return new TssWorkPackageResult();
        if (useCompletionHistory && _state.CompletionSnapshots.TryGetValue("WP2", out var snapshot)) return new TssWorkPackageResult(snapshot);
        var result = new TssWorkPackageResult();
        var an1 = Find("AN-1"); var an2 = Find("AN-2");
        CheckSwitch(an1, "AN-1", result); CheckSwitch(an2, "AN-2", result);
        CheckVlans(an1, "AN-1", result); CheckVlans(an2, "AN-2", result);
        CheckAccessPorts(an1, an2, result);
        CheckTrunks(an1, an2, result);
        CheckCableTypes(result);
        CheckUnusedPorts(an1, an2, result);
        if (useCompletionHistory && result.Complete) result = new TssWorkPackageResult(_state.SaveCompletionSnapshot("WP2"));
        return result;
    }

    public TssWorkPackageResult EvaluateLayer3(bool useCompletionHistory = true)
    {
        if (_state == null) return new TssWorkPackageResult();
        if (useCompletionHistory && _state.CompletionSnapshots.TryGetValue("WP3", out var snapshot)) return new TssWorkPackageResult(snapshot);
        var result = new TssWorkPackageResult();
        var router = Find("CN-1");
        if (router == null) { result.Add("WP3.Router", "Router configuration is unavailable."); return result; }
        CheckHardening(router, "WP3.Hardening", result);
        CheckRouterInterfaces(router, result);
        if (router.Evidence.Contains("show ip route")) result.Attempt("WP3.RouteEvidence");
        else result.Add("WP3.RouteEvidence", "Required router verification was not typed.");
        var hasDefault = false;
        foreach (var route in router.Routes)
            if (route.Network == "0.0.0.0" && route.Mask == "0.0.0.0" && route.NextHop == _state.Profile.WanProviderAddress) { hasDefault = true; break; }
        if (router.Routes.Count > 0) result.Attempt("WP3.DefaultRoute");
        if (!hasDefault) result.Add("WP3.DefaultRoute", "Provider routing is incomplete.");
        CheckEndpointEvidence(result);
        CheckSaved(router, "WP3.Save", result);
        if (useCompletionHistory && result.Complete) result = new TssWorkPackageResult(_state.SaveCompletionSnapshot("WP3"));
        return result;
    }

    public TssWorkPackageResult EvaluateLayer4(bool useCompletionHistory = true)
    {
        if (_state == null) return new TssWorkPackageResult();
        if (useCompletionHistory && _state.CompletionSnapshots.TryGetValue("WP4", out var snapshot)) return new TssWorkPackageResult(snapshot);

        var result = new TssWorkPackageResult();
        CheckWp4Condition(_state.AccessPoint.UplinkConnected, "WP4.APUplink", "AP uplink is not connected.", result);
        CheckWp4Condition(_state.IsAccessPointSwitchPortReady(), "WP4.SwitchPort", "AP switchport is not ready.", result);
        CheckWp4Condition(_state.AccessPoint.Configured, "WP4.APConfig", "AP wireless settings are incomplete.", result);

        var laptop = FindLaptop();
        if (laptop == null)
        {
            result.Add("WP4.LaptopAssociation", "Test laptop is unavailable.");
            result.Add("WP4.Dhcp", "Test laptop DHCP lease is unavailable.");
            result.Add("WP4.Diagnostics", "Test laptop diagnostics are incomplete.");
            result.Add("WP4.RedWolf", "RedWolf page evidence is incomplete.");
        }
        else
        {
            CheckWp4Condition(_state.IsWirelessAssociated(laptop.Key) && laptop.Layer1Connected && laptop.Layer2Connected,
                "WP4.LaptopAssociation", "Test laptop is not associated to the guest AP.", result);
            CheckWp4Condition(laptop.UseDhcp && laptop.Address == _state.Profile.TestLaptopAddress,
                "WP4.Dhcp", "Test laptop has not obtained the expected DHCP lease.", result);
            CheckWp4Condition(laptop.Evidence.Contains("ipconfig") && laptop.Evidence.Contains("TestLaptop-provider"),
                "WP4.Diagnostics", "Test laptop diagnostics are incomplete.", result);
            CheckWp4Condition(laptop.Evidence.Contains("redwolf"),
                "WP4.RedWolf", "RedWolf page evidence is incomplete.", result);
        }

        if (useCompletionHistory && result.Complete) result = new TssWorkPackageResult(_state.SaveCompletionSnapshot("WP4"));
        return result;
    }

    private void CheckSwitch(NetworkDeviceState device, string name, TssWorkPackageResult result)
    {
        if (device == null) { result.Add("WP2." + name + ".Device", name + " configuration is unavailable."); return; }
        CheckHardening(device, "WP2." + name + ".Hardening", result);
        if (!device.Evidence.Contains("show vlan brief") || !device.Evidence.Contains("show interfaces trunk")) result.Add("WP2." + name + ".Evidence", name + " verification was not typed.");
        else result.Pass("WP2." + name + ".Evidence", name + " verification typed.");
        CheckSaved(device, "WP2." + name + ".Save", result);
    }

    private void CheckHardening(NetworkDeviceState device, string id, TssWorkPackageResult result)
    {
        if (device != null && (!string.IsNullOrWhiteSpace(device.EnablePassword) || !string.IsNullOrWhiteSpace(device.EnableSecret) || !string.IsNullOrWhiteSpace(device.ConsolePassword) || !string.IsNullOrWhiteSpace(device.VtyPassword) || !string.IsNullOrWhiteSpace(device.Motd) || device.PasswordEncryption || device.DomainLookupDisabled))
            result.Attempt(id);
        var p = _state.Profile;
        if (p == null || !device.PasswordEncryption || !device.DomainLookupDisabled || device.EnableSecret != p.EnableSecret || device.ConsolePassword != p.ConsolePassword || device.VtyPassword != p.VtyPassword || device.Motd != p.Motd)
            result.Add(id, "Required device hardening is incomplete.");
        else result.Pass(id, "Required device hardening is complete.");
    }

    private void CheckVlans(NetworkDeviceState device, string name, TssWorkPackageResult result)
    {
        if (device != null && device.Vlans.Count > 1)
            result.Attempt("WP2." + name + ".Vlans");
        if (device == null || !ActiveVlan(device, _state.Profile.ManagementVlan, _state.Profile.ManagementVlanName) || !ActiveVlan(device, _state.Profile.ReceptionVlan, _state.Profile.ReceptionVlanName) || !ActiveVlan(device, _state.Profile.DataVlan, _state.Profile.DataVlanName) || !ActiveVlan(device, _state.Profile.NativeVlan, _state.Profile.NativeVlanName) || !ActiveVlan(device, _state.Profile.DeadVlan, _state.Profile.DeadVlanName))
            result.Add("WP2." + name + ".Vlans", name + " VLAN database is incomplete.");
        else result.Pass("WP2." + name + ".Vlans", name + " VLAN database is complete.");
    }

    private void CheckAccessPorts(NetworkDeviceState an1, NetworkDeviceState an2, TssWorkPackageResult result)
    {
        foreach (var required in _state.Profile?.RequiredPorts ?? Array.Empty<NetworkRequiredPort>())
        {
            var device = required.deviceRole.Equals("AN-2", StringComparison.OrdinalIgnoreCase) ? an2 : an1;
            if (device != null && device.Interfaces.ContainsKey(required.interfaceName))
                result.Attempt("WP2.Access." + required.interfaceName);
            if (device == null || !device.Interfaces.TryGetValue(required.interfaceName, out var port) || port.IsTrunk || !port.AdminUp || !port.CarrierUp || port.AccessVlan != VlanFor(required.endpointId))
                result.Add("WP2.Access." + required.interfaceName, "Required endpoint-facing access ports are incomplete.");
            else result.Pass("WP2.Access." + required.interfaceName, required.interfaceName + " access port is correct.");
        }
    }

    private void CheckTrunks(NetworkDeviceState an1, NetworkDeviceState an2, TssWorkPackageResult result)
    {
        foreach (var pair in new[] { (an1, "Fa0/1"), (an1, "Fa0/6"), (an2, "Fa0/1") })
        {
            if (pair.Item1 != null && pair.Item1.Interfaces.ContainsKey(pair.Item2))
                result.Attempt("WP2.Trunks");
            if (pair.Item1 == null || !pair.Item1.Interfaces.TryGetValue(pair.Item2, out var port) || !port.IsTrunk || !port.AdminUp || !port.CarrierUp || port.NativeVlan != _state.Profile.NativeVlan || !port.AllowedVlans.Contains(_state.Profile.ManagementVlan) || !port.AllowedVlans.Contains(_state.Profile.ReceptionVlan) || !port.AllowedVlans.Contains(_state.Profile.DataVlan))
                result.Add("WP2.Trunks", "Required trunks are incomplete.");
            else result.Pass("WP2.Trunks", pair.Item1.ExpectedRole + " " + pair.Item2 + " trunk is correct.");
        }
    }

    private void CheckCableTypes(TssWorkPackageResult result)
    {
        var session = _session;
        if (!session || !ReferenceEquals(session.NetworkState, _state))
            return;

        foreach (var status in session.GetCableTypeStatuses())
        {
            var id = "WP2.CableTypes." + (string.IsNullOrWhiteSpace(status.ConnectionId) ? "Required" : status.ConnectionId);
            var endpoints = status.EndpointAId + " to " + status.EndpointBId;
            if (status.IsConnected)
                result.Attempt("WP2.CableTypes");

            if (status.IsCorrect && status.IsConnected)
            {
                result.Pass(id, endpoints + " uses the required cable type.");
                continue;
            }

            result.Add(id, status.IsConnected
                ? $"{endpoints} uses {status.ActualCableType}; required {status.RequiredCableType}."
                : $"{endpoints} is not connected with the required {status.RequiredCableType} cable.");
        }
    }

    private void CheckUnusedPorts(NetworkDeviceState an1, NetworkDeviceState an2, TssWorkPackageResult result)
    {
        var complete = CheckUnusedPorts(an1, 8, 24, _state.Profile.DeadVlan) && CheckUnusedPorts(an2, 5, 24, _state.Profile.DeadVlan);
        if (complete)
            result.Attempt("WP2.UnusedPorts");
        if (complete)
            result.Pass("WP2.UnusedPorts", "Unused switch ports are parked and shut.");
        else
            result.Add("WP2.UnusedPorts", "Unused switch ports must use the dead VLAN and be shut down.");
    }

    private static bool CheckUnusedPorts(NetworkDeviceState device, int firstPort, int lastPort, int deadVlan)
    {
        if (device == null)
            return false;

        for (var portNumber = firstPort; portNumber <= lastPort; portNumber++)
        {
            if (!device.Interfaces.TryGetValue("Fa0/" + portNumber, out var port)
                || port.IsTrunk || port.AdminUp || port.AccessVlan != deadVlan)
                return false;
        }

        return true;
    }

    private void CheckRouterInterfaces(NetworkDeviceState router, TssWorkPackageResult result)
    {
        if (router.Interfaces.ContainsKey("Fa0/0"))
            result.Attempt("WP3.Subinterfaces");
        if (router.Interfaces.ContainsKey("Fa0/1"))
            result.Attempt("WP3.Wan");
        if (!HasInterface(router, "Fa0/0", false, null, null)) result.Add("WP3.ParentInterface", "Router parent interface is incomplete.");
        else result.Pass("WP3.ParentInterface", "Router parent interface is correct.");
        foreach (var pair in new[] { (_state.Profile.ManagementVlan, _state.Profile.ManagementNetwork + ".1"), (_state.Profile.ReceptionVlan, _state.Profile.ReceptionNetwork + ".1"), (_state.Profile.DataVlan, _state.Profile.DataNetwork + ".1") })
        {
            var found = false;
            foreach (var iface in router.Interfaces.Values) if (iface.Dot1qVlan == pair.Item1 && iface.Address == pair.Item2 && iface.Mask == "255.255.255.0" && iface.AdminUp && iface.CarrierUp) { found = true; break; }
            if (!found) result.Add("WP3.Subinterfaces", "Router subinterfaces are incomplete.");
            else result.Pass("WP3.Subinterfaces", "Router subinterface is correct.");
        }
        if (!HasInterface(router, "Fa0/1", false, _state.Profile.WanCustomerAddress, "255.255.255.252")) result.Add("WP3.Wan", "Provider-facing WAN configuration is incomplete.");
        else result.Pass("WP3.Wan", "Provider-facing WAN configuration is correct.");
    }

    private bool HasInterface(NetworkDeviceState device, string name, bool trunk, string address, string mask)
    {
        if (!device.Interfaces.TryGetValue(name, out var iface) || !iface.AdminUp || !iface.CarrierUp || iface.IsTrunk != trunk) return false;
        return address == null || (iface.Address == address && iface.Mask == mask);
    }

    private void CheckEndpointEvidence(TssWorkPackageResult result)
    {
        foreach (var requirement in _state.Profile?.EvidenceRequirements ?? Array.Empty<NetworkEvidenceRequirement>())
        {
            var found = false;
            foreach (var endpoint in _state.Endpoints) if (endpoint.Evidence.Contains(requirement.evidenceId) && _state.Evidence.Contains(endpoint.Key + ":" + requirement.evidenceId)) { found = true; break; }
            foreach (var endpoint in _state.Endpoints) if (endpoint.AttemptedEvidence.Contains(requirement.evidenceId)) { result.Attempt("WP3.EndpointEvidence." + requirement.evidenceId); break; }
            if (!found) result.Add("WP3.EndpointEvidence." + requirement.evidenceId, "Required endpoint diagnostics are incomplete.");
            else result.Pass("WP3.EndpointEvidence." + requirement.evidenceId, requirement.description ?? requirement.evidenceId + " verified.");
        }
    }

    private void CheckSaved(NetworkDeviceState device, string id, TssWorkPackageResult result)
    {
        if (device != null && device.Evidence.Contains("save"))
            result.Attempt(id);
        if (!device.Evidence.Contains("save") || device.StartupSnapshot == null || !Equivalent(device, device.StartupSnapshot)) result.Add(id, device.Hostname + " configuration has not been saved.");
        else result.Pass(id, device.Hostname + " configuration is saved.");
    }

    private bool ActiveVlan(NetworkDeviceState device, int vlan, string name)
    {
        if (device == null || !device.Vlans.TryGetValue(vlan, out var value))
            return false;

        var suspended = value.IndexOf("(suspended)", StringComparison.OrdinalIgnoreCase) >= 0;
        var actualName = value.Replace("(suspended)", string.Empty, StringComparison.OrdinalIgnoreCase).Trim();
        return string.Equals(actualName, name, StringComparison.OrdinalIgnoreCase)
            && (!suspended || vlan == _state.Profile.DeadVlan);
    }

    private void CheckWp4Condition(bool condition, string id, string label, TssWorkPackageResult result)
    {
        if (condition)
            result.Attempt(id);
        if (condition)
            result.Pass(id, id + " verified.");
        else
            result.Add(id, label);
    }

    private EndpointNetworkState FindLaptop()
    {
        foreach (var endpoint in _state.Endpoints)
            if (endpoint.Category == EquipmentCategory.Laptop || endpoint.Role.IndexOf("laptop", StringComparison.OrdinalIgnoreCase) >= 0)
                return endpoint;
        return null;
    }
    private int VlanFor(string role) => role.IndexOf("Reception", StringComparison.OrdinalIgnoreCase) >= 0 ? _state.Profile.ReceptionVlan : role.IndexOf("Data", StringComparison.OrdinalIgnoreCase) >= 0 ? _state.Profile.DataVlan : _state.Profile.ManagementVlan;

    private static bool Equivalent(NetworkDeviceState a, NetworkDeviceState b)
    {
        if (a == null || b == null || a.Hostname != b.Hostname || a.EnablePassword != b.EnablePassword || a.EnableSecret != b.EnableSecret || a.ConsolePassword != b.ConsolePassword || a.VtyPassword != b.VtyPassword || a.Motd != b.Motd || a.PasswordEncryption != b.PasswordEncryption || a.DomainLookupDisabled != b.DomainLookupDisabled || a.ConsoleLoginRequired != b.ConsoleLoginRequired || a.VtyLoginRequired != b.VtyLoginRequired || a.ManagementAddress != b.ManagementAddress || a.ManagementMask != b.ManagementMask || a.DefaultGateway != b.DefaultGateway || a.Routes.Count != b.Routes.Count || a.Vlans.Count != b.Vlans.Count || a.Interfaces.Count != b.Interfaces.Count) return false;
        foreach (var vlan in a.Vlans) if (!b.Vlans.TryGetValue(vlan.Key, out var name) || name != vlan.Value) return false;
        foreach (var route in a.Routes) if (!b.Routes.Any(x => x.Network == route.Network && x.Mask == route.Mask && x.NextHop == route.NextHop)) return false;
        foreach (var pair in a.Interfaces) if (!b.Interfaces.TryGetValue(pair.Key, out var other) || pair.Value.Name != other.Name || pair.Value.AdminUp != other.AdminUp || pair.Value.CarrierUp != other.CarrierUp || pair.Value.IsTrunk != other.IsTrunk || pair.Value.TrunkEncapsulationDot1q != other.TrunkEncapsulationDot1q || pair.Value.AccessVlan != other.AccessVlan || pair.Value.NativeVlan != other.NativeVlan || pair.Value.Dot1qVlan != other.Dot1qVlan || pair.Value.Address != other.Address || pair.Value.Mask != other.Mask || !pair.Value.AllowedVlans.SetEquals(other.AllowedVlans)) return false;
        return true;
    }

    private NetworkDeviceState Find(string hostname)
    {
        foreach (var device in _state.Devices)
            if (string.Equals(device.Hostname, hostname, StringComparison.OrdinalIgnoreCase) || string.Equals(device.ExpectedRole, hostname, StringComparison.OrdinalIgnoreCase)) return device;
        return null;
    }
}
