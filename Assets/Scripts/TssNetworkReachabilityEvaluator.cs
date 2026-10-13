using System;
using System.Collections.Generic;

public enum TssReachabilityFaultCategory
{
    None, PhysicalPathUnavailable, InterfaceUnavailable, VlanNotCarried,
    GatewayUnavailable, DestinationUnavailable, ProviderRouteUnavailable
}

public readonly struct TssReachabilityResult
{
    public readonly bool Success;
    public readonly int FailedLayer;
    public readonly TssReachabilityFaultCategory FaultCategory;
    public readonly string Diagnostic;
    public readonly string SourceRole;
    public readonly string DestinationRole;
    public readonly IReadOnlyList<string> TraceHops;

    public TssReachabilityResult(bool success, int failedLayer, TssReachabilityFaultCategory faultCategory, string diagnostic, string sourceRole, string destinationRole, IReadOnlyList<string> traceHops)
    {
        Success = success; FailedLayer = failedLayer; FaultCategory = faultCategory; Diagnostic = diagnostic;
        SourceRole = sourceRole; DestinationRole = destinationRole; TraceHops = traceHops;
    }
}

public sealed class TssNetworkReachabilityEvaluator
{
    private readonly TssNetworkSessionState _state;
    public TssNetworkReachabilityEvaluator(TssNetworkSessionState state) { _state = state; }

    public TssReachabilityResult Evaluate(string sourceKey, string destinationAddress)
    {
        if (_state == null || !_state.TryGetEndpoint(sourceKey, out var source))
            return Fail(1, TssReachabilityFaultCategory.PhysicalPathUnavailable, "source endpoint is unavailable.", string.Empty, string.Empty);

        var profile = _state.Profile;
        var destinationRole = ResolveRole(destinationAddress, profile);
        if (string.IsNullOrEmpty(destinationRole))
            return Fail(3, TssReachabilityFaultCategory.DestinationUnavailable, "destination does not respond.", source.Role, string.Empty, source.Address);

        var sourceDevice = FindDevice(source);
        var sourceInterface = FindSourceInterface(source, sourceDevice);
        if (!source.Layer1Connected)
            return Fail(1, TssReachabilityFaultCategory.PhysicalPathUnavailable, "no usable physical path.", source.Role, destinationRole);
        if (sourceInterface != null && (!sourceInterface.AdminUp || !sourceInterface.CarrierUp))
            return Fail(1, TssReachabilityFaultCategory.InterfaceUnavailable, "a participating interface is unavailable.", source.Role, destinationRole);
        if (HasStructuredTopology(sourceDevice))
        {
            var access = FindAccessInterface(FindAccessDevice(source, sourceDevice), source.Role);
            if (access != null && (!access.AdminUp || !access.CarrierUp))
                return Fail(1, TssReachabilityFaultCategory.InterfaceUnavailable, "a participating interface is unavailable.", source.Role, destinationRole);
            var parent = FindInterface(FindDevice("CN-1"), "Fa0/0");
            if (parent != null && (!parent.AdminUp || !parent.CarrierUp))
                return Fail(1, TssReachabilityFaultCategory.InterfaceUnavailable, "a participating interface is unavailable.", source.Role, destinationRole);
            foreach (var trunkName in RequiredTrunks(source.Role))
            {
                var split = trunkName.IndexOf(':');
                var trunk = FindInterface(FindDevice(trunkName.Substring(0, split)), trunkName.Substring(split + 1));
                if (trunk != null && (!trunk.AdminUp || !trunk.CarrierUp))
                    return Fail(1, TssReachabilityFaultCategory.InterfaceUnavailable, "a participating interface is unavailable.", source.Role, destinationRole);
            }
        }

        var hops = new List<string>();
        Add(hops, source.Address);
        if (HasStructuredTopology(sourceDevice))
        {
            var l2 = CheckLayer2(source, sourceDevice, sourceInterface, profile, destinationRole, hops);
            if (l2.FailedLayer != 0) return l2;
        }
        else if (!source.Layer2Connected)
        {
            return Fail(2, TssReachabilityFaultCategory.VlanNotCarried, "switching path is unavailable.", source.Role, destinationRole, hops.ToArray());
        }

        if (!source.HasValidAddress || !TssNetworkAddress.IsAddress(source.Gateway) || !SameSubnet(source.Address, source.Mask, source.Gateway))
            return Fail(3, TssReachabilityFaultCategory.GatewayUnavailable, "endpoint addressing is invalid.", source.Role, destinationRole, hops.ToArray());

        if (destinationAddress.Equals(profile.WanProviderAddress, StringComparison.OrdinalIgnoreCase))
            return CheckProvider(source, profile, destinationRole, hops, HasStructuredTopology(sourceDevice));

        var vlan = VlanFor(source.Role, source.Address, profile);
        var routerInterface = FindRouterInterface(vlan, profile);
        if (HasStructuredTopology(sourceDevice) && routerInterface == null)
            return Fail(3, TssReachabilityFaultCategory.GatewayUnavailable, "router gateway is unavailable.", source.Role, destinationRole, hops.ToArray());
        if (routerInterface != null)
        {
            Add(hops, routerInterface.Address);
            if (!routerInterface.AdminUp || !routerInterface.CarrierUp || routerInterface.Dot1qVlan != vlan || routerInterface.Address != source.Gateway || routerInterface.Mask != "255.255.255.0")
                return Fail(3, TssReachabilityFaultCategory.GatewayUnavailable, "router gateway is unavailable.", source.Role, destinationRole, hops.ToArray());
        }

        var destination = FindEndpointByAddress(destinationAddress);
        if (destination != null && (!destination.HasValidAddress || !GatewayMatches(destination, profile)))
            return Fail(3, TssReachabilityFaultCategory.DestinationUnavailable, "destination addressing is invalid.", source.Role, destinationRole, hops.ToArray());

        Add(hops, destinationAddress);
        return new TssReachabilityResult(true, 0, TssReachabilityFaultCategory.None, "Reachable.", source.Role, destinationRole, hops);
    }

