using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using UnityEngine;

public sealed class TssNetworkRunProfile
{
    public readonly int Seed;
    public readonly string StudentId;
    public readonly int ManagementVlan;
    public readonly int ReceptionVlan;
    public readonly int DataVlan;
    public readonly int NativeVlan;
    public readonly int DeadVlan;
    public readonly string ManagementNetwork;
    public readonly string ReceptionNetwork;
    public readonly string DataNetwork;
    public readonly string WanCustomerAddress;
    public readonly string WanProviderAddress;
    public readonly string WanNetwork;
    public readonly string CircuitId;
    public readonly string EnableSecret;
    public readonly string ConsolePassword;
    public readonly string VtyPassword;
    public readonly string Motd;
    public readonly string Ssid;
    public readonly string WifiPassphrase;
    public readonly string ApAdminPassword;
    public readonly int WirelessChannel;
    public readonly string ApManagementAddress;
    public readonly string TestLaptopAddress;
    public readonly Dictionary<string, string> EndpointAddresses = new(StringComparer.OrdinalIgnoreCase);
    public readonly Dictionary<string, string> EndpointAliases = new(StringComparer.OrdinalIgnoreCase);
    public readonly NetworkRoleBinding[] RoleBindings;
    public readonly NetworkRequiredPort[] RequiredPorts;
    public readonly NetworkEvidenceRequirement[] EvidenceRequirements;
    public readonly string ManagementVlanName;
    public readonly string ReceptionVlanName;
    public readonly string DataVlanName;
    public readonly string NativeVlanName;
    public readonly string DeadVlanName;
    public readonly HashSet<string> DhcpNetworkNames = new(StringComparer.OrdinalIgnoreCase);

