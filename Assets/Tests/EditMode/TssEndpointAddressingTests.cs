using NUnit.Framework;

public sealed class TssEndpointAddressingTests
{
    [Test]
    public void ServerRejectsAddressOutsideItsSopPlan()
    {
        var profile = TssNetworkRunProfile.Create(12345, new NetworkScenarioConfig());
        var state = new TssNetworkSessionState(profile);
        var server = state.GetOrCreateEndpoint("server", "IT2", EquipmentCategory.Server);
        var commands = new TssEndpointCommandProcessor(state);

        Assert.IsFalse(commands.TryApply("server", false, profile.ManagementNetwork + ".99", "255.255.255.0", profile.ManagementNetwork + ".1", "8.8.8.8", out _));
        Assert.IsTrue(commands.TryApply("server", false, profile.EndpointAddresses["IT2"], "255.255.255.0", profile.ManagementNetwork + ".1", "8.8.8.8", out _));
        Assert.AreEqual(profile.EndpointAddresses["IT2"], server.Address);
    }

    [Test]
    public void LaptopDoesNotReceiveDhcpBeforeWirelessAssociation()
    {
        var profile = TssNetworkRunProfile.Create(12345, new NetworkScenarioConfig());
        var state = new TssNetworkSessionState(profile);
        state.GetOrCreateEndpoint("laptop", "Wireless Laptop", EquipmentCategory.Laptop);

        Assert.IsFalse(new TssEndpointCommandProcessor(state).TryApply("laptop", true, string.Empty, string.Empty, string.Empty, string.Empty, out _));
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
    public void LaptopDhcpRequiresProviderUplinkAndRoute()
    {
        var profile = TssNetworkRunProfile.Create(12345, new NetworkScenarioConfig());
        var state = new TssNetworkSessionState(profile);
        var laptop = state.GetOrCreateEndpoint("laptop", "Wireless Laptop", EquipmentCategory.Laptop);
        laptop.Layer1Connected = laptop.Layer2Connected = true;
        state.SetAccessPointUplink(true);
        state.AccessPoint.Configured = true;

        var accessSwitch = state.GetOrCreateDevice("switch", EquipmentCategory.Switch, "AN-1");
        accessSwitch.Interfaces["Fa0/7"] = new NetworkInterfaceState { Name = "Fa0/7", AdminUp = true, CarrierUp = true, AccessVlan = profile.ReceptionVlan };
        var router = state.GetOrCreateDevice("router", EquipmentCategory.Router, "CN-1");
        router.Interfaces["Fa0/1"] = new NetworkInterfaceState { Name = "Fa0/1", AdminUp = true, CarrierUp = true, Address = profile.WanCustomerAddress, Mask = "255.255.255.252" };
        var commands = new TssEndpointCommandProcessor(state);

        Assert.IsFalse(commands.TryApply("laptop", true, string.Empty, string.Empty, string.Empty, string.Empty, out _));
        router.Routes.Add(new NetworkRouteState { Network = "0.0.0.0", Mask = "0.0.0.0", NextHop = profile.WanProviderAddress });
        Assert.IsFalse(commands.TryApply("laptop", true, string.Empty, string.Empty, string.Empty, string.Empty, out _));
        state.SetProviderDemarcConnected(true);
        Assert.IsTrue(commands.TryApply("laptop", true, string.Empty, string.Empty, string.Empty, string.Empty, out _));
        Assert.AreEqual(profile.WanProviderAddress, laptop.Dns);
    }
}
