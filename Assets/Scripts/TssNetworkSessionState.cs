using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using UnityEngine;

public sealed class TssNetworkRunProfile
{
    public readonly int Seed;
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
    public readonly string EnableSecret;
    public readonly string ConsolePassword;
    public readonly string VtyPassword;
    public readonly string Motd;
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

    private TssNetworkRunProfile(int seed, NetworkScenarioConfig config)
    {
        Seed = seed;
        var random = new System.Random(seed);
        var minVlan = config != null ? Math.Min(4090, Math.Max(1, config.vlanMinimum)) : 10;
        var maxVlan = config != null ? Math.Min(4095, Math.Max(minVlan + 5, config.vlanMaximum + 1)) : 400;
        var vlans = Unique(random, 5, minVlan, maxVlan);
        ManagementVlan = vlans[0]; ReceptionVlan = vlans[1]; DataVlan = vlans[2]; NativeVlan = vlans[3]; DeadVlan = vlans[4];
        var nets = Unique(random, 3, 20, 220);
        ManagementNetwork = $"10.{nets[0]}.10";
        ReceptionNetwork = $"10.{nets[1]}.20";
        DataNetwork = $"10.{nets[2]}.30";
        var wan = random.Next(20, 220);
        WanCustomerAddress = $"172.16.{wan}.1";
        WanProviderAddress = $"172.16.{wan}.2";
        EnableSecret = Token(random); ConsolePassword = Token(random); VtyPassword = Token(random); Motd = "AUTHORIZED TRAINING ACCESS ONLY";
        ManagementVlanName = config?.managementVlanName ?? "Management";
        ReceptionVlanName = config?.receptionVlanName ?? "Reception";
        DataVlanName = config?.dataVlanName ?? "Data";
        NativeVlanName = config?.nativeVlanName ?? "Native";
        DeadVlanName = config?.deadVlanName ?? "Dead_Vlan";
        RoleBindings = config?.roleBindings ?? Array.Empty<NetworkRoleBinding>();
        RequiredPorts = config?.requiredPorts is { Length: > 0 } ? config.requiredPorts : DefaultPorts();
        EvidenceRequirements = config?.evidenceRequirements is { Length: > 0 } ? config.evidenceRequirements : DefaultEvidence();
        Add("IT1", ManagementNetwork, 11); Add("IT2", ManagementNetwork, 12);
        Add("Reception1", ReceptionNetwork, 21); Add("Reception2", ReceptionNetwork, 22);
        Add("Data1", DataNetwork, 31); Add("Data2", DataNetwork, 32); Add("Data3", DataNetwork, 33);
        if (config?.endpointAliases != null)
            foreach (var alias in config.endpointAliases)
                if (!string.IsNullOrWhiteSpace(alias)) EndpointAliases[alias] = alias;
        if (config?.roleBindings != null)
            foreach (var binding in config.roleBindings)
                if (binding != null && !string.IsNullOrWhiteSpace(binding.role) && !string.IsNullOrWhiteSpace(binding.deviceKey))
                    EndpointAliases[binding.role] = binding.deviceKey;
    }
    public static TssNetworkRunProfile Create(int seed, NetworkScenarioConfig config) => new(seed, config);
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
    private static int[] Unique(System.Random random, int count, int min, int max)
    {
        var values = new HashSet<int>(); while (values.Count < count) values.Add(random.Next(min, max)); return values.ToArray();
    }
    private static string Token(System.Random random) => "T" + random.Next(100000, 999999).ToString(CultureInfo.InvariantCulture);
    public static string MacFor(int seed, string key)
    {
        uint hash = 2166136261u;
        foreach (var c in $"{seed}:{key}") { hash ^= c; hash *= 16777619u; }
        return $"02:{(hash >> 24) & 0xFF:X2}:{(hash >> 16) & 0xFF:X2}:{(hash >> 8) & 0xFF:X2}:{hash & 0xFF:X2}:01";
    }
}

public enum TssNetworkCliMode { UserExec, PrivilegedExec, GlobalConfig, InterfaceConfig, LineConfig, VlanConfig }

public sealed class TssNetworkSessionState
{
    private readonly Dictionary<string, NetworkDeviceState> _devices = new(StringComparer.OrdinalIgnoreCase);
    private readonly Dictionary<string, EndpointNetworkState> _endpoints = new(StringComparer.OrdinalIgnoreCase);
    private readonly Dictionary<string, TssNetworkSessionSnapshot> _checkpoints = new(StringComparer.OrdinalIgnoreCase);
    private readonly Dictionary<string, TssNetworkCompletionSnapshot> _completionSnapshots = new(StringComparer.OrdinalIgnoreCase);
    private int _completionOrder;
    public readonly HashSet<string> Evidence = new(StringComparer.OrdinalIgnoreCase);
    public TssNetworkRunProfile Profile { get; }
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
        return false;
    }
    public EndpointNetworkState GetOrCreateEndpoint(string key, string role)
    {
        if (_endpoints.TryGetValue(key, out var state))
            return state;

        state = new EndpointNetworkState(key, role, Profile);
        _endpoints.Add(key, state);
        return state;
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
    public TssNetworkSessionSnapshot(TssNetworkSessionState state)
    {
        foreach (var device in state.Devices) Devices[device.Key] = device.Clone();
        foreach (var endpoint in state.Endpoints) Endpoints[endpoint.Key] = endpoint.Clone();
        foreach (var item in state.Evidence) Evidence.Add(item);
    }
}