    private TssNetworkRunProfile(int seed, NetworkScenarioConfig config, string studentId)
    {
        Seed = seed;
        StudentId = studentId ?? string.Empty;
        var random = new System.Random(seed);
        var minVlan = config != null ? Math.Max(10, config.vlanMinimum) : 10;
        var maxVlan = config != null ? Math.Min(390, config.vlanMaximum) : 390;
        var vlans = UniqueTens(random, 4, minVlan, maxVlan);
        ManagementVlan = vlans[0]; ReceptionVlan = vlans[1]; DataVlan = vlans[2]; NativeVlan = vlans[3]; DeadVlan = 888;
        var nets = Unique(random, 3, 10, 240);
        ManagementNetwork = $"192.168.{nets[0]}";
        ReceptionNetwork = $"192.168.{nets[1]}";
        DataNetwork = $"192.168.{nets[2]}";
        WanNetwork = $"10.{random.Next(10, 240)}.{random.Next(10, 240)}";
        WanCustomerAddress = WanNetwork + ".1";
        WanProviderAddress = WanNetwork + ".2";
        CircuitId = config?.circuitId ?? string.Empty;
        EnableSecret = config?.enableSecret ?? string.Empty;
        ConsolePassword = config?.consolePassword ?? string.Empty;
        VtyPassword = config?.vtyPassword ?? string.Empty;
        Motd = config?.motd ?? string.Empty;
        Ssid = config?.ssid ?? string.Empty;
        WifiPassphrase = config?.wifiPassphrase ?? string.Empty;
        ApAdminPassword = config?.apAdminPassword ?? string.Empty;
        WirelessChannel = config?.wirelessChannel ?? 0;
        ManagementVlanName = config?.managementVlanName ?? "Management";
        ReceptionVlanName = config?.receptionVlanName ?? "Reception";
        DataVlanName = config?.dataVlanName ?? "Data";
        NativeVlanName = config?.nativeVlanName ?? "Native";
        DeadVlanName = config?.deadVlanName ?? "Dead_Vlan";
        foreach (var name in config?.dhcpNetworkNames is { Length: > 0 } configured ? configured : new[] { ReceptionVlanName })
            if (!string.IsNullOrWhiteSpace(name)) DhcpNetworkNames.Add(name.Trim());
        RoleBindings = config?.roleBindings ?? Array.Empty<NetworkRoleBinding>();
        RequiredPorts = config?.requiredPorts is { Length: > 0 } ? config.requiredPorts : DefaultPorts();
        EvidenceRequirements = config?.evidenceRequirements is { Length: > 0 } ? config.evidenceRequirements : DefaultEvidence();
        Add("IT1", ManagementNetwork, 11); Add("IT2", ManagementNetwork, 12);
        Add("Reception1", ReceptionNetwork, 21); Add("Reception2", ReceptionNetwork, 22);
        Add("Data1", DataNetwork, 31); Add("Data2", DataNetwork, 32); Add("Data3", DataNetwork, 33);
        ApManagementAddress = ReceptionNetwork + ".50";
        TestLaptopAddress = ReceptionNetwork + ".60";
        EndpointAddresses["AP-1"] = ApManagementAddress;
        EndpointAddresses["TestLaptop"] = TestLaptopAddress;
        if (config?.endpointAliases != null)
            foreach (var alias in config.endpointAliases)
                if (!string.IsNullOrWhiteSpace(alias)) EndpointAliases[alias] = alias;
    }
    public static TssNetworkRunProfile Create(int seed, NetworkScenarioConfig config) => new(seed, config, string.Empty);
    public static TssNetworkRunProfile Create(int seed, NetworkScenarioConfig config, string studentId) => new(seed, config, studentId);
    public static int DeriveSeed(string studentId, string scenarioId)
    {
        uint hash = 2166136261u;
        foreach (var c in $"{studentId?.Trim().ToUpperInvariant()}:{scenarioId?.Trim().ToUpperInvariant()}")
        {
            hash ^= c;
            hash *= 16777619u;
        }

        return (int)(hash % 2147483646u) + 1;
    }
    private static NetworkRequiredPort[] DefaultPorts() => new[]
    {
        new NetworkRequiredPort { deviceRole = "AN-1", interfaceName = "Fa0/2", endpointId = "IT1" },
        new NetworkRequiredPort { deviceRole = "AN-1", interfaceName = "Fa0/3", endpointId = "Reception1" },
        new NetworkRequiredPort { deviceRole = "AN-1", interfaceName = "Fa0/4", endpointId = "Reception2" },
        new NetworkRequiredPort { deviceRole = "AN-1", interfaceName = "Fa0/5", endpointId = "IT2" },
        new NetworkRequiredPort { deviceRole = "AN-2", interfaceName = "Fa0/2", endpointId = "Data1" },
        new NetworkRequiredPort { deviceRole = "AN-2", interfaceName = "Fa0/3", endpointId = "Data2" },
        new NetworkRequiredPort { deviceRole = "AN-2", interfaceName = "Fa0/4", endpointId = "Data3" }
    };
    private NetworkEvidenceRequirement[] DefaultEvidence() => new[]
    {
        new NetworkEvidenceRequirement { evidenceId = "IT1-provider", command = "ping " + WanProviderAddress, description = "Endpoint-originated provider diagnostic" }
    };
    private void Add(string role, string network, int host) => EndpointAddresses[role] = network + "." + host;
    public bool IsDhcpNetwork(string address)
    {
        if (string.IsNullOrWhiteSpace(address)) return false;
        var networkName = address.StartsWith(ManagementNetwork + ".", StringComparison.Ordinal) ? ManagementVlanName
            : address.StartsWith(ReceptionNetwork + ".", StringComparison.Ordinal) ? ReceptionVlanName
            : address.StartsWith(DataNetwork + ".", StringComparison.Ordinal) ? DataVlanName : string.Empty;
        return DhcpNetworkNames.Contains(networkName);
    }
    private static int[] Unique(System.Random random, int count, int min, int max)
    {
        var values = new HashSet<int>(); while (values.Count < count) values.Add(random.Next(min, max)); return values.ToArray();
    }
    private static int[] UniqueTens(System.Random random, int count, int min, int max)
    {
        min = Math.Max(10, (min / 10) * 10);
        max = Math.Min(390, (max / 10) * 10);
        if (max - min < (count - 1) * 10) { min = 10; max = 390; }
        var values = new HashSet<int>();
        while (values.Count < count) values.Add(random.Next(min / 10, max / 10 + 1) * 10);
        return values.ToArray();
    }
    public static string MacFor(int seed, string key)
    {
        uint hash = 2166136261u;
        foreach (var c in $"{seed}:{key}") { hash ^= c; hash *= 16777619u; }
        return $"02:{(hash >> 24) & 0xFF:X2}:{(hash >> 16) & 0xFF:X2}:{(hash >> 8) & 0xFF:X2}:{hash & 0xFF:X2}:01";
    }
}

public enum TssNetworkCliMode { UserExec, PrivilegedExec, GlobalConfig, InterfaceConfig, LineConfig, VlanConfig }


