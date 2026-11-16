using System;
using System.Collections.Generic;
using System.Linq;
// Shared reachability is the source of truth for endpoint and device probes.


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

        return EvaluateSource(source, FindDevice(source), destinationAddress);
    }

    public TssReachabilityResult EvaluateDevice(string sourceKey, string destinationAddress)
    {
        if (_state == null || !_state.TryGetDevice(sourceKey, out var sourceDevice))
            return Fail(1, TssReachabilityFaultCategory.PhysicalPathUnavailable, "source device is unavailable.", string.Empty, string.Empty);

        var profile = _state.Profile;
        var localInterface = sourceDevice.Interfaces.Values.FirstOrDefault(iface => string.Equals(iface.Address, destinationAddress, StringComparison.OrdinalIgnoreCase))
            ?? sourceDevice.Svis.Values.FirstOrDefault(iface => string.Equals(iface.Address, destinationAddress, StringComparison.OrdinalIgnoreCase));
        if (localInterface != null)
        {
            if (!TssNetworkAddress.IsAddress(localInterface.Address) || !TssNetworkAddress.IsMask(localInterface.Mask) || !IsDeviceInterfaceOperational(sourceDevice, localInterface, profile))
                return Fail(3, TssReachabilityFaultCategory.InterfaceUnavailable, "local interface is unavailable.", sourceDevice.Hostname, sourceDevice.Hostname);
            return new TssReachabilityResult(true, 0, TssReachabilityFaultCategory.None, "Reachable.", sourceDevice.Hostname, sourceDevice.Hostname, new[] { localInterface.Address });
        }
        var destinationDevice = FindDeviceInterfaceByAddress(destinationAddress, out var destinationSvi);
        var destinationRole = ResolveRole(destinationAddress, profile);
        if (destinationDevice != null)
            destinationRole = destinationDevice.ExpectedRole ?? destinationDevice.Hostname;
        if (string.IsNullOrEmpty(destinationRole))
            return Fail(3, TssReachabilityFaultCategory.DestinationUnavailable, "destination does not respond.", sourceDevice.Hostname, string.Empty);
        var knownEndpointFailure = ValidateKnownEndpointDestination(destinationAddress, destinationRole, sourceDevice.Hostname);
        if (knownEndpointFailure.HasValue) return knownEndpointFailure.Value;

        var sourceInterface = SelectDeviceSourceInterface(sourceDevice, destinationAddress, profile);
        if (sourceInterface == null || !IsDeviceInterfaceOperational(sourceDevice, sourceInterface, profile))
            return Fail(3, TssReachabilityFaultCategory.InterfaceUnavailable, "source has no operational Layer 3 interface.", sourceDevice.Hostname, destinationRole);

        if (!TssNetworkAddress.IsAddress(sourceInterface.Address) || !TssNetworkAddress.IsMask(sourceInterface.Mask))
            return Fail(3, TssReachabilityFaultCategory.InterfaceUnavailable, "source interface has no valid Layer 3 address.", sourceDevice.Hostname, destinationRole);

        var source = new EndpointNetworkState("__device_probe__", sourceDevice.Hostname, profile)
        {
            Address = sourceInterface.Address,
            Mask = sourceInterface.Mask,
            Gateway = sourceDevice.Category == EquipmentCategory.Switch ? sourceDevice.DefaultGateway : sourceInterface.Address
        };

        var hops = new List<string> { source.Address };
        if (destinationAddress.Equals(profile.WanProviderAddress, StringComparison.OrdinalIgnoreCase))
            return EvaluateDeviceProvider(sourceDevice, sourceInterface, source, destinationRole, hops);

        if (FindEndpointByAddress(destinationAddress) is { } destination)
        {
            if (!destination.HasValidAddress)
                return Fail(3, TssReachabilityFaultCategory.DestinationUnavailable, "destination addressing is invalid.", source.Role, destinationRole, hops.ToArray());

            if (sourceDevice.Category == EquipmentCategory.Switch
                && TssNetworkAddress.IsInSameSubnet(source.Address, source.Mask, destinationAddress))
            {
                if (!TssNetworkAddress.IsInSameSubnet(destination.Address, destination.Mask, source.Address))
                    return Fail(3, TssReachabilityFaultCategory.DestinationUnavailable, "destination addressing is invalid.", source.Role, destinationRole, hops.ToArray());
                var localFailure = CheckLocalEndpoint(destination, source.Role, destinationRole);
                if (localFailure.HasValue) return localFailure.Value;
                var access = FindAccessDevice(destination, FindDevice(destination));
                var trunkFailure = CheckLocalSwitchPath(sourceDevice, access, profile.ManagementVlan, source.Role, destinationRole);
                if (trunkFailure.HasValue) return trunkFailure.Value;
                Add(hops, destinationAddress);
                return new TssReachabilityResult(true, 0, TssReachabilityFaultCategory.None, "Reachable.", source.Role, destinationRole, hops);
            }

            if (!GatewayMatches(destination, profile))
                return Fail(3, TssReachabilityFaultCategory.DestinationUnavailable, "destination addressing is invalid.", source.Role, destinationRole, hops.ToArray());

            var destinationPath = Evaluate(destination.Key, destination.Gateway);
            if (!destinationPath.Success)
                return Fail(destinationPath.FailedLayer, destinationPath.FaultCategory, destinationPath.Diagnostic.Substring(destinationPath.Diagnostic.IndexOf(':') + 1).Trim(), source.Role, destinationRole, hops.ToArray());
        }

        // Switch SVIs on the same subnet communicate over the trunk without a router.
        var directSwitchPing = sourceDevice.Category == EquipmentCategory.Switch
            && destinationDevice != null && destinationDevice.Category == EquipmentCategory.Switch
            && destinationSvi != null
            && TssNetworkAddress.IsInSameSubnet(source.Address, source.Mask, destinationAddress);
        var routeFailure = directSwitchPing ? null : ValidateDeviceRoute(sourceDevice, sourceInterface, source, destinationAddress, destinationRole, hops);
        if (routeFailure.HasValue)
            return routeFailure.Value;

        if (destinationDevice != null && destinationSvi != null)
        {
            if (!IsDeviceInterfaceOperational(destinationDevice, destinationSvi, profile))
                return Fail(3, TssReachabilityFaultCategory.InterfaceUnavailable, "destination SVI is unavailable.", source.Role, destinationRole, hops.ToArray());
            var vlan = profile.ManagementVlan;
            var pathStart = directSwitchPing ? sourceDevice : FindDevice(profile.RouterRole);
            var pathFailure = CheckTrunkPath(pathStart, destinationDevice, vlan, source.Role, destinationRole);
            if (pathFailure.HasValue) return pathFailure.Value;
        }

        Add(hops, destinationAddress);
        return new TssReachabilityResult(true, 0, TssReachabilityFaultCategory.None, "Reachable.", source.Role, destinationRole, hops);
    }

    private TssReachabilityResult EvaluateSource(EndpointNetworkState source, NetworkDeviceState sourceDevice, string destinationAddress)
    {
        var profile = _state.Profile;

        if (source.HasValidAddress && string.Equals(source.Address, destinationAddress, StringComparison.OrdinalIgnoreCase))
            return new TssReachabilityResult(true, 0, TssReachabilityFaultCategory.None, "Reachable.", source.Role, source.Role, new[] { source.Address });

        var destinationEndpoint = FindEndpointByAddress(destinationAddress);
        var destinationDevice = FindDeviceInterfaceByAddress(destinationAddress, out var destinationInterface);
        var destinationRole = destinationDevice != null ? destinationDevice.ExpectedRole ?? destinationDevice.Hostname
            : destinationEndpoint != null ? destinationEndpoint.Role : ResolveRole(destinationAddress, profile);
        if (string.IsNullOrEmpty(destinationRole))
            return Fail(3, TssReachabilityFaultCategory.DestinationUnavailable, "destination does not respond.", source.Role, string.Empty, source.Address);
        var knownEndpointFailure = ValidateKnownEndpointDestination(destinationAddress, destinationRole, source.Role);
        if (knownEndpointFailure.HasValue) return knownEndpointFailure.Value;

        var isGateway = profile.RouterSubinterfaceRequirements.Any(requirement => requirement != null &&
            string.Equals(destinationAddress, profile.NetworkAddress(requirement.networkName, requirement.addressSuffix), StringComparison.OrdinalIgnoreCase));
        var local = !isGateway && source.HasValidAddress
            && TssNetworkAddress.IsInSameSubnet(source.Address, source.Mask, destinationAddress)
            && (destinationEndpoint != null || destinationDevice?.Category == EquipmentCategory.Switch
                && destinationInterface != null && destinationInterface.Name.StartsWith("Vlan", StringComparison.OrdinalIgnoreCase));
        if (local)
        {
            var sourceFailure = CheckLocalEndpoint(source, source.Role, destinationRole);
            if (sourceFailure.HasValue) return sourceFailure.Value;
            var sourceSwitch = FindAccessDevice(source, sourceDevice);
            var destinationSwitch = destinationEndpoint != null
                ? FindAccessDevice(destinationEndpoint, FindDevice(destinationEndpoint)) : destinationDevice;
            var trunkFailure = CheckLocalSwitchPath(sourceSwitch, destinationSwitch,
                VlanForEndpoint(source, profile), source.Role, destinationRole);
            if (trunkFailure.HasValue) return trunkFailure.Value;
            if (destinationEndpoint != null)
            {
                if (!destinationEndpoint.HasValidAddress
                    || !TssNetworkAddress.IsInSameSubnet(destinationEndpoint.Address, destinationEndpoint.Mask, source.Address))
                    return Fail(3, TssReachabilityFaultCategory.DestinationUnavailable, "destination addressing is invalid.", source.Role, destinationRole);
                var destinationFailure = CheckLocalEndpoint(destinationEndpoint, source.Role, destinationRole);
                if (destinationFailure.HasValue) return destinationFailure.Value;
            }
            else if (!ActiveVlan(destinationDevice, VlanFor(destinationAddress, profile)))
                return Fail(2, TssReachabilityFaultCategory.VlanNotCarried, "management VLAN is unavailable.", source.Role, destinationRole);
            else if (!TssNetworkAddress.IsMask(destinationInterface.Mask)
                || !IsDeviceInterfaceOperational(destinationDevice, destinationInterface, profile))
                return Fail(3, TssReachabilityFaultCategory.InterfaceUnavailable, "destination SVI is unavailable.", source.Role, destinationRole);
            return new TssReachabilityResult(true, 0, TssReachabilityFaultCategory.None, "Reachable.", source.Role, destinationRole, new[] { source.Address, destinationAddress });
        }

        if (isGateway && HasStructuredTopology(sourceDevice))
        {
            var sourceFailure = CheckLocalEndpoint(source, source.Role, destinationRole);
            if (sourceFailure.HasValue) return sourceFailure.Value;
            if (!source.HasValidAddress || !TssNetworkAddress.IsAddress(source.Gateway)
                || !SameSubnet(source.Address, source.Mask, source.Gateway)
                || !string.Equals(source.Gateway, destinationAddress, StringComparison.OrdinalIgnoreCase))
                return Fail(3, TssReachabilityFaultCategory.GatewayUnavailable, "endpoint addressing is invalid.", source.Role, destinationRole);
            var gatewayVlan = VlanForEndpoint(source, profile);
            var accessSwitch = FindAccessDevice(source, sourceDevice);
            var router = FindDevice(profile.RouterRole);
            var switchPathFailure = CheckTrunkPath(accessSwitch, router, gatewayVlan, source.Role, destinationRole);
            if (switchPathFailure.HasValue) return switchPathFailure.Value;
            var gatewayInterface = FindRouterInterface(gatewayVlan, profile);
            if (router == null || gatewayInterface == null || !router.IsInterfaceOperational(gatewayInterface)
                || gatewayInterface.Dot1qVlan != gatewayVlan || gatewayInterface.Mask != RouterMaskForVlan(gatewayVlan, profile))
                return Fail(3, TssReachabilityFaultCategory.GatewayUnavailable, "router gateway is unavailable.", source.Role, destinationRole);
            return new TssReachabilityResult(true, 0, TssReachabilityFaultCategory.None, "Reachable.", source.Role, destinationRole, new[] { source.Address, destinationAddress });
        }

        var sourceInterface = FindSourceInterface(source, sourceDevice);
        if (!source.Layer1Connected)
            return Fail(1, TssReachabilityFaultCategory.PhysicalPathUnavailable, "no usable physical path.", source.Role, destinationRole);
        if (sourceInterface != null && !sourceDevice.IsInterfaceOperational(sourceInterface))
            return Fail(1, TssReachabilityFaultCategory.InterfaceUnavailable, "a participating interface is unavailable.", source.Role, destinationRole);
        if (HasStructuredTopology(sourceDevice))
        {
            var accessDevice = FindAccessDevice(source, sourceDevice);
            var access = FindAccessInterface(accessDevice, source);
            if (access != null && !accessDevice.IsInterfaceOperational(access))
                return Fail(1, TssReachabilityFaultCategory.InterfaceUnavailable, "a participating interface is unavailable.", source.Role, destinationRole);
            var router = FindDevice(_state.Profile.RouterRole);
            var parent = FindInterface(router, _state.Profile.RouterParentInterface);
            if (parent != null && !router.IsInterfaceOperational(parent))
                return Fail(1, TssReachabilityFaultCategory.InterfaceUnavailable, "a participating interface is unavailable.", source.Role, destinationRole);
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

        var vlan = VlanForEndpoint(source, profile);
        var routerInterface = FindRouterInterface(vlan, profile);
        if (HasStructuredTopology(sourceDevice) && routerInterface == null)
            return Fail(3, TssReachabilityFaultCategory.GatewayUnavailable, "router gateway is unavailable.", source.Role, destinationRole, hops.ToArray());
        if (routerInterface != null)
        {
            Add(hops, routerInterface.Address);
            var router = FindDevice(profile.RouterRole);
            if (router == null || !router.IsInterfaceOperational(routerInterface) || routerInterface.Dot1qVlan != vlan || routerInterface.Address != source.Gateway || routerInterface.Mask != RouterMaskForVlan(vlan, profile))
                return Fail(3, TssReachabilityFaultCategory.GatewayUnavailable, "router gateway is unavailable.", source.Role, destinationRole, hops.ToArray());
        }

        if (destinationAddress.Equals(profile.WanProviderAddress, StringComparison.OrdinalIgnoreCase))
            return CheckProvider(source, profile, destinationRole, hops, HasStructuredTopology(sourceDevice));

        var destination = FindEndpointByAddress(destinationAddress);
        if (destination != null && (!destination.HasValidAddress || !GatewayMatches(destination, profile)))
            return Fail(3, TssReachabilityFaultCategory.DestinationUnavailable, "destination addressing is invalid.", source.Role, destinationRole, hops.ToArray());
        if (destination != null)
        {
            var destinationPath = Evaluate(destination.Key, destination.Gateway);
            if (!destinationPath.Success)
                return Fail(destinationPath.FailedLayer, destinationPath.FaultCategory,
                    destinationPath.Diagnostic.Substring(destinationPath.Diagnostic.IndexOf(':') + 1).Trim(), source.Role, destinationRole, hops.ToArray());
        }

        Add(hops, destinationAddress);
        return new TssReachabilityResult(true, 0, TssReachabilityFaultCategory.None, "Reachable.", source.Role, destinationRole, hops);
    }

    private TssReachabilityResult? CheckLocalEndpoint(EndpointNetworkState endpoint, string sourceRole, string destinationRole)
    {
        if (!endpoint.Layer1Connected)
            return Fail(1, TssReachabilityFaultCategory.PhysicalPathUnavailable, "no usable physical path.", sourceRole, destinationRole);
        var device = FindDevice(endpoint);
        var ownInterface = FindSourceInterface(endpoint, device);
        if (ownInterface != null && !device.IsInterfaceOperational(ownInterface))
            return Fail(1, TssReachabilityFaultCategory.InterfaceUnavailable, "a participating interface is unavailable.", sourceRole, destinationRole);
        if (!endpoint.Layer2Connected)
            return Fail(2, TssReachabilityFaultCategory.VlanNotCarried, "switching path is unavailable.", sourceRole, destinationRole);
        var accessDevice = FindAccessDevice(endpoint, device);
        if (!HasStructuredTopology(device))
            return null;
        var access = FindAccessInterface(accessDevice, endpoint);
        if (accessDevice == null || access == null)
            return Fail(2, TssReachabilityFaultCategory.VlanNotCarried, "source access VLAN is unavailable.", sourceRole, destinationRole);
        if (!accessDevice.IsInterfaceOperational(access))
            return Fail(1, TssReachabilityFaultCategory.InterfaceUnavailable, "a participating interface is unavailable.", sourceRole, destinationRole);
        var vlan = VlanForEndpoint(endpoint, _state.Profile);
        if (access.IsTrunk || access.AccessVlan != vlan || !ActiveVlan(accessDevice, vlan))
            return Fail(2, TssReachabilityFaultCategory.VlanNotCarried, "source access VLAN is unavailable.", sourceRole, destinationRole);
        return null;
    }

    private TssReachabilityResult? CheckLocalSwitchPath(NetworkDeviceState sourceSwitch, NetworkDeviceState destinationSwitch, int vlan, string sourceRole, string destinationRole)
    {
        if (sourceSwitch == null || destinationSwitch == null)
            return HasStructuredTopology(null)
                ? Fail(2, TssReachabilityFaultCategory.VlanNotCarried, "a switch path is not configured.", sourceRole, destinationRole)
                : null;
        return CheckTrunkPath(sourceSwitch, destinationSwitch, vlan, sourceRole, destinationRole);
    }

    private TssReachabilityResult? CheckTrunkPath(NetworkDeviceState start, NetworkDeviceState end, int vlan, string sourceRole, string destinationRole)
    {
        if (start == null || end == null)
            return HasStructuredTopology(null)
                ? Fail(2, TssReachabilityFaultCategory.VlanNotCarried, "a trunk path is not configured.", sourceRole, destinationRole)
                : null;
        if (start == end)
            return null;

        var queue = new Queue<NetworkDeviceState>();
        var visited = new HashSet<string>(StringComparer.OrdinalIgnoreCase) { start.Key };
        var previous = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        var links = new Dictionary<string, NetworkTrunkRequirement>(StringComparer.OrdinalIgnoreCase);
        queue.Enqueue(start);

        while (queue.Count > 0 && !visited.Contains(end.Key))
        {
            var current = queue.Dequeue();
            foreach (var link in _state.Profile?.TrunkRequirements ?? Array.Empty<NetworkTrunkRequirement>())
            {
                if (link == null || string.IsNullOrWhiteSpace(link.peerDeviceRole) || string.IsNullOrWhiteSpace(link.peerInterfaceName))
                    continue;
                var first = FindDevice(link.deviceRole);
                var peer = FindDevice(link.peerDeviceRole);
                if (first == null || peer == null)
                    continue;

                var next = first.Key.Equals(current.Key, StringComparison.OrdinalIgnoreCase) ? peer
                    : peer.Key.Equals(current.Key, StringComparison.OrdinalIgnoreCase) ? first : null;
                if (next == null || !visited.Add(next.Key))
                    continue;

                previous[next.Key] = current.Key;
                links[next.Key] = link;
                queue.Enqueue(next);
            }
        }

        if (!visited.Contains(end.Key))
            return Fail(2, TssReachabilityFaultCategory.VlanNotCarried, "no configured trunk path reaches the destination.", sourceRole, destinationRole);

        var path = new List<NetworkTrunkRequirement>();
        for (var key = end.Key; !key.Equals(start.Key, StringComparison.OrdinalIgnoreCase); key = previous[key])
            path.Add(links[key]);
        path.Reverse();

        foreach (var link in path)
        {
            var first = FindDevice(link.deviceRole);
            var peer = FindDevice(link.peerDeviceRole);
            var firstPort = FindInterface(first, link.interfaceName);
            var peerPort = FindInterface(peer, link.peerInterfaceName);
            if (firstPort == null || peerPort == null || !first.IsInterfaceOperational(firstPort) || !peer.IsInterfaceOperational(peerPort))
                return Fail(1, TssReachabilityFaultCategory.InterfaceUnavailable, "a participating trunk interface is unavailable.", sourceRole, destinationRole);

            foreach (var pair in new[] { (Device: first, Port: firstPort), (Device: peer, Port: peerPort) })
            {
                if (pair.Device.Category == EquipmentCategory.Router)
                    continue;
                if (!pair.Port.IsTrunk || !pair.Port.AllowedVlans.Contains(vlan) || pair.Port.NativeVlan != _state.Profile.NativeVlan
                    || !ActiveVlan(pair.Device, vlan))
                    return Fail(2, TssReachabilityFaultCategory.VlanNotCarried, "VLAN is not carried across the trunk path.", sourceRole, destinationRole);
            }
        }

        return null;
    }

    private NetworkInterfaceState SelectDeviceSourceInterface(NetworkDeviceState device, string destinationAddress, TssNetworkRunProfile profile)
    {
        if (device == null || profile == null)
            return null;

        if (device.Category == EquipmentCategory.Router && destinationAddress.Equals(profile.WanProviderAddress, StringComparison.OrdinalIgnoreCase))
            return FindInterface(device, profile.RouterWanInterface);

        if (device.Category == EquipmentCategory.Router)
        {
            var role = ResolveRole(destinationAddress, profile);
            var vlan = VlanFor(destinationAddress, profile);
            var gateway = GatewayForVlan(vlan, profile);
            return device.Interfaces.Values.FirstOrDefault(iface => string.Equals(iface.Address, gateway, StringComparison.OrdinalIgnoreCase))
                ?? device.Interfaces.Values.FirstOrDefault(iface => TssNetworkAddress.IsInSameSubnet(iface.Address, iface.Mask, destinationAddress));
        }

        if (device.Category == EquipmentCategory.Switch)
            return device.Svis.Values.FirstOrDefault(iface => TssNetworkAddress.IsAddress(iface.Address) && TssNetworkAddress.IsMask(iface.Mask))
                ?? (TssNetworkAddress.IsAddress(device.ManagementAddress) && TssNetworkAddress.IsMask(device.ManagementMask)
                    ? new NetworkInterfaceState { Name = "Vlan1", Address = device.ManagementAddress, Mask = device.ManagementMask, AdminUp = true }
                    : null);

        return device.Interfaces.Values.FirstOrDefault(iface => TssNetworkAddress.IsAddress(iface.Address) && TssNetworkAddress.IsMask(iface.Mask));
    }

    private bool IsDeviceInterfaceOperational(NetworkDeviceState device, NetworkInterfaceState iface, TssNetworkRunProfile profile)
    {
        if (device == null || iface == null || !device.IsInterfaceAdminUp(iface))
            return false;

        if (device.Category != EquipmentCategory.Switch || !iface.Name.StartsWith("Vlan", StringComparison.OrdinalIgnoreCase))
            return device.IsInterfaceOperational(iface);

        var vlan = iface.Name.Equals("Vlan1", StringComparison.OrdinalIgnoreCase)
            ? profile.ManagementVlan
            : int.TryParse(iface.Name.Substring(4), out var parsed) ? parsed : 0;
        if (!ActiveVlan(device, vlan))
            return false;

        return device.Interfaces.Values.Any(port => device.IsInterfaceOperational(port)
            && (port.IsTrunk ? port.AllowedVlans.Contains(vlan) : port.AccessVlan == vlan));
    }

    private TssReachabilityResult EvaluateDeviceProvider(NetworkDeviceState sourceDevice, NetworkInterfaceState sourceInterface, EndpointNetworkState source, string destinationRole, List<string> hops)
    {
        var profile = _state.Profile;
        NetworkDeviceState router;
        if (sourceDevice.Category == EquipmentCategory.Router)
        {
            router = sourceDevice;
            if (sourceInterface.Address != profile.WanCustomerAddress || sourceInterface.Mask != "255.255.255.252")
                return Fail(3, TssReachabilityFaultCategory.ProviderRouteUnavailable, "provider interface is unavailable.", source.Role, destinationRole, hops.ToArray());
        }
        else
        {
            if (!TssNetworkAddress.IsAddress(sourceDevice.DefaultGateway)
                || !TssNetworkAddress.IsInSameSubnet(source.Address, source.Mask, sourceDevice.DefaultGateway))
                return Fail(3, TssReachabilityFaultCategory.GatewayUnavailable, "default gateway is unavailable.", source.Role, destinationRole, hops.ToArray());

            router = FindRouterByInterfaceAddress(sourceDevice.DefaultGateway);
            if (router == null)
                return Fail(3, TssReachabilityFaultCategory.GatewayUnavailable, "default gateway is unavailable.", source.Role, destinationRole, hops.ToArray());
            Add(hops, sourceDevice.DefaultGateway);
            sourceInterface = FindInterface(router, _state.Profile.RouterWanInterface);
        }

        if (!_state.ProviderDemarcConnected)
            return Fail(1, TssReachabilityFaultCategory.PhysicalPathUnavailable, "provider handoff is not connected through the DEMARC.", source.Role, destinationRole, hops.ToArray());
        if (sourceInterface == null || !router.IsInterfaceOperational(sourceInterface)
            || sourceInterface.Address != profile.WanCustomerAddress || sourceInterface.Mask != "255.255.255.252")
            return Fail(3, TssReachabilityFaultCategory.ProviderRouteUnavailable, "provider interface is unavailable.", source.Role, destinationRole, hops.ToArray());
        if (!HasProviderRoute(router, profile))
            return Fail(3, TssReachabilityFaultCategory.ProviderRouteUnavailable, "provider route is unavailable.", source.Role, destinationRole, hops.ToArray());

        Add(hops, profile.WanProviderAddress);
        return new TssReachabilityResult(true, 0, TssReachabilityFaultCategory.None, "Reachable.", source.Role, destinationRole, hops);
    }

    private TssReachabilityResult? ValidateDeviceRoute(NetworkDeviceState sourceDevice, NetworkInterfaceState sourceInterface, EndpointNetworkState source, string destinationAddress, string destinationRole, List<string> hops)
    {
        var profile = _state.Profile;
        var vlan = VlanFor(destinationAddress, profile);
        var gateway = GatewayForVlan(vlan, profile);
        if (sourceDevice.Category == EquipmentCategory.Router)
        {
            var routeInterface = sourceDevice.Interfaces.Values.FirstOrDefault(iface => string.Equals(iface.Address, gateway, StringComparison.OrdinalIgnoreCase));
            if (routeInterface == null || !sourceDevice.IsInterfaceOperational(routeInterface) || routeInterface.Dot1qVlan != vlan)
                return Fail(3, TssReachabilityFaultCategory.GatewayUnavailable, "router gateway is unavailable.", source.Role, destinationRole, hops.ToArray());
            var destinationEndpoint = FindEndpointByAddress(destinationAddress);
            if (destinationEndpoint != null)
            {
                var pathFailure = CheckTrunkPath(sourceDevice, FindAccessDevice(destinationEndpoint, FindDevice(destinationEndpoint)), vlan, source.Role, destinationRole);
                if (pathFailure.HasValue) return pathFailure.Value;
            }
            Add(hops, routeInterface.Address);
            return null;
        }

        if (sourceDevice.Category == EquipmentCategory.Switch)
        {
            if (!TssNetworkAddress.IsAddress(sourceDevice.DefaultGateway)
                || !TssNetworkAddress.IsInSameSubnet(source.Address, source.Mask, sourceDevice.DefaultGateway))
                return Fail(3, TssReachabilityFaultCategory.GatewayUnavailable, "default gateway is unavailable.", source.Role, destinationRole, hops.ToArray());

            var gatewayRouter = FindRouterByInterfaceAddress(sourceDevice.DefaultGateway);
            if (gatewayRouter == null)
                return Fail(3, TssReachabilityFaultCategory.GatewayUnavailable, "default gateway is unavailable.", source.Role, destinationRole, hops.ToArray());
            var sourcePathFailure = CheckTrunkPath(sourceDevice, gatewayRouter, profile.ManagementVlan, source.Role, destinationRole);
            if (sourcePathFailure.HasValue) return sourcePathFailure.Value;

            var sourceGatewayInterface = gatewayRouter.Interfaces.Values.FirstOrDefault(iface => string.Equals(iface.Address, sourceDevice.DefaultGateway, StringComparison.OrdinalIgnoreCase));
            if (sourceGatewayInterface == null || !gatewayRouter.IsInterfaceOperational(sourceGatewayInterface))
                return Fail(3, TssReachabilityFaultCategory.GatewayUnavailable, "default gateway is unavailable.", source.Role, destinationRole, hops.ToArray());
            Add(hops, sourceDevice.DefaultGateway);

            var destinationRouterInterface = gatewayRouter.Interfaces.Values.FirstOrDefault(iface => string.Equals(iface.Address, gateway, StringComparison.OrdinalIgnoreCase));
            if (destinationRouterInterface == null || !gatewayRouter.IsInterfaceOperational(destinationRouterInterface) || destinationRouterInterface.Dot1qVlan != vlan)
                return Fail(3, TssReachabilityFaultCategory.GatewayUnavailable, "destination route is unavailable.", source.Role, destinationRole, hops.ToArray());
            var destinationEndpoint = FindEndpointByAddress(destinationAddress);
            if (destinationEndpoint != null)
            {
                var destinationPathFailure = CheckTrunkPath(gatewayRouter, FindAccessDevice(destinationEndpoint, FindDevice(destinationEndpoint)), vlan, source.Role, destinationRole);
                if (destinationPathFailure.HasValue) return destinationPathFailure.Value;
            }
            Add(hops, destinationRouterInterface.Address);
            return null;
        }

        if (!TssNetworkAddress.IsInSameSubnet(sourceInterface.Address, sourceInterface.Mask, destinationAddress))
            return Fail(3, TssReachabilityFaultCategory.GatewayUnavailable, "default gateway is unavailable.", source.Role, destinationRole, hops.ToArray());
        return null;
    }

    private NetworkDeviceState FindRouterByInterfaceAddress(string address)
    {
        var router = FindDevice(_state.Profile?.RouterRole);
        return router != null && router.Category == EquipmentCategory.Router
            && router.Interfaces.Values.Any(iface => string.Equals(iface.Address, address, StringComparison.OrdinalIgnoreCase))
            ? router : null;
    }

    private TssReachabilityResult CheckLayer2(EndpointNetworkState source, NetworkDeviceState sourceDevice, NetworkInterfaceState sourceInterface, TssNetworkRunProfile profile, string destinationRole, List<string> hops)
    {
        var vlan = VlanForEndpoint(source, profile);
        var accessDevice = FindAccessDevice(source, sourceDevice);
        var access = sourceDevice != null && sourceDevice.Category == EquipmentCategory.Switch ? sourceInterface : FindAccessInterface(accessDevice, source);
        if (accessDevice == null || access == null || access.IsTrunk || access.AccessVlan != vlan)
            return Fail(2, TssReachabilityFaultCategory.VlanNotCarried, "source access VLAN is unavailable.", source.Role, destinationRole, hops.ToArray());
        if (!ActiveVlan(accessDevice, vlan))
            return Fail(2, TssReachabilityFaultCategory.VlanNotCarried, "source VLAN is unavailable.", source.Role, destinationRole, hops.ToArray());

        var pathFailure = CheckTrunkPath(accessDevice, FindDevice(profile.RouterRole), vlan, source.Role, destinationRole);
        return pathFailure ?? new TssReachabilityResult(true, 0, TssReachabilityFaultCategory.None, string.Empty, source.Role, destinationRole, hops);
    }

    private TssReachabilityResult CheckProvider(EndpointNetworkState source, TssNetworkRunProfile profile, string destinationRole, List<string> hops, bool structured)
    {
        if (!GatewayMatches(source, profile))
            return Fail(3, TssReachabilityFaultCategory.GatewayUnavailable, "default gateway is unavailable.", source.Role, destinationRole, hops.ToArray());
        if (structured && !_state.ProviderDemarcConnected)
            return Fail(1, TssReachabilityFaultCategory.PhysicalPathUnavailable, "provider handoff is not connected through the DEMARC.", source.Role, destinationRole, hops.ToArray());
        var router = FindDevice(profile.RouterRole);
        var wan = FindInterface(router, profile.RouterWanInterface);
        if (structured && (router == null || wan == null || !router.IsInterfaceOperational(wan) || wan.Address != profile.WanCustomerAddress || wan.Mask != "255.255.255.252"))
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
        foreach (var required in _state.Profile?.RequiredPorts ?? Array.Empty<NetworkRequiredPort>())
            if (required != null && string.Equals(required.endpointId, source.Role, StringComparison.OrdinalIgnoreCase))
                return FindDevice(required.deviceRole);
        if (source.Category is EquipmentCategory.AccessPoint or EquipmentCategory.Laptop)
            return FindDevice(_state.Profile?.AccessPointSwitchRole);
        return null;
    }

    private NetworkDeviceState FindDevice(string keyOrRole)
    {
        if (string.IsNullOrWhiteSpace(keyOrRole)) return null;
        if (_state.TryGetDevice(keyOrRole, out var exact)) return exact;
        foreach (var device in _state.Devices)
            if (string.Equals(device.ExpectedRole, keyOrRole, StringComparison.OrdinalIgnoreCase)) return device;
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

    private NetworkInterfaceState FindAccessInterface(NetworkDeviceState device, EndpointNetworkState endpoint)
    {
        if (device == null) return null;
        var profile = _state.Profile;
        foreach (var required in profile?.RequiredPorts ?? Array.Empty<NetworkRequiredPort>())
            if (required != null && string.Equals(required.endpointId, endpoint.Role, StringComparison.OrdinalIgnoreCase)
                && string.Equals(FindDevice(required.deviceRole)?.Key, device.Key, StringComparison.OrdinalIgnoreCase))
                return FindInterface(device, required.interfaceName);
        if (endpoint.Category is EquipmentCategory.AccessPoint or EquipmentCategory.Laptop
            && string.Equals(FindDevice(profile?.AccessPointSwitchRole)?.Key, device.Key, StringComparison.OrdinalIgnoreCase))
            return FindInterface(device, profile?.AccessPointSwitchPort);
        return null;
    }

    private NetworkInterfaceState FindRouterInterface(int vlan, TssNetworkRunProfile profile)
    {
        var router = FindDevice(profile?.RouterRole);
        if (router != null && router.Category == EquipmentCategory.Router)
            foreach (var iface in router.Interfaces.Values)
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
    private static bool ActiveVlan(NetworkDeviceState device, int vlan) => device != null && device.Vlans.ContainsKey(vlan) && !device.IsVlanSuspended(vlan);
    private static bool HasProviderRoute(NetworkDeviceState router, TssNetworkRunProfile profile)
    {
        foreach (var route in router.Routes)
            if (route.Network == "0.0.0.0" && route.Mask == "0.0.0.0" && route.NextHop == profile.WanProviderAddress) return true;
        return false;
    }

    private static bool GatewayMatches(EndpointNetworkState source, TssNetworkRunProfile profile) => source.Gateway == EndpointNetworkState.GatewayFor(source.Address, profile);
    private int VlanForEndpoint(EndpointNetworkState endpoint, TssNetworkRunProfile profile)
    {
        if (TssNetworkAddress.IsAddress(endpoint.Address))
            return VlanFor(endpoint.Address, profile);
        if (endpoint.Category is EquipmentCategory.AccessPoint or EquipmentCategory.Laptop)
            return profile.ReceptionVlan;
        foreach (var required in profile.RequiredPorts ?? Array.Empty<NetworkRequiredPort>())
            if (required != null && string.Equals(required.endpointId, endpoint.Role, StringComparison.OrdinalIgnoreCase))
            {
                var vlan = profile.VlanId(required.vlanName);
                if (vlan > 0) return vlan;
            }
        return profile.ManagementVlan;
    }

    private static int VlanFor(string address, TssNetworkRunProfile profile)
    {
        if (address.StartsWith(profile.ReceptionNetwork + ".", StringComparison.Ordinal)) return profile.ReceptionVlan;
        if (address.StartsWith(profile.DataNetwork + ".", StringComparison.Ordinal)) return profile.DataVlan;
        return profile.ManagementVlan;
    }
    private static string GatewayForVlan(int vlan, TssNetworkRunProfile profile)
    {
        foreach (var requirement in profile.RouterSubinterfaceRequirements)
            if (requirement != null && profile.VlanId(requirement.vlanName) == vlan)
                return profile.NetworkAddress(requirement.networkName, requirement.addressSuffix);
        return string.Empty;
    }

    private static string RouterMaskForVlan(int vlan, TssNetworkRunProfile profile)
    {
        foreach (var requirement in profile.RouterSubinterfaceRequirements)
            if (requirement != null && profile.VlanId(requirement.vlanName) == vlan && TssNetworkAddress.IsMask(requirement.mask))
                return requirement.mask;
        return string.Empty;
    }

    private EndpointNetworkState FindEndpointByAddress(string address)
    {
        foreach (var endpoint in _state.Endpoints)
            if (string.Equals(endpoint.Address, address, StringComparison.OrdinalIgnoreCase)) return endpoint;
        return null;
    }

    private TssReachabilityResult? ValidateKnownEndpointDestination(string address, string role, string sourceRole)
    {
        var profile = _state.Profile;
        if (profile == null) return null;
        foreach (var known in profile.EndpointAddresses)
        {
            if (!string.Equals(known.Value, address, StringComparison.OrdinalIgnoreCase))
                continue;

            var endpoint = FindEndpointByAddress(address);
            if (endpoint == null || !endpoint.HasValidAddress)
                return Fail(3, TssReachabilityFaultCategory.DestinationUnavailable,
                    "destination endpoint is not installed or configured.", sourceRole, role);
            break;
        }
        return null;
    }
    private NetworkDeviceState FindDeviceInterfaceByAddress(string address, out NetworkInterfaceState matched)
    {
        matched = null;
        foreach (var device in _state.Devices)
        {
            matched = device.Svis.Values.FirstOrDefault(iface => string.Equals(iface.Address, address, StringComparison.OrdinalIgnoreCase));
            if (matched != null) return device;
            matched = device.Interfaces.Values.FirstOrDefault(iface => string.Equals(iface.Address, address, StringComparison.OrdinalIgnoreCase));
            if (matched != null) return device;
        }
        return null;
    }
    private static string ResolveRole(string address, TssNetworkRunProfile profile)
    {
        if (profile == null) return string.Empty;
        if (address == profile.WanProviderAddress) return "Provider";
        foreach (var pair in profile.EndpointAddresses) if (pair.Value == address) return pair.Key;
        foreach (var requirement in profile.RouterSubinterfaceRequirements)
            if (requirement != null && string.Equals(address, profile.NetworkAddress(requirement.networkName, requirement.addressSuffix), StringComparison.OrdinalIgnoreCase))
                return requirement.networkName + " gateway";
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
