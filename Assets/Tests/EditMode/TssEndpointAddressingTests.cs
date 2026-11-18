using NUnit.Framework;

public sealed class TssEndpointAddressingTests
{
    [TestCase(EquipmentCategory.Server, "IT2")]
    [TestCase(EquipmentCategory.DesktopPc, "IT1")]
    [TestCase(EquipmentCategory.Printer, "Reception2")]
    [TestCase(EquipmentCategory.AccessPoint, "AP-1")]
    public void StaticEndpointsRetainValidButIncorrectSettings(EquipmentCategory category, string role)
    {
        var profile = TssNetworkRunProfile.Create(12345, new NetworkScenarioConfig());
        var state = new TssNetworkSessionState(profile);
        var endpoint = state.GetOrCreateEndpoint("endpoint", role, category);
        var commands = new TssEndpointCommandProcessor(state);

        Assert.IsTrue(commands.TryApply(endpoint.Key, false, " 192.0.2.99 ", "255.255.0.0", "198.51.100.1", "203.0.113.53", out var error), error);
        Assert.AreSame(endpoint, state.GetOrCreateEndpoint(endpoint.Key, role, category));
        Assert.IsFalse(endpoint.UseDhcp);
        Assert.AreEqual("192.0.2.99", endpoint.Address);
        Assert.AreEqual("255.255.0.0", endpoint.Mask);
        Assert.AreEqual("198.51.100.1", endpoint.Gateway);
        Assert.AreEqual("203.0.113.53", endpoint.Dns);
        var output = commands.Execute(endpoint.Key, "ipconfig").Output;
        foreach (var value in new[] { endpoint.Address, endpoint.Mask, endpoint.Gateway, endpoint.Dns })
            StringAssert.Contains(value, output);

        var expectedAddress = state.ExpectedAddress(endpoint);
        Assert.IsTrue(commands.TryApply(endpoint.Key, false, expectedAddress, "255.255.255.0", EndpointNetworkState.GatewayFor(expectedAddress, profile), profile.WanProviderAddress, out error), error);
        Assert.AreEqual(expectedAddress, endpoint.Address);
    }

    [TestCase("999.1.1.1", "255.255.255.0", "192.0.2.1", "8.8.8.8")]
    [TestCase("192.0.2.2", "255.0.255.0", "192.0.2.1", "8.8.8.8")]
    [TestCase("192.0.2.2", "255.255.255.0", "invalid", "8.8.8.8")]
    [TestCase("192.0.2.2", "255.255.255.0", "192.0.2.1", "invalid")]
    public void MalformedStaticSettingsLeaveSavedValuesUnchanged(string address, string mask, string gateway, string dns)
    {
        var state = new TssNetworkSessionState(TssNetworkRunProfile.Create(12345, new NetworkScenarioConfig()));
        var endpoint = state.GetOrCreateEndpoint("pc", "IT1");
        var commands = new TssEndpointCommandProcessor(state);
        Assert.IsTrue(commands.TryApply(endpoint.Key, false, "192.0.2.99", "255.255.255.0", "192.0.2.1", "8.8.8.8", out _));

        Assert.IsFalse(commands.TryApply(endpoint.Key, false, address, mask, gateway, dns, out var error));
        Assert.AreEqual("Enter valid IPv4 settings.", error);
        Assert.AreEqual("192.0.2.99", endpoint.Address);
        Assert.AreEqual("255.255.255.0", endpoint.Mask);
        Assert.AreEqual("192.0.2.1", endpoint.Gateway);
        Assert.AreEqual("8.8.8.8", endpoint.Dns);
    }

    [Test]
    public void LaptopDoesNotReceiveDhcpBeforeWirelessAssociation()
    {
        var profile = TssNetworkRunProfile.Create(12345, new NetworkScenarioConfig());
        var state = new TssNetworkSessionState(profile);
        state.GetOrCreateEndpoint("laptop", "Wireless Laptop", EquipmentCategory.Laptop);

        Assert.IsFalse(new TssEndpointCommandProcessor(state).TryApply("laptop", true, string.Empty, string.Empty, string.Empty, string.Empty, out _));
        Assert.IsFalse(new TssEndpointCommandProcessor(state).TryApply("laptop", false, "192.0.2.2", "255.255.255.0", "192.0.2.1", "8.8.8.8", out var error));
        StringAssert.Contains("must use DHCP", error);
    }