// Runtime network session state.\n
public sealed class TssNetworkSessionState
{
    private readonly Dictionary<string, NetworkDeviceState> _devices = new(StringComparer.OrdinalIgnoreCase);
    private readonly Dictionary<string, EndpointNetworkState> _endpoints = new(StringComparer.OrdinalIgnoreCase);
    private readonly Dictionary<string, TssNetworkSessionSnapshot> _checkpoints = new(StringComparer.OrdinalIgnoreCase);
    private readonly Dictionary<string, TssNetworkCompletionSnapshot> _completionSnapshots = new(StringComparer.OrdinalIgnoreCase);
    private int _completionOrder;
    public readonly HashSet<string> Evidence = new(StringComparer.OrdinalIgnoreCase);
    public TssNetworkRunProfile Profile { get; }
    public TssWirelessAccessPointState AccessPoint { get; } = new();
    public bool ProviderDemarcConnected { get; private set; }
    public IEnumerable<NetworkDeviceState> Devices => _devices.Values;
    public IEnumerable<EndpointNetworkState> Endpoints => _endpoints.Values;
    public IReadOnlyDictionary<string, TssNetworkSessionSnapshot> Checkpoints => _checkpoints;
    public IReadOnlyDictionary<string, TssNetworkCompletionSnapshot> CompletionSnapshots => _completionSnapshots;
    public TssNetworkSessionState(TssNetworkRunProfile profile) { Profile = profile; }
    public NetworkDeviceState GetOrCreateDevice(string key, EquipmentCategory category, string displayName)
    {
        if (_devices.TryGetValue(key, out var state)) return state;
        state = new NetworkDeviceState(key, category, displayName, Profile);
        foreach (var binding in Profile?.RoleBindings ?? Array.Empty<NetworkRoleBinding>())
            if (binding != null && string.Equals(binding.deviceKey, key, StringComparison.OrdinalIgnoreCase))
            { state.ExpectedRole = binding.role; break; }
        if (string.IsNullOrWhiteSpace(state.ExpectedRole)) state.ExpectedRole = displayName;
        _devices.Add(key, state); return state;
    }
    public bool TryGetDevice(string key, out NetworkDeviceState state) => _devices.TryGetValue(key, out state);
    public bool TryGetEndpoint(string key, out EndpointNetworkState state) => _endpoints.TryGetValue(key, out state);
    public bool IsEndpointLayer1Available(string endpointId)
    {
        if (string.IsNullOrWhiteSpace(endpointId)) return false;
        foreach (var device in _devices.Values)
            foreach (var iface in device.Interfaces.Values)
                if (string.Equals(iface.EndpointId, endpointId, StringComparison.OrdinalIgnoreCase))
                    return iface.AdminUp && iface.CarrierUp;
        return !endpointId.StartsWith("Rack:", StringComparison.OrdinalIgnoreCase);
    }

    public bool IsEndpointLogicalLayer1Available(TssTrainingSession session, string endpointId)
    {
        if (!TryGetLogicalInterface(session, endpointId, out _, out var iface))
            return session && session.IsEndpointOnEffectivePhysicalPath(endpointId);

        return iface.AdminUp && iface.CarrierUp;
    }

    public bool IsEndpointLogicalLayer2Available(TssTrainingSession session, string endpointId)
    {
        if (!TryGetLogicalInterface(session, endpointId, out var device, out var iface))
            return session && session.IsEndpointOnEffectivePhysicalPath(endpointId);

        if (!iface.AdminUp || !iface.CarrierUp)
            return false;

        if (device.Category != EquipmentCategory.Switch)
            return true;
        return iface.IsTrunk
            ? iface.AllowedVlans != null && iface.AllowedVlans.Any(vlan => IsActiveVlan(device, vlan))
            : IsActiveVlan(device, iface.AccessVlan);
    }
    public bool IsEndpointLayer2Available(string endpointId)
    {
        if (!TryGetInterfaceByEndpoint(endpointId, out var device, out var iface))
            return true;

        if (!iface.AdminUp || !iface.CarrierUp)
            return false;

        if (device.Category != EquipmentCategory.Switch)
            return true;

        if (iface.IsTrunk)
            return iface.AllowedVlans != null && iface.AllowedVlans.Any(vlan => IsActiveVlan(device, vlan));

        return IsActiveVlan(device, iface.AccessVlan);
    }

    public bool IsEndpointIpRoutingAvailable(string endpointId)
    {
        if (!TryGetInterfaceByEndpoint(endpointId, out var device, out var iface))
            return false;

        if (device.Category == EquipmentCategory.Router && string.Equals(iface.Name, "Fa0/0", StringComparison.OrdinalIgnoreCase))
            return device.Interfaces.Values.Any(candidate => candidate.Name.StartsWith(iface.Name + ".", StringComparison.OrdinalIgnoreCase)
                && IsInterfaceIpRoutingAvailable(device, candidate));

        return IsInterfaceIpRoutingAvailable(device, iface);
    }