    private TssReachabilityResult CheckLayer2(EndpointNetworkState source, NetworkDeviceState sourceDevice, NetworkInterfaceState sourceInterface, TssNetworkRunProfile profile, string destinationRole, List<string> hops)
    {
        var vlan = VlanFor(source.Role, source.Address, profile);
        var accessDevice = FindAccessDevice(source, sourceDevice);
        var access = sourceDevice != null && sourceDevice.Category == EquipmentCategory.Switch ? sourceInterface : FindAccessInterface(accessDevice, source.Role);
        if (accessDevice == null || access == null || access.IsTrunk || access.AccessVlan != vlan)
            return Fail(2, TssReachabilityFaultCategory.VlanNotCarried, "source access VLAN is unavailable.", source.Role, destinationRole, hops.ToArray());
        if (!ActiveVlan(accessDevice, vlan))
            return Fail(2, TssReachabilityFaultCategory.VlanNotCarried, "source VLAN is unavailable.", source.Role, destinationRole, hops.ToArray());

        var dataPath = source.Role.StartsWith("Data", StringComparison.OrdinalIgnoreCase);
        var switches = dataPath ? new[] { "AN-2", "AN-1" } : new[] { "AN-1" };
        foreach (var name in switches)
        {
            var device = FindDevice(name);
            if (device == null || !ActiveVlan(device, vlan))
                return Fail(2, TssReachabilityFaultCategory.VlanNotCarried, "required VLAN is not active.", source.Role, destinationRole, hops.ToArray());
        }

        var trunkNames = RequiredTrunks(source.Role);
        int? native = null;
        foreach (var trunkName in trunkNames)
        {
            var split = trunkName.IndexOf(':');
            var device = FindDevice(trunkName.Substring(0, split));
            var trunk = FindInterface(device, trunkName.Substring(split + 1));
            if (device == null || trunk == null || !trunk.IsTrunk || !trunk.AdminUp || !trunk.CarrierUp || !trunk.AllowedVlans.Contains(vlan))
                return Fail(2, TssReachabilityFaultCategory.VlanNotCarried, "VLAN is not carried across the trunk.", source.Role, destinationRole, hops.ToArray());
            if ((native.HasValue && native.Value != trunk.NativeVlan) || trunk.NativeVlan != profile.NativeVlan)
                return Fail(2, TssReachabilityFaultCategory.VlanNotCarried, "trunk native VLANs do not agree.", source.Role, destinationRole, hops.ToArray());
            native = trunk.NativeVlan;
        }
        return new TssReachabilityResult(true, 0, TssReachabilityFaultCategory.None, string.Empty, source.Role, destinationRole, hops);
    }