public sealed class NetworkDeviceState
{
    public readonly string Key;
    public readonly EquipmentCategory Category;
    public string Hostname;
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
    public readonly Dictionary<int, string> Vlans = new();
    public readonly Dictionary<string, NetworkInterfaceState> Interfaces = new(StringComparer.OrdinalIgnoreCase);
    public readonly List<NetworkRouteState> Routes = new();
    public readonly HashSet<string> Evidence = new(StringComparer.OrdinalIgnoreCase);
    public NetworkDeviceState StartupSnapshot;
    public TssNetworkCliMode Mode;
    public List<string> SelectedInterfaces = new();
    public int SelectedVlan;
    public string SelectedLine;
    public string ExpectedRole;
    public NetworkDeviceState(string key, EquipmentCategory category, string name, TssNetworkRunProfile profile)
    {
        Key = key; Category = category; Hostname = string.IsNullOrWhiteSpace(name) ? category.ToString() : name.Trim();
        MacAddress = profile != null ? TssNetworkRunProfile.MacFor(profile.Seed, key) : string.Empty;
        ManagementAddress = profile != null ? profile.ManagementNetwork + ".2" : string.Empty;
        DefaultGateway = profile != null ? profile.ManagementNetwork + ".1" : string.Empty;
        if (category == EquipmentCategory.Switch) Vlans[1] = "default";
    }
    public NetworkDeviceState Clone()
    {
        var copy = new NetworkDeviceState(Key, Category, Hostname, null)
        {
            EnableSecret = EnableSecret, ConsolePassword = ConsolePassword, VtyPassword = VtyPassword, Motd = Motd,
            MacAddress = MacAddress, ManagementAddress = ManagementAddress, ManagementMask = ManagementMask,
            DefaultGateway = DefaultGateway, DnsAddress = DnsAddress, UseDhcp = UseDhcp,
            PasswordEncryption = PasswordEncryption, DomainLookupDisabled = DomainLookupDisabled, Mode = Mode,
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
    public int AccessVlan = 1;
    public HashSet<int> AllowedVlans = new();
    public int NativeVlan = 1;
    public string Address;
    public string Mask;
    public int Dot1qVlan;
    public NetworkInterfaceState Clone() { var copy = (NetworkInterfaceState)MemberwiseClone(); copy.AllowedVlans = new HashSet<int>(AllowedVlans); return copy; }
}

public sealed class NetworkRouteState { public string Network; public string Mask; public string NextHop; }

public sealed class EndpointNetworkState
{
    public readonly string Key;
    public readonly string Role;
    public string MacAddress;
    public bool UseDhcp;
    public string Address;
    public string Mask = "255.255.255.0";
    public string Gateway;
    public string Dns = "8.8.8.8";
    public bool Layer1Connected;
    public bool Layer2Connected;
    public string DeviceKey;
    public string InterfaceName;
    public readonly HashSet<string> Evidence = new(StringComparer.OrdinalIgnoreCase);
    public int PrintAttemptCount;
    public bool LastPrintSucceeded;
    public string LastPrintSourceKey;
    public string LastPrintDiagnostic;

    public EndpointNetworkState(string key, string role, TssNetworkRunProfile profile)
    {
        Key = key;
        Role = role ?? string.Empty;
        MacAddress = TssNetworkRunProfile.MacFor(profile?.Seed ?? 0, key);
        var addressRole = Role;
        if (profile != null && profile.EndpointAliases.TryGetValue(Role, out var mappedRole))
            addressRole = mappedRole;
        Address = profile != null && profile.EndpointAddresses.TryGetValue(addressRole, out var address) ? address : string.Empty;
        Gateway = GatewayFor(Address, profile);
    }

    public EndpointNetworkState Clone()
    {
        var copy = new EndpointNetworkState(Key, Role, null)
        {
            UseDhcp = UseDhcp, Address = Address, Mask = Mask, Gateway = Gateway, Dns = Dns,
            Layer1Connected = Layer1Connected, Layer2Connected = Layer2Connected, MacAddress = MacAddress
            , DeviceKey = DeviceKey, InterfaceName = InterfaceName, PrintAttemptCount = PrintAttemptCount,
            LastPrintSucceeded = LastPrintSucceeded, LastPrintSourceKey = LastPrintSourceKey,
            LastPrintDiagnostic = LastPrintDiagnostic
        };
        foreach (var item in Evidence) copy.Evidence.Add(item);
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
}