    public bool IsInterfaceIpRoutingAvailable(NetworkDeviceState device, NetworkInterfaceState iface)
    {
        if (device == null || iface == null || !iface.AdminUp || !iface.CarrierUp
            || !TssNetworkAddress.IsAddress(iface.Address) || !TssNetworkAddress.IsMask(iface.Mask))
            return false;

        if (device.Category == EquipmentCategory.Router && iface.Name.Contains('.'))
        {
            var separator = iface.Name.IndexOf('.');
            return iface.Dot1qVlan > 0
                && device.Interfaces.TryGetValue(iface.Name.Substring(0, separator), out var parent)
                && parent.AdminUp && parent.CarrierUp;
        }

        return true;
    }

    public bool IsEndpointLayer3Operational(string endpointId)
    {
        if (!TryGetInterfaceByEndpoint(endpointId, out var device, out var iface)
            || !IsEndpointIpRoutingAvailable(endpointId))
            return false;

        if (device.Category == EquipmentCategory.Router
            && string.Equals(iface.Name, "Fa0/1", StringComparison.OrdinalIgnoreCase)
            && Profile != null)
            return string.Equals(iface.Address, Profile.WanCustomerAddress, StringComparison.OrdinalIgnoreCase)
                && string.Equals(iface.Mask, "255.255.255.252", StringComparison.OrdinalIgnoreCase);

        if (device.Category == EquipmentCategory.Router && string.Equals(iface.Name, "Fa0/0", StringComparison.OrdinalIgnoreCase))
            return device.Interfaces.Values.Any(candidate => candidate.Name.StartsWith(iface.Name + ".", StringComparison.OrdinalIgnoreCase)
                && IsInterfaceIpRoutingAvailable(device, candidate));

        return true;
    }

    private bool TryGetInterfaceByEndpoint(string endpointId, out NetworkDeviceState device, out NetworkInterfaceState iface)
    {
        device = null;
        iface = null;
        if (string.IsNullOrWhiteSpace(endpointId))
            return false;

        foreach (var candidate in _devices.Values)
            foreach (var candidateInterface in candidate.Interfaces.Values)
                if (string.Equals(candidateInterface.EndpointId, endpointId, StringComparison.OrdinalIgnoreCase))
                {
                    device = candidate;
                    iface = candidateInterface;
                    return true;
                }

        return false;
    }

    private bool TryGetLogicalInterface(TssTrainingSession session, string endpointId, out NetworkDeviceState device, out NetworkInterfaceState iface)
    {
        device = null;
        iface = null;
        if (string.IsNullOrWhiteSpace(endpointId))
            return false;

        foreach (var candidate in _devices.Values)
            foreach (var candidateInterface in candidate.Interfaces.Values)
                if (string.Equals(candidateInterface.EndpointId, endpointId, StringComparison.OrdinalIgnoreCase)
                    || (session && session.HasPhysicalPath(endpointId, candidateInterface.EndpointId)))
                {
                    device = candidate;
                    iface = candidateInterface;
                    return true;
                }

        return false;
    }

    private static bool IsActiveVlan(NetworkDeviceState device, int vlan)
    {
        return device != null
            && device.Vlans.TryGetValue(vlan, out var name)
            && (name ?? string.Empty).IndexOf("suspend", StringComparison.OrdinalIgnoreCase) < 0;
    }
    public EndpointNetworkState GetOrCreateEndpoint(string key, string role, EquipmentCategory category = EquipmentCategory.DesktopPc)
    {
        if (_endpoints.TryGetValue(key, out var state))
        {
            state.Category = category;
            return state;
        }

        state = new EndpointNetworkState(key, role, Profile) { Category = category };
        _endpoints.Add(key, state);
        return state;
    }

    public void SetAccessPointUplink(bool connected)
    {
        AccessPoint.UplinkConnected = connected;
        var endpoint = GetOrCreateEndpoint("AP-1", "AP-1", EquipmentCategory.AccessPoint);
        endpoint.Layer1Connected = connected;
        endpoint.Layer2Connected = connected;
    }

    public void SetProviderDemarcConnected(bool connected) => ProviderDemarcConnected = connected;

    public bool IsAccessPointSwitchPortReady()
    {
        var accessSwitch = FindDeviceByRole("AN-1");
        return accessSwitch != null
            && accessSwitch.Interfaces.TryGetValue("Fa0/7", out var port)
            && port.AdminUp && port.CarrierUp && !port.IsTrunk
            && port.AccessVlan == Profile.ReceptionVlan;
    }