    private TssReachabilityResult CheckProvider(EndpointNetworkState source, TssNetworkRunProfile profile, string destinationRole, List<string> hops, bool structured)
    {
        if (!GatewayMatches(source, profile))
            return Fail(3, TssReachabilityFaultCategory.GatewayUnavailable, "default gateway is unavailable.", source.Role, destinationRole, hops.ToArray());
        if (structured && !_state.ProviderDemarcConnected)
            return Fail(1, TssReachabilityFaultCategory.PhysicalPathUnavailable, "provider handoff is not connected through the DEMARC.", source.Role, destinationRole, hops.ToArray());
        var router = FindDevice("CN-1");
        var wan = FindInterface(router, "Fa0/1");
        if (structured && (router == null || wan == null || !wan.AdminUp || !wan.CarrierUp || wan.Address != profile.WanCustomerAddress || wan.Mask != "255.255.255.252"))
            return Fail(3, TssReachabilityFaultCategory.ProviderRouteUnavailable, "provider interface is unavailable.", source.Role, destinationRole, hops.ToArray());
        if (router != null && !HasProviderRoute(router, profile))
            return Fail(3, TssReachabilityFaultCategory.ProviderRouteUnavailable, "provider route is unavailable.", source.Role, destinationRole, hops.ToArray());
        Add(hops, profile.WanCustomerAddress); Add(hops, profile.WanProviderAddress);
        return new TssReachabilityResult(true, 0, TssReachabilityFaultCategory.None, "Reachable.", source.Role, destinationRole, hops);
    }

    private NetworkDeviceState FindDevice(EndpointNetworkState endpoint)
    {
        if (endpoint != null && !string.IsNullOrWhiteSpace(endpoint.DeviceKey) && _state.TryGetDevice(endpoint.DeviceKey, out var device)) return device;
        return FindDevice(endpoint?.Role);
    }

    private NetworkDeviceState FindAccessDevice(EndpointNetworkState source, NetworkDeviceState sourceDevice)
    {
        if (sourceDevice != null && sourceDevice.Category == EquipmentCategory.Switch) return sourceDevice;
        return FindDevice(source.Role.StartsWith("Data", StringComparison.OrdinalIgnoreCase) ? "AN-2" : "AN-1");
    }

    private NetworkDeviceState FindDevice(string keyOrRole)
    {
        if (string.IsNullOrWhiteSpace(keyOrRole)) return null;
        if (_state.TryGetDevice(keyOrRole, out var exact)) return exact;
        foreach (var device in _state.Devices)
            if (string.Equals(device.ExpectedRole, keyOrRole, StringComparison.OrdinalIgnoreCase) || string.Equals(device.Hostname, keyOrRole, StringComparison.OrdinalIgnoreCase)) return device;
        foreach (var binding in _state.Profile?.RoleBindings ?? Array.Empty<NetworkRoleBinding>())
            if (binding != null && string.Equals(binding.role, keyOrRole, StringComparison.OrdinalIgnoreCase) && _state.TryGetDevice(binding.deviceKey, out var bound)) return bound;
        return null;
    }

    private static NetworkInterfaceState FindSourceInterface(EndpointNetworkState source, NetworkDeviceState device)
    {
        if (device == null) return null;
        if (!string.IsNullOrWhiteSpace(source.InterfaceName) && device.Interfaces.TryGetValue(source.InterfaceName, out var named)) return named;
        foreach (var iface in device.Interfaces.Values)
            if (string.Equals(iface.EndpointId, source.Key, StringComparison.OrdinalIgnoreCase)) return iface;
        return null;
    }

    private static NetworkInterfaceState FindAccessInterface(NetworkDeviceState device, string role)
    {
        if (device == null) return null;
        var name = role switch
        {
            "IT1" => "Fa0/2", "IT2" => "Fa0/5", "Reception1" => "Fa0/3", "Reception2" => "Fa0/4",
            "Data1" => "Fa0/2", "Data2" => "Fa0/3", "Data3" => "Fa0/4", "AP-1" => "Fa0/7", _ => null
        };
        if (name == null && role.IndexOf("laptop", StringComparison.OrdinalIgnoreCase) >= 0)
            name = "Fa0/7";
        return FindInterface(device, name);
    }

    private static string[] RequiredTrunks(string role)
    {
        return role.StartsWith("Data", StringComparison.OrdinalIgnoreCase)
            ? new[] { "AN-2:Fa0/1", "AN-1:Fa0/1", "AN-1:Fa0/6" }
            : new[] { "AN-1:Fa0/6" };
    }

    private NetworkInterfaceState FindRouterInterface(int vlan, TssNetworkRunProfile profile)
    {
        foreach (var device in _state.Devices)
            if (device.Category == EquipmentCategory.Router)
                foreach (var iface in device.Interfaces.Values)
                    if (iface.Dot1qVlan == vlan && iface.Address == GatewayForVlan(vlan, profile)) return iface;
        return null;
    }

