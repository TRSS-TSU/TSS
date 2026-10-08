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

    private TssNetworkRunProfile(int seed, NetworkScenarioConfig config)
    {
        Seed = seed;
        var random = new System.Random(seed);
        var vlans = Unique(random, 5, 10, 400);
        ManagementVlan = vlans[0]; ReceptionVlan = vlans[1]; DataVlan = vlans[2]; NativeVlan = vlans[3]; DeadVlan = vlans[4];
        var nets = Unique(random, 3, 20, 220);
        ManagementNetwork = $"10.{nets[0]}.10";
        ReceptionNetwork = $"10.{nets[1]}.20";
        DataNetwork = $"10.{nets[2]}.30";
        var wan = random.Next(20, 220);
        WanCustomerAddress = $"172.16.{wan}.1";
        WanProviderAddress = $"172.16.{wan}.2";
        EnableSecret = Token(random); ConsolePassword = Token(random); VtyPassword = Token(random); Motd = "AUTHORIZED TRAINING ACCESS ONLY";
        Add("IT1", ManagementNetwork, 11); Add("IT2", ManagementNetwork, 12);
        Add("Reception1", ReceptionNetwork, 21); Add("Reception2", ReceptionNetwork, 22);
        Add("Data1", DataNetwork, 31); Add("Data2", DataNetwork, 32); Add("Data3", DataNetwork, 33);
    }
    public static TssNetworkRunProfile Create(int seed, NetworkScenarioConfig config) => new(seed, config);
    private void Add(string role, string network, int host) => EndpointAddresses[role] = network + "." + host;
    private static int[] Unique(System.Random random, int count, int min, int max)
    {
        var values = new HashSet<int>(); while (values.Count < count) values.Add(random.Next(min, max)); return values.ToArray();
    }
    private static string Token(System.Random random) => "T" + random.Next(100000, 999999).ToString(CultureInfo.InvariantCulture);
}

public enum TssNetworkCliMode { UserExec, PrivilegedExec, GlobalConfig, InterfaceConfig, LineConfig, VlanConfig }

public sealed class TssNetworkSessionState
{
    private readonly Dictionary<string, NetworkDeviceState> _devices = new(StringComparer.OrdinalIgnoreCase);
    private readonly Dictionary<string, EndpointNetworkState> _endpoints = new(StringComparer.OrdinalIgnoreCase);
    public TssNetworkRunProfile Profile { get; }
    public IEnumerable<NetworkDeviceState> Devices => _devices.Values;
    public TssNetworkSessionState(TssNetworkRunProfile profile) { Profile = profile; }
    public NetworkDeviceState GetOrCreateDevice(string key, EquipmentCategory category, string displayName)
    {
        if (_devices.TryGetValue(key, out var state)) return state;
        state = new NetworkDeviceState(key, category, displayName, Profile);
        _devices.Add(key, state); return state;
    }
    public bool TryGetDevice(string key, out NetworkDeviceState state) => _devices.TryGetValue(key, out state);
    public bool TryGetEndpoint(string key, out EndpointNetworkState state) => _endpoints.TryGetValue(key, out state);
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
    public NetworkDeviceState(string key, EquipmentCategory category, string name, TssNetworkRunProfile profile)
    {
        Key = key; Category = category; Hostname = name;
        if (category == EquipmentCategory.Switch) Vlans[1] = "default";
    }
    public NetworkDeviceState Clone()
    {
        var copy = new NetworkDeviceState(Key, Category, Hostname, null)
        {
            EnableSecret = EnableSecret, ConsolePassword = ConsolePassword, VtyPassword = VtyPassword, Motd = Motd,
            PasswordEncryption = PasswordEncryption, DomainLookupDisabled = DomainLookupDisabled, Mode = Mode,
            SelectedVlan = SelectedVlan, SelectedLine = SelectedLine, SelectedInterfaces = new List<string>(SelectedInterfaces)
        };
        foreach (var vlan in Vlans) copy.Vlans[vlan.Key] = vlan.Value;
        foreach (var pair in Interfaces) copy.Interfaces[pair.Key] = pair.Value.Clone();
        foreach (var route in Routes) copy.Routes.Add(new NetworkRouteState { Network = route.Network, Mask = route.Mask, NextHop = route.NextHop });
        foreach (var item in Evidence) copy.Evidence.Add(item);
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
    public readonly string MacAddress;
    public bool UseDhcp;
    public string Address;
    public string Mask = "255.255.255.0";
    public string Gateway;
    public string Dns = "8.8.8.8";
    public bool Layer1Connected;
    public bool Layer2Connected;

    public EndpointNetworkState(string key, string role, TssNetworkRunProfile profile)
    {
        Key = key;
        Role = role ?? string.Empty;
        MacAddress = StableMac((profile?.Seed ?? 0) + ":" + key);
        Address = profile != null && profile.EndpointAddresses.TryGetValue(Role, out var address) ? address : string.Empty;
        Gateway = GatewayFor(Address, profile);
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

    private static string StableMac(string value)
    {
        uint hash = 2166136261;
        foreach (var c in value ?? string.Empty) { hash ^= c; hash *= 16777619; }
        return $"02:{(hash >> 24) & 0xFF:X2}:{(hash >> 16) & 0xFF:X2}:{(hash >> 8) & 0xFF:X2}:{hash & 0xFF:X2}:01";
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
        return (mask | (mask - 1)) == uint.MaxValue;
    }
}