    public bool IsWirelessAssociated(string endpointKey)
    {
        return !string.IsNullOrWhiteSpace(endpointKey)
            && string.Equals(AccessPoint.AssociatedEndpointKey, endpointKey, StringComparison.OrdinalIgnoreCase)
            && AccessPoint.Configured;
    }

    public bool IsProviderDhcpAvailable()
    {
        if (!ProviderDemarcConnected)
            return false;

        var router = FindDeviceByRole("CN-1");
        if (router == null || !router.Interfaces.TryGetValue("Fa0/1", out var wan)
            || !wan.AdminUp || !wan.CarrierUp
            || wan.Address != Profile.WanCustomerAddress || wan.Mask != "255.255.255.252")
            return false;

        foreach (var route in router.Routes)
            if (route.Network == "0.0.0.0" && route.Mask == "0.0.0.0" && route.NextHop == Profile.WanProviderAddress)
                return true;
        return false;
    }

    public bool TryConfigureAccessPoint(string sourceKey, string ssid, string adminPassword, string passphrase, string channel, bool wpsDisabled, out string error)
    {
        error = string.Empty;
        if (!AccessPoint.UplinkConnected)
        {
            error = "Layer 1: connect the AP uplink through the structured path first.";
            return false;
        }
        var reachability = new TssNetworkReachabilityEvaluator(this).Evaluate(sourceKey, Profile.ApManagementAddress);
        if (!reachability.Success)
        {
            error = reachability.Diagnostic;
            return false;
        }
        if (!string.Equals((ssid ?? string.Empty).Trim(), Profile.Ssid, StringComparison.Ordinal)
            || !string.Equals((adminPassword ?? string.Empty).Trim(), Profile.ApAdminPassword, StringComparison.Ordinal)
            || !string.Equals((passphrase ?? string.Empty).Trim(), Profile.WifiPassphrase, StringComparison.Ordinal)
            || !int.TryParse(channel, out var parsedChannel) || parsedChannel != Profile.WirelessChannel
            || !wpsDisabled)
        {
            error = "AP settings do not match the wireless SOP.";
            return false;
        }

        AccessPoint.Configured = true;
        AccessPoint.Ssid = Profile.Ssid;
        return true;
    }

    public bool TryAssociateWirelessClient(string sourceKey, string ssid, string passphrase, out string error)
    {
        error = string.Empty;
        if (!TryGetEndpoint(sourceKey, out var endpoint) || endpoint.Category != EquipmentCategory.Laptop)
        {
            error = "Wireless client is unavailable.";
            return false;
        }

        if (!AccessPoint.Configured)
        {
            error = "Configure the secured guest AP before joining Wi-Fi.";
            return false;
        }

        if (!string.Equals((ssid ?? string.Empty).Trim(), Profile.Ssid, StringComparison.Ordinal)
            || !string.Equals((passphrase ?? string.Empty).Trim(), Profile.WifiPassphrase, StringComparison.Ordinal))
        {
            error = "SSID or passphrase does not match the wireless SOP.";
            return false;
        }

        AccessPoint.AssociatedEndpointKey = sourceKey;
        AccessPoint.AssociatedSsid = Profile.Ssid;
        endpoint.Layer1Connected = true;
        endpoint.Layer2Connected = true;
        endpoint.AttemptedEvidence.Add("wifi-association");
        endpoint.Evidence.Add("wifi-association");
        return true;
    }

    public string ExpectedAddress(EndpointNetworkState endpoint)
    {
        if (endpoint == null || Profile == null)
            return string.Empty;
        if (endpoint.Category == EquipmentCategory.Laptop)
            return Profile.TestLaptopAddress;
        if (endpoint.Category == EquipmentCategory.Server)
            return Profile.EndpointAddresses.TryGetValue("IT2", out var serverAddress) ? serverAddress : string.Empty;
        var role = endpoint.Role;
        if (Profile.EndpointAliases.TryGetValue(role, out var mappedRole))
            role = mappedRole;
        return Profile.EndpointAddresses.TryGetValue(role, out var address) ? address : string.Empty;
    }

