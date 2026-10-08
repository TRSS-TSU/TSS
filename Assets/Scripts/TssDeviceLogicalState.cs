using System;
using System.Collections.Generic;
using UnityEngine;

public sealed class TssDeviceLogicalState
{
    private static readonly Dictionary<string, TssDeviceLogicalState> Cache = new();

    public string DeviceKey { get; }
    public string DeviceName { get; set; }
    public EquipmentCategory Category { get; }
    public string MacAddress { get; set; }
    public string IpAddress { get; set; }
    public string SubnetMask { get; set; }
    public string DefaultGateway { get; set; }
    public string DnsServer { get; set; }
    public bool UseDhcp { get; set; }
    public List<InterfaceLogicalRecord> Interfaces { get; } = new();

    public bool IsSwitchOrRouter => Category == EquipmentCategory.Switch || Category == EquipmentCategory.Router;
    public bool IsDesktopSessionType => Category == EquipmentCategory.DesktopPc || Category == EquipmentCategory.Server || Category == EquipmentCategory.Printer;

    public sealed class InterfaceLogicalRecord
    {
        public string InterfaceName;
        public string EndpointId;
        public bool IsPhysicalLinkUp;
        public string ConnectedPeerDevice;
        public string ConnectedPeerPort;
        public string MacAddress;
        public string IpAddress;
        public int VlanId = 1;
        public string PortMode = "Access";
        public string SpeedDuplex = "100 Mbps Full";
    }

    public TssDeviceLogicalState(string deviceKey, EquipmentCategory category, string deviceName)
    {
        DeviceKey = deviceKey;
        Category = category;
        DeviceName = string.IsNullOrWhiteSpace(deviceName) ? $"{category}" : deviceName.Trim();
        MacAddress = GenerateMac(deviceKey);
        AssignDefaults();
    }

    public static TssDeviceLogicalState FromNetwork(NetworkDeviceState network, TssTrainingSession session)
    {
        if (network == null) return null;
        var state = new TssDeviceLogicalState(network.Key, network.Category, network.Hostname)
        {
            MacAddress = network.MacAddress, IpAddress = network.ManagementAddress,
            SubnetMask = network.ManagementMask, DefaultGateway = network.DefaultGateway,
            DnsServer = network.DnsAddress, UseDhcp = network.UseDhcp
        };
        state.Interfaces.Clear();
        foreach (var iface in network.Interfaces.Values)
            state.Interfaces.Add(new InterfaceLogicalRecord
            {
                InterfaceName = iface.Name, EndpointId = iface.EndpointId, IsPhysicalLinkUp = iface.CarrierUp,
                VlanId = iface.AccessVlan, PortMode = iface.IsTrunk ? "Trunk" : "Access",
                IpAddress = string.IsNullOrEmpty(iface.Address) ? string.Empty : iface.Address + "/24",
                MacAddress = TssNetworkRunProfile.MacFor(session?.NetworkProfile?.Seed ?? 0, network.Key + ":" + iface.Name)
            });
        return state;
    }

    public void SyncFromNetwork(NetworkDeviceState network)
    {
        if (network == null) return;
        DeviceName = network.Hostname; MacAddress = network.MacAddress; IpAddress = network.ManagementAddress;
        SubnetMask = network.ManagementMask; DefaultGateway = network.DefaultGateway; DnsServer = network.DnsAddress;
        UseDhcp = network.UseDhcp;
    }

    private void AssignDefaults()
    {
        SubnetMask = "255.255.255.0";
        DefaultGateway = "192.168.1.1";
        DnsServer = "8.8.8.8";

        switch (Category)
        {
            case EquipmentCategory.Router:
                IpAddress = "192.168.1.1";
                DefaultGateway = "0.0.0.0";
                UseDhcp = false;
                break;
            case EquipmentCategory.Switch:
                IpAddress = "192.168.1.2";
                UseDhcp = false;
                break;
            case EquipmentCategory.Server:
                IpAddress = "192.168.1.10";
                DnsServer = "192.168.1.10";
                UseDhcp = false;
                break;
            case EquipmentCategory.Printer:
                IpAddress = "192.168.1.200";
                UseDhcp = false;
                break;
            case EquipmentCategory.DesktopPc:
            default:
                IpAddress = "192.168.1.101";
                UseDhcp = true;
                break;
        }
    }

    public void RefreshPhysicalConnectivity(IEnumerable<TssPortEndpoint> endpoints, TssTrainingSession session)
    {
        Interfaces.Clear();
        if (endpoints == null)
            return;

        var portIndex = 1;
        foreach (var endpoint in endpoints)
        {
            if (!endpoint)
                continue;

            var rec = new InterfaceLogicalRecord
            {
                InterfaceName = string.IsNullOrWhiteSpace(endpoint.DisplayLabel) ? endpoint.name : endpoint.DisplayLabel,
                EndpointId = endpoint.EndpointId,
                MacAddress = GenerateMac($"{DeviceKey}_{endpoint.EndpointId}"),
                VlanId = 1,
                PortMode = Category == EquipmentCategory.Switch ? "Access" : "N/A",
                SpeedDuplex = "100 Mbps Full"
            };

            if (Category == EquipmentCategory.Router)
                rec.IpAddress = $"192.168.{portIndex}.1/24";

            if (session != null && !string.IsNullOrWhiteSpace(endpoint.EndpointId))
            {
                foreach (var conn in session.PhysicalConnections)
                {
                    if (conn.EndpointAId == endpoint.EndpointId || conn.EndpointBId == endpoint.EndpointId)
                    {
                        rec.IsPhysicalLinkUp = true;
                        var peerEndpointId = conn.EndpointAId == endpoint.EndpointId ? conn.EndpointBId : conn.EndpointAId;
                        rec.ConnectedPeerDevice = TssPhysicalConnectionPanel.DeviceName(peerEndpointId, session);
                        rec.ConnectedPeerPort = TssPhysicalConnectionPanel.EndpointLabel(peerEndpointId, session);
                        break;
                    }
                }
            }

            Interfaces.Add(rec);
            portIndex++;
        }
    }

    public static TssDeviceLogicalState GetOrCreate(string deviceKey, EquipmentCategory category, string deviceName)
    {
        if (string.IsNullOrWhiteSpace(deviceKey))
            deviceKey = $"{category}_{deviceName}";

        if (!Cache.TryGetValue(deviceKey, out var state))
        {
            state = new TssDeviceLogicalState(deviceKey, category, deviceName);
            Cache[deviceKey] = state;
        }
        else if (!string.IsNullOrWhiteSpace(deviceName))
        {
            state.DeviceName = deviceName.Trim();
        }

        return state;
    }

    private static string GenerateMac(string seed)
    {
        var hash = Mathf.Abs(seed != null ? seed.GetHashCode() : 0);
        var b1 = (hash >> 16) & 0xFF;
        var b2 = (hash >> 8) & 0xFF;
        var b3 = hash & 0xFF;
        return $"00:50:79:{b1:X2}:{b2:X2}:{b3:X2}";
    }
}