    [Test]
    public void PrinterUsesItsStaticSopAddress()
    {
        var profile = TssNetworkRunProfile.Create(12345, new NetworkScenarioConfig());
        var state = new TssNetworkSessionState(profile);
        var printer = state.GetOrCreateEndpoint("printer", "Reception2", EquipmentCategory.Printer);
        var commands = new TssEndpointCommandProcessor(state);

        Assert.IsFalse(commands.TryApply("printer", true, string.Empty, string.Empty, string.Empty, string.Empty, out _));
        Assert.IsTrue(commands.TryApply("printer", false, profile.EndpointAddresses["Reception2"], "255.255.255.0", profile.ReceptionNetwork + ".1", "8.8.8.8", out _));
        Assert.AreEqual(profile.EndpointAddresses["Reception2"], printer.Address);
    }

    [Test]
    public void ManagementNetworkRemainsStaticWhenReceptionIsDhcpCapable()
    {
        var profile = TssNetworkRunProfile.Create(12345, new NetworkScenarioConfig());
        var state = new TssNetworkSessionState(profile);
        var endpoint = state.GetOrCreateEndpoint("pc", "IT1", EquipmentCategory.DesktopPc);

        Assert.IsFalse(new TssEndpointCommandProcessor(state).TryApply("pc", true, string.Empty, string.Empty, string.Empty, string.Empty, out var error));
        StringAssert.Contains("static addressing", error);
        Assert.IsFalse(endpoint.UseDhcp);
        Assert.AreEqual(string.Empty, endpoint.Address);
    }

    [Test]
    public void LogicalStateMirrorsStaticAndDhcpEndpointAddressing()
    {
        var profile = TssNetworkRunProfile.Create(12345, new NetworkScenarioConfig());
        var state = new TssNetworkSessionState(profile);
        var endpoint = state.GetOrCreateEndpoint("pc", "Reception1", EquipmentCategory.DesktopPc);
        var logical = new TssDeviceLogicalState("pc", EquipmentCategory.DesktopPc, "PC");

        logical.SyncFromEndpoint(endpoint);
        Assert.IsFalse(logical.UseDhcp);
        Assert.AreEqual(string.Empty, logical.IpAddress);

        endpoint.UseDhcp = true;
        endpoint.Address = profile.EndpointAddresses["Reception1"];
        endpoint.Mask = "255.255.255.0";
        endpoint.Gateway = profile.ReceptionNetwork + ".1";
        endpoint.Dns = profile.WanProviderAddress;
        logical.SyncFromEndpoint(endpoint);

        Assert.IsTrue(logical.UseDhcp);
        Assert.AreEqual(endpoint.Address, logical.IpAddress);
        Assert.AreEqual(endpoint.Gateway, logical.DefaultGateway);
        Assert.AreEqual(endpoint.Dns, logical.DnsServer);
    }

    [Test]
    public void LaptopDhcpRequiresProviderUplinkAndRoute()
    {
        var profile = TssNetworkRunProfile.Create(12345, new NetworkScenarioConfig
        {
            routerRole = "CN-1", routerWanInterface = "Fa0/1",
            accessPointSwitchRole = "AN-1", accessPointSwitchPort = "Fa0/7"
        });
        var state = new TssNetworkSessionState(profile);
        var laptop = state.GetOrCreateEndpoint("laptop", "Wireless Laptop", EquipmentCategory.Laptop);
        laptop.Layer1Connected = laptop.Layer2Connected = true;
        state.SetAccessPointUplink(true);
        state.AccessPoint.Configured = true;

        var accessSwitch = state.GetOrCreateDevice("switch", EquipmentCategory.Switch, "AN-1", "AN-1");
        accessSwitch.Interfaces["Fa0/7"] = new NetworkInterfaceState { Name = "Fa0/7", AdminUp = true, CarrierUp = true, AccessVlan = profile.ReceptionVlan };
        var router = state.GetOrCreateDevice("router", EquipmentCategory.Router, "CN-1", "CN-1");
        router.Interfaces["Fa0/1"] = new NetworkInterfaceState { Name = "Fa0/1", AdminUp = true, CarrierUp = true, Address = profile.WanCustomerAddress, Mask = "255.255.255.252" };
        var commands = new TssEndpointCommandProcessor(state);

        Assert.IsFalse(commands.TryApply("laptop", true, string.Empty, string.Empty, string.Empty, string.Empty, out _));
        router.Routes.Add(new NetworkRouteState { Network = "0.0.0.0", Mask = "0.0.0.0", NextHop = profile.WanProviderAddress });
        Assert.IsFalse(commands.TryApply("laptop", true, string.Empty, string.Empty, string.Empty, string.Empty, out _));
        state.SetProviderDemarcConnected(true);
        Assert.IsTrue(commands.TryApply("laptop", true, string.Empty, string.Empty, string.Empty, string.Empty, out _));
        Assert.AreEqual(profile.WanProviderAddress, laptop.Dns);
    }