    private NetworkDeviceState FindDeviceByRole(string role)
    {
        foreach (var device in _devices.Values)
            if (string.Equals(device.ExpectedRole, role, StringComparison.OrdinalIgnoreCase)
                || string.Equals(device.Hostname, role, StringComparison.OrdinalIgnoreCase))
                return device;
        return null;
    }
    public void RemoveDevice(string key) { if (!string.IsNullOrWhiteSpace(key)) _devices.Remove(key); }
    public void RemoveEndpoint(string key) { if (!string.IsNullOrWhiteSpace(key)) _endpoints.Remove(key); }
    public void Clear() { _devices.Clear(); _endpoints.Clear(); Evidence.Clear(); _checkpoints.Clear(); _completionSnapshots.Clear(); _completionOrder = 0; }
    public bool TryPrintTestPage(string printerKey, string sourceKey, out TssReachabilityResult result)
    {
        if (!TryGetEndpoint(printerKey, out var printer))
        {
            result = new TssReachabilityResult(false, 1, TssReachabilityFaultCategory.PhysicalPathUnavailable, "Layer 1: destination unavailable.", string.Empty, string.Empty, Array.Empty<string>());
            return false;
        }

        printer.PrintAttemptCount++;
        printer.LastPrintSourceKey = sourceKey;
        if (string.IsNullOrWhiteSpace(sourceKey) || sourceKey.Equals(printerKey, StringComparison.OrdinalIgnoreCase) || !printer.Layer1Connected || !printer.Layer2Connected)
            result = new TssReachabilityResult(false, 1, TssReachabilityFaultCategory.PhysicalPathUnavailable, "Layer 1: destination unavailable.", sourceKey ?? string.Empty, printer.Role, Array.Empty<string>());
        else
            result = new TssNetworkReachabilityEvaluator(this).Evaluate(sourceKey, printer.Address);

        printer.LastPrintSucceeded = result.Success;
        printer.LastPrintDiagnostic = result.Success ? "Print job completed." : result.Diagnostic;
        return result.Success;
    }

    public bool TryLoadRedWolfPage(string sourceKey, out string error)
    {
        error = string.Empty;
        if (!TryGetEndpoint(sourceKey, out var endpoint) || endpoint.Category != EquipmentCategory.Laptop)
        {
            error = "The test laptop is unavailable.";
            return false;
        }

        if (!IsWirelessAssociated(sourceKey) || !endpoint.UseDhcp || endpoint.Address != Profile.TestLaptopAddress)
        {
            error = "Join the secured guest Wi-Fi and obtain the DHCP lease first.";
            return false;
        }

        var result = new TssNetworkReachabilityEvaluator(this).Evaluate(sourceKey, Profile.WanProviderAddress);
        if (!result.Success)
        {
            error = result.Diagnostic;
            return false;
        }

        endpoint.AttemptedEvidence.Add("redwolf");
        endpoint.Evidence.Add("redwolf");
        return true;
    }
    public void SaveCheckpoint(string name)
    {
        if (string.IsNullOrWhiteSpace(name)) return;
        _checkpoints[name] = new TssNetworkSessionSnapshot(this);
    }

    public TssNetworkCompletionSnapshot SaveCompletionSnapshot(string workPackage)
    {
        if (string.IsNullOrWhiteSpace(workPackage)) return null;
        if (_completionSnapshots.TryGetValue(workPackage, out var existing)) return existing;
        var snapshot = new TssNetworkCompletionSnapshot(workPackage, Profile?.Seed ?? 0, ++_completionOrder, this);
        _completionSnapshots[workPackage] = snapshot;
        return snapshot;
    }
}

public sealed class TssNetworkCompletionSnapshot
{
    public readonly string WorkPackage;
    public readonly int ProfileSeed;
    public readonly int CompletionOrder;
    public readonly TssNetworkSessionSnapshot State;
    public TssNetworkCompletionSnapshot(string workPackage, int profileSeed, int completionOrder, TssNetworkSessionState state)
    {
        WorkPackage = workPackage; ProfileSeed = profileSeed; CompletionOrder = completionOrder; State = new TssNetworkSessionSnapshot(state);
    }
}

public sealed class TssNetworkSessionSnapshot
{
    public readonly Dictionary<string, NetworkDeviceState> Devices = new(StringComparer.OrdinalIgnoreCase);
    public readonly Dictionary<string, EndpointNetworkState> Endpoints = new(StringComparer.OrdinalIgnoreCase);
    public readonly HashSet<string> Evidence = new(StringComparer.OrdinalIgnoreCase);
    public readonly TssWirelessAccessPointState AccessPoint = new();
    public bool ProviderDemarcConnected;
    public TssNetworkSessionSnapshot(TssNetworkSessionState state)
    {
        foreach (var device in state.Devices) Devices[device.Key] = device.Clone();
        foreach (var endpoint in state.Endpoints) Endpoints[endpoint.Key] = endpoint.Clone();
        foreach (var item in state.Evidence) Evidence.Add(item);
        AccessPoint.UplinkConnected = state.AccessPoint.UplinkConnected;
        AccessPoint.Configured = state.AccessPoint.Configured;
        AccessPoint.Ssid = state.AccessPoint.Ssid;
        AccessPoint.AssociatedEndpointKey = state.AccessPoint.AssociatedEndpointKey;
        AccessPoint.AssociatedSsid = state.AccessPoint.AssociatedSsid;
        ProviderDemarcConnected = state.ProviderDemarcConnected;
    }
}