    private static NetworkInterfaceState FindInterface(NetworkDeviceState device, string name)
    {
        if (device == null || string.IsNullOrWhiteSpace(name)) return null;
        foreach (var iface in device.Interfaces.Values)
            if (Canonical(iface.Name) == Canonical(name)) return iface;
        return null;
    }

    private bool HasStructuredTopology(NetworkDeviceState sourceDevice)
    {
        if (sourceDevice != null && sourceDevice.Category != EquipmentCategory.DesktopPc && sourceDevice.Interfaces.Count > 0) return true;
        foreach (var device in _state.Devices)
            if ((device.Category == EquipmentCategory.Switch || device.Category == EquipmentCategory.Router) && device.Interfaces.Count > 0) return true;
        return false;
    }
    private static bool ActiveVlan(NetworkDeviceState device, int vlan) => device != null && device.Vlans.TryGetValue(vlan, out var name) && (name ?? string.Empty).IndexOf("suspend", StringComparison.OrdinalIgnoreCase) < 0;
    private static bool HasProviderRoute(NetworkDeviceState router, TssNetworkRunProfile profile)
    {
        foreach (var route in router.Routes)
            if (route.Network == "0.0.0.0" && route.Mask == "0.0.0.0" && route.NextHop == profile.WanProviderAddress) return true;
        return false;
    }

    private static bool GatewayMatches(EndpointNetworkState source, TssNetworkRunProfile profile) => source.Gateway == EndpointNetworkState.GatewayFor(source.Address, profile);
    private static int VlanFor(string role, string address, TssNetworkRunProfile profile)
    {
        if (role.StartsWith("Reception", StringComparison.OrdinalIgnoreCase) || address.StartsWith(profile.ReceptionNetwork + ".", StringComparison.Ordinal)) return profile.ReceptionVlan;
        if (role.StartsWith("Data", StringComparison.OrdinalIgnoreCase) || address.StartsWith(profile.DataNetwork + ".", StringComparison.Ordinal)) return profile.DataVlan;
        return profile.ManagementVlan;
    }
    private static string GatewayForVlan(int vlan, TssNetworkRunProfile profile)
    {
        if (vlan == profile.ReceptionVlan) return profile.ReceptionNetwork + ".1";
        if (vlan == profile.DataVlan) return profile.DataNetwork + ".1";
        return profile.ManagementNetwork + ".1";
    }

    private EndpointNetworkState FindEndpointByAddress(string address)
    {
        foreach (var endpoint in _state.Endpoints)
            if (endpoint.Address == address) return endpoint;
        return null;
    }
    private static string ResolveRole(string address, TssNetworkRunProfile profile)
    {
        if (profile == null) return string.Empty;
        if (address == profile.WanProviderAddress) return "Provider";
        foreach (var pair in profile.EndpointAddresses) if (pair.Value == address) return pair.Key;
        if (address == profile.ManagementNetwork + ".1") return "Management gateway";
        if (address == profile.ReceptionNetwork + ".1") return "Reception gateway";
        if (address == profile.DataNetwork + ".1") return "Data gateway";
        return string.Empty;
    }
    private static TssReachabilityResult Fail(int layer, TssReachabilityFaultCategory category, string diagnostic, string source, string destination, params string[] hops) => new(false, layer, category, $"Layer {layer}: {diagnostic}", source, destination, hops ?? Array.Empty<string>());
    private static void Add(List<string> hops, string value) { if (!string.IsNullOrWhiteSpace(value) && (hops.Count == 0 || hops[hops.Count - 1] != value)) hops.Add(value); }
    private static string Canonical(string value) => (value ?? string.Empty).Replace("FastEthernet", "Fa", StringComparison.OrdinalIgnoreCase).Replace("GigabitEthernet", "Gi", StringComparison.OrdinalIgnoreCase).Replace("/", string.Empty).Replace(".", string.Empty).ToUpperInvariant();
    private static bool SameSubnet(string address, string mask, string other)
    {
        if (!TryIpv4(address, out var a) || !TryIpv4(mask, out var m) || !TryIpv4(other, out var b)) return false;
        return (a & m) == (b & m);
    }
    private static bool TryIpv4(string value, out uint result)
    {
        result = 0; if (!TssNetworkAddress.IsAddress(value)) return false;
        foreach (var part in value.Split('.')) result = (result << 8) | uint.Parse(part);
        return true;
    }
}
