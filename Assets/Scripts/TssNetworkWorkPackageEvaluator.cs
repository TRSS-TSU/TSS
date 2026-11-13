using System;
using System.Collections.Generic;
using System.Linq;

public sealed class TssWorkPackageResult
{
    public readonly List<TssWorkPackageCondition> UnmetCategories = new();
    public readonly List<TssWorkPackageCondition> Conditions = new();
    public readonly HashSet<string> AttemptedCategories = new(StringComparer.OrdinalIgnoreCase);
    public IReadOnlyList<string> UnmetConditions => UnmetCategories.ConvertAll(x => x.Label);
    public bool Complete => UnmetCategories.Count == 0;
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
        foreach (var attempt in AttemptedCategories)
            if (prefix ? attempt.StartsWith(id, StringComparison.OrdinalIgnoreCase) : attempt.Equals(id, StringComparison.OrdinalIgnoreCase))
                return true;
        return false;
    }
    public bool IsComplete(string id, bool prefix = false)
    {
        var found = false;
        foreach (var condition in Conditions)
        {
            var matches = prefix
                ? condition.Id.StartsWith(id, StringComparison.OrdinalIgnoreCase)
                : condition.Id.Equals(id, StringComparison.OrdinalIgnoreCase);
            if (!matches)
                continue;

            found = true;
            if (!condition.IsComplete)
                return false;
        }

        return found;
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
    public TssNetworkWorkPackageEvaluator(TssNetworkSessionState state) => _state = state;

    public TssWorkPackageResult EvaluateLayer2()
    {
        if (_state == null) return new TssWorkPackageResult();
        var result = new TssWorkPackageResult();
        foreach (var role in SwitchRoles())
        {
            var device = Find(role);
            CheckSwitch(device, role, result);
            CheckVlans(device, role, result);
        }
        CheckAccessPorts(result);
        CheckTrunks(result);
        CheckUnusedPorts(result);
        return result;
    }

    public TssWorkPackageResult EvaluateLayer3()
    {
        if (_state == null) return new TssWorkPackageResult();
        var result = new TssWorkPackageResult();
        var router = Find(_state.Profile?.RouterRole ?? "CN-1");
        if (router == null) { result.Add("WP3.Router", "Router configuration is unavailable."); return result; }
        CheckHardening(router, "WP3.Hardening", result, _state.Profile?.ExpectedHostname(_state.Profile.RouterRole ?? "CN-1"));
        CheckRouterInterfaces(router, result);
        if (_state.Profile.RequireRouteEvidence)
        {
            if (router.Evidence.Contains(_state.Profile.RouteEvidenceCommand)) result.Pass("WP3.RouteEvidence", "Required router route verification is complete.");
            else result.Add("WP3.RouteEvidence", "Required router verification was not typed.");
        }
        var hasDefault = false;
        foreach (var route in router.Routes)
            if (route.Network == "0.0.0.0" && route.Mask == "0.0.0.0" && route.NextHop == _state.Profile.WanProviderAddress) { hasDefault = true; break; }
        if (_state.Profile.RequireDefaultRoute)
        {
            if (router.Routes.Count > 0) result.Attempt("WP3.DefaultRoute");
            if (hasDefault) result.Pass("WP3.DefaultRoute", "Default route points to the provider.");
            else result.Add("WP3.DefaultRoute", "Provider routing is incomplete.");
        }
        CheckEndpointEvidence(result);
        CheckSaved(router, "WP3.Save", result);
        return result;
    }

    public TssWorkPackageResult EvaluateLayer4()
    {
        if (_state == null) return new TssWorkPackageResult();

        var result = new TssWorkPackageResult();
        if (_state.Profile.RequireAccessPointUplink)
            CheckWp4Condition(_state.AccessPoint.UplinkConnected, "WP4.APUplink", "AP uplink is not connected.", result);
        if (_state.Profile.RequireAccessPointSwitchPort)
            CheckWp4Condition(_state.IsAccessPointSwitchPortReady(), "WP4.SwitchPort", "AP switchport is not ready.", result);
        if (_state.Profile.RequireAccessPointConfiguration)
            CheckWp4Condition(_state.AccessPoint.Configured, "WP4.APConfig", "AP wireless settings are incomplete.", result);

        var laptop = FindLaptop();
        if (laptop == null)
        {
            if (_state.Profile.RequireLaptopAssociation) result.Add("WP4.LaptopAssociation", "Test laptop is unavailable.");
            if (_state.Profile.RequireDhcpLease) result.Add("WP4.Dhcp", "Test laptop DHCP lease is unavailable.");
            if (_state.Profile.RequireLaptopDiagnostics) result.Add("WP4.Diagnostics", "Test laptop diagnostics are incomplete.");
            if (_state.Profile.RequireRedWolfEvidence) result.Add("WP4.RedWolf", "RedWolf page evidence is incomplete.");
        }
        else
        {
            if (_state.Profile.RequireLaptopAssociation)
                CheckWp4Condition(_state.IsWirelessAssociated(laptop.Key) && laptop.Layer1Connected && laptop.Layer2Connected,
                    "WP4.LaptopAssociation", "Test laptop is not associated to the guest AP.", result);
            if (_state.Profile.RequireDhcpLease)
                CheckWp4Condition(laptop.UseDhcp && laptop.Address == _state.Profile.TestLaptopAddress,
                    "WP4.Dhcp", "Test laptop has not obtained the expected DHCP lease.", result);
            if (_state.Profile.RequireLaptopDiagnostics)
                CheckWp4Condition(_state.Profile.TestLaptopDiagnosticEvidenceIds.All(laptop.Evidence.Contains),
                    "WP4.Diagnostics", "Test laptop diagnostics are incomplete.", result);
            if (_state.Profile.RequireRedWolfEvidence)
                CheckWp4Condition(laptop.Evidence.Contains(_state.Profile.RedWolfEvidenceId),
                    "WP4.RedWolf", "RedWolf page evidence is incomplete.", result);
        }

        return result;
    }

    private void CheckSwitch(NetworkDeviceState device, string name, TssWorkPackageResult result)
    {
        if (device == null)
        {
            result.Add("WP2." + name + ".Hardening", name + " configuration is unavailable.");
            result.Add("WP2." + name + ".Evidence", name + " verification was not typed.");
            result.Add("WP2." + name + ".Save", name + " configuration has not been saved.");
            return;
        }

        CheckHardening(device, "WP2." + name + ".Hardening", result, _state.Profile?.ExpectedHostname(name));
        CheckManagementSvi(device, name, result);
        if (!device.Evidence.Contains("show vlan brief") || !device.Evidence.Contains("show interfaces trunk")) result.Add("WP2." + name + ".Evidence", name + " verification was not typed.");
        else result.Pass("WP2." + name + ".Evidence", name + " verification typed.");
        CheckSaved(device, "WP2." + name + ".Save", result);
    }

    private void CheckManagementSvi(NetworkDeviceState device, string name, TssWorkPackageResult result)
    {
        var id = "WP2." + name + ".ManagementSvi";
        var expectedAddress = _state.Profile?.ManagementNetwork + (name.Equals("AN-2", StringComparison.OrdinalIgnoreCase) ? ".3" : ".2");
        var valid = device != null && device.Svis.TryGetValue(1, out var vlan1) && !vlan1.AdminUp && string.IsNullOrWhiteSpace(vlan1.Address)
            && device.Svis.TryGetValue(_state.Profile.ManagementVlan, out var svi)
            && svi.AdminUp && string.Equals(svi.Address, expectedAddress, StringComparison.OrdinalIgnoreCase)
            && string.Equals(svi.Mask, "255.255.255.0", StringComparison.OrdinalIgnoreCase)
            && ActiveVlan(device, _state.Profile.ManagementVlan, _state.Profile.ManagementVlanName)
            && device.Interfaces.Values.Any(port => device.IsInterfaceOperational(port) && (port.IsTrunk ? port.AllowedVlans.Contains(_state.Profile.ManagementVlan) : port.AccessVlan == _state.Profile.ManagementVlan));

        if (valid) result.Pass(id, name + " VLAN 1 management interface is correct.");
        else result.Add(id, name + " VLAN 1 management interface is incomplete.");
    }

    private void CheckHardening(NetworkDeviceState device, string id, TssWorkPackageResult result, string expectedHostname = null)
    {
        if (device != null && (!string.IsNullOrWhiteSpace(device.EnablePassword) || !string.IsNullOrWhiteSpace(device.EnableSecret) || !string.IsNullOrWhiteSpace(device.ConsolePassword) || !string.IsNullOrWhiteSpace(device.VtyPassword) || !string.IsNullOrWhiteSpace(device.Motd) || device.PasswordEncryption || device.DomainLookupDisabled || device.VtyLoginLocal || device.UserPasswords.Count > 0))
            result.Attempt(id);
        var p = _state.Profile;
        var name = string.IsNullOrWhiteSpace(expectedHostname) ? device?.ExpectedRole ?? "Device" : expectedHostname;
        if (p == null || device == null) { result.Add(id, name + " hardening configuration is unavailable."); return; }
        var missing = new List<string>();
        if (!string.IsNullOrWhiteSpace(expectedHostname) && !string.Equals(device.Hostname, expectedHostname, StringComparison.OrdinalIgnoreCase)) missing.Add("hostname");
        if (p.RequirePasswordEncryption && !device.PasswordEncryption) missing.Add("password encryption");
        if (p.RequireDomainLookupDisabled && !device.DomainLookupDisabled) missing.Add("domain lookup disabled");
        if (p.RequireNoEnablePassword && !string.IsNullOrWhiteSpace(device.EnablePassword)) missing.Add("no enable password");
        if (device.EnableSecret != p.EnableSecret) missing.Add("enable secret");
        if (p.RequireConsoleLogin && !device.ConsoleLoginRequired) missing.Add("console login");
        if (p.RequireConsoleLogin && device.ConsolePassword != p.ConsolePassword) missing.Add("console password");
        if (p.RequireVtyLogin && (!device.VtyLoginRequired || !device.VtyLoginLocal)) missing.Add("VTY local login");
        if (p.RequireVtyLogin && device.VtyPassword != p.VtyPassword) missing.Add("VTY password");
        if (!string.IsNullOrWhiteSpace(p.LocalUsername) && (!device.UserPasswords.TryGetValue(p.LocalUsername, out var password) || password != p.LocalPassword)) missing.Add("local user " + p.LocalUsername);
        if (device.Motd != p.Motd) missing.Add("login banner");
        if (missing.Count > 0) result.Add(id, name + " hardening needs: " + string.Join(", ", missing) + ".");
        else result.Pass(id, name + " hardening is complete.");
    }

    private void CheckVlans(NetworkDeviceState device, string name, TssWorkPackageResult result)
    {
        if (device != null && device.Vlans.Count > 1)
            result.Attempt("WP2." + name + ".Vlans");
        var p = _state.Profile;
        var expected = new HashSet<int> { p.ManagementVlan, p.ReceptionVlan, p.DataVlan, p.NativeVlan, p.DeadVlan };
        if (device == null || !ActiveVlan(device, p.ManagementVlan, p.ManagementVlanName) || !ActiveVlan(device, p.ReceptionVlan, p.ReceptionVlanName) || !ActiveVlan(device, p.DataVlan, p.DataVlanName) || !ActiveVlan(device, p.NativeVlan, p.NativeVlanName) || !SuspendedVlan(device, p.DeadVlan, p.DeadVlanName) || device.Vlans.Keys.Any(vlan => vlan != 1 && !expected.Contains(vlan)))
            result.Add("WP2." + name + ".Vlans", name + " VLAN database is incomplete.");
        else result.Pass("WP2." + name + ".Vlans", name + " VLAN database is complete.");
    }

    private void CheckAccessPorts(TssWorkPackageResult result)
    {
        foreach (var required in _state.Profile?.RequiredPorts ?? Array.Empty<NetworkRequiredPort>())
        {
            var device = Find(required.deviceRole);
            var id = "WP2.Access:" + required.deviceRole + ":" + required.interfaceName;
            if (device != null && device.Interfaces.ContainsKey(required.interfaceName))
                result.Attempt(id);
            if (device == null || !device.Interfaces.TryGetValue(required.interfaceName, out var port) || port.IsTrunk || !device.IsInterfaceOperational(port) || port.AccessVlan != VlanFor(required))
                result.Add(id, required.deviceRole + " " + required.interfaceName + " access port is incomplete.");
            else result.Pass(id, required.deviceRole + " " + required.interfaceName + " access port is correct.");
        }
    }

    private void CheckTrunks(TssWorkPackageResult result)
    {
        foreach (var requirement in _state.Profile?.TrunkRequirements ?? Array.Empty<NetworkTrunkRequirement>())
        {
            var device = Find(requirement.deviceRole);
            var id = "WP2.Trunk:" + requirement.deviceRole + ":" + requirement.interfaceName;
            if (device != null && device.Interfaces.ContainsKey(requirement.interfaceName))
                result.Attempt(id);
            var expected = new HashSet<int>((requirement.allowedVlanNames ?? Array.Empty<string>()).Select(_state.Profile.VlanId));
            var nativeVlan = _state.Profile.VlanId(requirement.nativeVlanName);
            if (device == null || !device.Interfaces.TryGetValue(requirement.interfaceName, out var port) || !port.IsTrunk || !device.IsInterfaceOperational(port) || port.NativeVlan != nativeVlan || !port.AllowedVlans.SetEquals(expected))
                result.Add(id, requirement.deviceRole + " " + requirement.interfaceName + " trunk is incomplete.");
            else result.Pass(id, requirement.deviceRole + " " + requirement.interfaceName + " trunk is correct.");
        }
    }

    private void CheckUnusedPorts(TssWorkPackageResult result)
    {
        foreach (var requirement in _state.Profile?.UnusedPortRequirements ?? Array.Empty<NetworkUnusedPortRequirement>())
            CheckUnusedPorts(Find(requirement.deviceRole), requirement, result);
    }

    private void CheckUnusedPorts(NetworkDeviceState device, NetworkUnusedPortRequirement requirement, TssWorkPackageResult result)
    {
        var complete = device != null;
        var deadVlan = _state.Profile.VlanId(requirement.deadVlanName);
        for (var portNumber = requirement.firstPort; complete && portNumber <= requirement.lastPort; portNumber++)
            complete = device.Interfaces.TryGetValue(requirement.interfacePrefix + portNumber, out var port) && !port.IsTrunk && !port.AdminUp && port.AccessVlan == deadVlan;

        var id = "WP2.UnusedPorts:" + requirement.deviceRole;
        if (complete) result.Pass(id, requirement.deviceRole + " unused ports are parked and shut.");
        else result.Add(id, requirement.deviceRole + " unused ports must use the dead VLAN and be shut down.");
    }

    private void CheckRouterInterfaces(NetworkDeviceState router, TssWorkPackageResult result)
    {
        if (router.Interfaces.ContainsKey(_state.Profile.RouterParentInterface))
            result.Attempt("WP3.Subinterfaces");
        if (router.Interfaces.ContainsKey(_state.Profile.RouterWanInterface))
            result.Attempt("WP3.Wan");
        if (!HasInterface(router, _state.Profile.RouterParentInterface, false, null, null)) result.Add("WP3.ParentInterface", "Router parent interface is incomplete.");
        else result.Pass("WP3.ParentInterface", "Router parent interface is correct.");
        foreach (var requirement in _state.Profile.RouterSubinterfaceRequirements)
        {
            var vlan = _state.Profile.VlanId(requirement.vlanName);
            var address = _state.Profile.NetworkAddress(requirement.networkName, requirement.addressSuffix);
            var found = false;
            foreach (var iface in router.Interfaces.Values) if (iface.Dot1qVlan == vlan && iface.Address == address && iface.Mask == requirement.mask && router.IsInterfaceOperational(iface)) { found = true; break; }
            if (!found) result.Add("WP3.Subinterfaces", "Router subinterfaces are incomplete.");
            else result.Pass("WP3.Subinterfaces", "Router subinterface is correct.");
        }
        if (!HasInterface(router, _state.Profile.RouterWanInterface, false, _state.Profile.WanCustomerAddress, "255.255.255.252")) result.Add("WP3.Wan", "Provider-facing WAN configuration is incomplete.");
        else result.Pass("WP3.Wan", "Provider-facing WAN configuration is correct.");
    }

    private bool HasInterface(NetworkDeviceState device, string name, bool trunk, string address, string mask)
    {
        if (!device.Interfaces.TryGetValue(name, out var iface) || !device.IsInterfaceOperational(iface) || iface.IsTrunk != trunk) return false;
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
        if (!_state.Profile.RequireSave)
            return;
        if (device != null && device.Evidence.Contains("save"))
            result.Attempt(id);
        if (!device.Evidence.Contains("save") || device.StartupSnapshot == null || !Equivalent(device, device.StartupSnapshot)) result.Add(id, device.Hostname + " configuration has not been saved.");
        else result.Pass(id, device.Hostname + " configuration is saved.");
    }

    private bool ActiveVlan(NetworkDeviceState device, int vlan, string name)
    {
        if (device == null || !device.Vlans.TryGetValue(vlan, out var value))
            return false;

        var suspended = device.IsVlanSuspended(vlan);
        var actualName = device.GetVlanName(vlan);
        return string.Equals(actualName, name, StringComparison.OrdinalIgnoreCase)
            && (!suspended || vlan == _state.Profile.DeadVlan);
    }

    private static bool SuspendedVlan(NetworkDeviceState device, int vlan, string name)
    {
        if (device == null || !device.Vlans.TryGetValue(vlan, out var value))
            return false;

        return device.IsVlanSuspended(vlan)
            && string.Equals(device.GetVlanName(vlan), name, StringComparison.OrdinalIgnoreCase);
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
            if (string.Equals(endpoint.Role, _state.Profile?.TestLaptopRole, StringComparison.OrdinalIgnoreCase)
                || endpoint.Category == EquipmentCategory.Laptop)
                return endpoint;
        return null;
    }
    private int VlanFor(NetworkRequiredPort requirement)
    {
        if (!string.IsNullOrWhiteSpace(requirement.vlanName))
            return _state.Profile.VlanId(requirement.vlanName);
        var endpointId = requirement.endpointId ?? string.Empty;
        return endpointId.IndexOf("Reception", StringComparison.OrdinalIgnoreCase) >= 0 ? _state.Profile.ReceptionVlan : endpointId.IndexOf("Data", StringComparison.OrdinalIgnoreCase) >= 0 ? _state.Profile.DataVlan : _state.Profile.ManagementVlan;
    }

    private static bool Equivalent(NetworkDeviceState a, NetworkDeviceState b)
    {
        if (a == null || b == null || a.Hostname != b.Hostname || a.EnablePassword != b.EnablePassword || a.EnableSecret != b.EnableSecret || a.ConsolePassword != b.ConsolePassword || a.AuxPassword != b.AuxPassword || a.VtyPassword != b.VtyPassword || a.Motd != b.Motd || a.PasswordEncryption != b.PasswordEncryption || a.DomainLookupDisabled != b.DomainLookupDisabled || a.ConsoleLoginRequired != b.ConsoleLoginRequired || a.AuxLoginRequired != b.AuxLoginRequired || a.VtyLoginRequired != b.VtyLoginRequired || a.Vty5To15ExecDisabled != b.Vty5To15ExecDisabled || a.VtyLoginLocal != b.VtyLoginLocal || a.VtyExecTimeoutMinutes != b.VtyExecTimeoutMinutes || a.VtyTransportInputSsh != b.VtyTransportInputSsh || a.ConsoleLoggingSynchronous != b.ConsoleLoggingSynchronous || a.AuxLoggingSynchronous != b.AuxLoggingSynchronous || a.VtyLoggingSynchronous != b.VtyLoggingSynchronous || a.ManagementAddress != b.ManagementAddress || a.ManagementMask != b.ManagementMask || a.DefaultGateway != b.DefaultGateway || a.Routes.Count != b.Routes.Count || a.Vlans.Count != b.Vlans.Count || a.UserPasswords.Count != b.UserPasswords.Count || a.Interfaces.Count != b.Interfaces.Count) return false;
        foreach (var vlan in a.Vlans) if (!b.Vlans.TryGetValue(vlan.Key, out var name) || name != vlan.Value) return false;
        foreach (var user in a.UserPasswords) if (!b.UserPasswords.TryGetValue(user.Key, out var password) || password != user.Value) return false;
        foreach (var route in a.Routes) if (!b.Routes.Any(x => x.Network == route.Network && x.Mask == route.Mask && x.NextHop == route.NextHop)) return false;
        foreach (var pair in a.Interfaces) if (!b.Interfaces.TryGetValue(pair.Key, out var other) || pair.Value.Name != other.Name || pair.Value.AdminUp != other.AdminUp || pair.Value.IsTrunk != other.IsTrunk || pair.Value.TrunkEncapsulationDot1q != other.TrunkEncapsulationDot1q || pair.Value.AccessVlan != other.AccessVlan || pair.Value.NativeVlan != other.NativeVlan || pair.Value.Dot1qVlan != other.Dot1qVlan || pair.Value.Address != other.Address || pair.Value.Mask != other.Mask || !pair.Value.AllowedVlans.SetEquals(other.AllowedVlans)) return false;
        return true;
    }

    private IEnumerable<string> SwitchRoles()
    {
        var roles = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var binding in _state.Profile?.RoleBindings ?? Array.Empty<NetworkRoleBinding>())
            if (binding != null && binding.category == EquipmentCategory.Switch && !string.IsNullOrWhiteSpace(binding.role))
                roles.Add(binding.role);
        foreach (var required in _state.Profile?.RequiredPorts ?? Array.Empty<NetworkRequiredPort>())
            if (required != null && !string.IsNullOrWhiteSpace(required.deviceRole))
                roles.Add(required.deviceRole);
        if (roles.Count == 0) { roles.Add("AN-1"); roles.Add("AN-2"); }
        return roles;
    }

    private NetworkDeviceState Find(string hostname)
    {
        foreach (var device in _state.Devices)
            if (string.Equals(device.Hostname, hostname, StringComparison.OrdinalIgnoreCase) || string.Equals(device.ExpectedRole, hostname, StringComparison.OrdinalIgnoreCase)) return device;
        return null;
    }
}