public sealed class NetworkDeviceState
{
    public readonly string Key;
    public readonly EquipmentCategory Category;
    public string Hostname;
    public string EnablePassword;
    public string EnableSecret;
    public string ConsolePassword;
    public string VtyPassword;
    public string Motd;
    public string MacAddress;
    public string ManagementAddress;
    public string ManagementMask = "255.255.255.0";
    public string DefaultGateway;
    public string DnsAddress = "8.8.8.8";
    public bool UseDhcp;
    public bool PasswordEncryption;
    public bool DomainLookupDisabled;
    public bool ConsoleLoginRequired;
    public bool VtyLoginRequired;
    public readonly Dictionary<int, string> Vlans = new();
    public readonly Dictionary<string, NetworkInterfaceState> Interfaces = new(StringComparer.OrdinalIgnoreCase);
    public readonly List<NetworkRouteState> Routes = new();
    public readonly HashSet<string> Evidence = new(StringComparer.OrdinalIgnoreCase);
    public NetworkDeviceState StartupSnapshot;
    public TssNetworkCliMode Mode;
    public bool AwaitingEnablePassword;
    public List<string> SelectedInterfaces = new();
    public int SelectedVlan;
    public string SelectedLine;
    public string ExpectedRole;
    public NetworkDeviceState(string key, EquipmentCategory category, string name, TssNetworkRunProfile profile)
    {
        Key = key; Category = category; Hostname = string.IsNullOrWhiteSpace(name) ? category.ToString() : name.Trim();
        MacAddress = profile != null ? TssNetworkRunProfile.MacFor(profile.Seed, key) : string.Empty;
        ManagementAddress = category == EquipmentCategory.Router || profile == null
            ? string.Empty
            : profile.ManagementNetwork + ".2";
        DefaultGateway = category == EquipmentCategory.Router || profile == null
            ? string.Empty
            : profile.ManagementNetwork + ".1";
        if (category == EquipmentCategory.Switch) Vlans[1] = "default";
    }
    public NetworkDeviceState Clone()
    {
        var copy = new NetworkDeviceState(Key, Category, Hostname, null)
        {
            EnablePassword = EnablePassword, EnableSecret = EnableSecret, ConsolePassword = ConsolePassword, VtyPassword = VtyPassword, Motd = Motd,
            MacAddress = MacAddress, ManagementAddress = ManagementAddress, ManagementMask = ManagementMask,
            DefaultGateway = DefaultGateway, DnsAddress = DnsAddress, UseDhcp = UseDhcp,
            PasswordEncryption = PasswordEncryption, DomainLookupDisabled = DomainLookupDisabled,
            ConsoleLoginRequired = ConsoleLoginRequired, VtyLoginRequired = VtyLoginRequired, Mode = Mode,
            AwaitingEnablePassword = AwaitingEnablePassword,
            SelectedVlan = SelectedVlan, SelectedLine = SelectedLine, SelectedInterfaces = new List<string>(SelectedInterfaces), ExpectedRole = ExpectedRole
        };
        foreach (var vlan in Vlans) copy.Vlans[vlan.Key] = vlan.Value;
        foreach (var pair in Interfaces) copy.Interfaces[pair.Key] = pair.Value.Clone();
        foreach (var route in Routes) copy.Routes.Add(new NetworkRouteState { Network = route.Network, Mask = route.Mask, NextHop = route.NextHop });
        foreach (var item in Evidence) copy.Evidence.Add(item);
        copy.StartupSnapshot = StartupSnapshot?.Clone();
        return copy;
    }
}

public sealed class NetworkInterfaceState
{
    public string Name;
    public string EndpointId;
    public bool AdminUp;
    public bool CarrierUp;
    public bool IsTrunk;
    public bool SwitchportModeConfigured;
    public bool SwitchportAccessVlanConfigured;
    public bool TrunkEncapsulationDot1q;
    public int AccessVlan = 1;
    public HashSet<int> AllowedVlans = new();
    public int NativeVlan = 1;
    public string Address;
    public string Mask;
    public int Dot1qVlan;
    public NetworkInterfaceState Clone() { var copy = (NetworkInterfaceState)MemberwiseClone(); copy.AllowedVlans = new HashSet<int>(AllowedVlans); return copy; }
}