    [Test]
    public void WiredDhcpUsesScenarioNetworkAndRequiresRouterAndProviderPath()
    {
        var config = new NetworkScenarioConfig
        {
            dhcpNetworkNames = new[] { "Reception" },
            routerRole = "CN-1", routerParentInterface = "Fa0/0", routerWanInterface = "Fa0/1",
            requiredPorts = new[] { new NetworkRequiredPort { deviceRole = "AN-1", interfaceName = "Fa0/3", endpointId = "Reception1", vlanName = "Reception" } },
            trunkRequirements = new[] { new NetworkTrunkRequirement { deviceRole = "AN-1", interfaceName = "Fa0/6", peerDeviceRole = "CN-1", peerInterfaceName = "Fa0/0", nativeVlanName = "Native", allowedVlanNames = new[] { "Reception" } } },
            routerSubinterfaceRequirements = new[] { new NetworkRouterSubinterfaceRequirement { networkName = "Reception", vlanName = "Reception" } }
        };
        var profile = TssNetworkRunProfile.Create(12345, config);
        var state = new TssNetworkSessionState(profile);
        var endpoint = state.GetOrCreateEndpoint("pc", "Reception1", EquipmentCategory.DesktopPc);
        endpoint.Layer1Connected = endpoint.Layer2Connected = true;
        var access = state.GetOrCreateDevice("switch", EquipmentCategory.Switch, "AN-1");
        access.ExpectedRole = "AN-1";
        access.Vlans[profile.ReceptionVlan] = profile.ReceptionVlanName;
        access.Interfaces["Fa0/3"] = new NetworkInterfaceState { Name = "Fa0/3", AdminUp = true, CarrierUp = true, AccessVlan = profile.ReceptionVlan, EndpointId = "pc" };
        access.Interfaces["Fa0/6"] = new NetworkInterfaceState { Name = "Fa0/6", AdminUp = true, CarrierUp = true, IsTrunk = true, NativeVlan = profile.NativeVlan, AllowedVlans = new System.Collections.Generic.HashSet<int> { profile.ReceptionVlan } };
        var router = state.GetOrCreateDevice("router", EquipmentCategory.Router, "CN-1");
        router.ExpectedRole = "CN-1";
        router.Interfaces["Fa0/0"] = new NetworkInterfaceState { Name = "Fa0/0", AdminUp = true, CarrierUp = true };
        router.Interfaces["Fa0/0.10"] = new NetworkInterfaceState { Name = "Fa0/0.10", AdminUp = true, CarrierUp = true, Dot1qVlan = profile.ReceptionVlan, Address = profile.ReceptionNetwork + ".1", Mask = "255.255.255.0" };
        router.Interfaces["Fa0/1"] = new NetworkInterfaceState { Name = "Fa0/1", AdminUp = true, CarrierUp = true, Address = profile.WanCustomerAddress, Mask = "255.255.255.252" };
        router.Routes.Add(new NetworkRouteState { Network = "0.0.0.0", Mask = "0.0.0.0", NextHop = profile.WanProviderAddress });
        state.SetProviderDemarcConnected(true);

        Assert.IsTrue(new TssEndpointCommandProcessor(state).TryApply("pc", true, string.Empty, string.Empty, string.Empty, string.Empty, out var error), error);
        Assert.AreEqual(profile.EndpointAddresses["Reception1"], endpoint.Address);
        Assert.AreEqual(profile.ReceptionNetwork + ".1", endpoint.Gateway);
        Assert.AreEqual(profile.WanProviderAddress, endpoint.Dns);
    }
}