public sealed class NetworkRouteState { public string Network; public string Mask; public string NextHop; }

public sealed class TssWirelessAccessPointState
{
    public bool UplinkConnected;
    public bool Configured;
    public string Ssid;
    public string AssociatedEndpointKey;
    public string AssociatedSsid;
}

public sealed class EndpointNetworkState
{
    public readonly string Key;
    public readonly string Role;
    public string MacAddress;
    public bool UseDhcp;
    public string Address;
    public string Mask = string.Empty;
    public string Gateway;
    public string Dns = string.Empty;
    public bool Layer1Connected;
    public bool Layer2Connected;
    public string DeviceKey;
    public string InterfaceName;
    public EquipmentCategory Category;
    public readonly HashSet<string> Evidence = new(StringComparer.OrdinalIgnoreCase);
    public int PrintAttemptCount;
    public bool LastPrintSucceeded;
    public string LastPrintSourceKey;
    public string LastPrintDiagnostic;
    public readonly HashSet<string> AttemptedEvidence = new(StringComparer.OrdinalIgnoreCase);

    public EndpointNetworkState(string key, string role, TssNetworkRunProfile profile)
    {
        Key = key;
        Role = role ?? string.Empty;
        MacAddress = TssNetworkRunProfile.MacFor(profile?.Seed ?? 0, key);
        Address = string.Empty;
        Gateway = string.Empty;
    }

    public EndpointNetworkState Clone()
    {
        var copy = new EndpointNetworkState(Key, Role, null)
        {
            UseDhcp = UseDhcp, Address = Address, Mask = Mask, Gateway = Gateway, Dns = Dns,
            Layer1Connected = Layer1Connected, Layer2Connected = Layer2Connected, MacAddress = MacAddress
            , DeviceKey = DeviceKey, InterfaceName = InterfaceName, PrintAttemptCount = PrintAttemptCount,
            LastPrintSucceeded = LastPrintSucceeded, LastPrintSourceKey = LastPrintSourceKey,
            LastPrintDiagnostic = LastPrintDiagnostic, Category = Category
        };
        foreach (var item in Evidence) copy.Evidence.Add(item);
        foreach (var item in AttemptedEvidence) copy.AttemptedEvidence.Add(item);
        return copy;
    }

    public bool HasValidAddress => TssNetworkAddress.IsAddress(Address) && TssNetworkAddress.IsMask(Mask);
    public static string GatewayFor(string address, TssNetworkRunProfile profile)
    {
        if (profile == null || string.IsNullOrWhiteSpace(address)) return string.Empty;
        if (address.StartsWith(profile.ManagementNetwork + ".", StringComparison.Ordinal)) return profile.ManagementNetwork + ".1";
        if (address.StartsWith(profile.ReceptionNetwork + ".", StringComparison.Ordinal)) return profile.ReceptionNetwork + ".1";
        if (address.StartsWith(profile.DataNetwork + ".", StringComparison.Ordinal)) return profile.DataNetwork + ".1";
        return string.Empty;
    }

}

public static class TssNetworkAddress
{
    public static bool IsAddress(string value)
    {
        if (string.IsNullOrWhiteSpace(value)) return false;
        var parts = value.Split('.');
        if (parts.Length != 4) return false;
        foreach (var part in parts) if (!int.TryParse(part, out var octet) || octet is < 0 or > 255) return false;
        return true;
    }

    public static bool IsMask(string value)
    {
        if (!IsAddress(value)) return false;
        uint mask = 0;
        foreach (var part in value.Split('.')) mask = (mask << 8) | byte.Parse(part, CultureInfo.InvariantCulture);
        if (mask == 0) return false;
        var inverted = ~mask;
        return (inverted & (inverted + 1)) == 0;
    }

    public static int PrefixLength(string value)
    {
        if (!IsMask(value)) return 0;
        var prefix = 0;
        foreach (var part in value.Split('.'))
        {
            var octet = byte.Parse(part, CultureInfo.InvariantCulture);
            while (octet != 0) { prefix += octet & 1; octet >>= 1; }
        }
        return prefix;
    }

    public static bool IsInSameSubnet(string address, string mask, string candidate)
    {
        if (!IsAddress(address) || !IsMask(mask) || !IsAddress(candidate)) return false;
        return (ToUInt32(address) & ToUInt32(mask)) == (ToUInt32(candidate) & ToUInt32(mask));
    }

    private static uint ToUInt32(string value)
    {
        uint result = 0;
        foreach (var part in value.Split('.')) result = (result << 8) | byte.Parse(part, CultureInfo.InvariantCulture);
        return result;
    }
}
