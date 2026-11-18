using NUnit.Framework;
using UnityEngine;
using Object = UnityEngine.Object;

public sealed class TssDynamicDeviceConfigurationTests
{
    [Test]
    public void SwitchDescriptionsAndSviAreAcceptedAndCloned()
    {
        var sessionObject = new GameObject("DynamicConfigSession");
        var scenario = ScriptableObject.CreateInstance<TssScenarioDefinition>();
        scenario.networkScenario = new NetworkScenarioConfig { enableLayer23Simulation = true };
        try
        {
            var session = sessionObject.AddComponent<TssTrainingSession>();
            session.BeginScenario(scenario);
            var cli = new TssCliCommandProcessor(session, "SW", EquipmentCategory.Switch, "SW");
            cli.Execute("enable"); cli.Execute("configure terminal");
            Assert.IsTrue(cli.Execute("interface range Fa0/1 - Fa0/2").Accepted);
            Assert.IsTrue(cli.Execute("description access floor").Accepted);
            Assert.IsTrue(cli.Execute("interface Vlan10").Accepted);
            Assert.IsTrue(cli.Execute("description management vlan").Accepted);
            Assert.IsTrue(cli.Execute("ip address 192.0.2.10 255.255.255.0").Accepted);
            Assert.IsTrue(cli.Execute("shutdown").Accepted);
            var clone = session.NetworkState.TryGetDevice("SW", out var device) ? device.Clone() : null;
            Assert.IsNotNull(clone);
            Assert.AreEqual("access floor", clone.Interfaces["Fa0/1"].Description);
            Assert.AreEqual("management vlan", clone.Svis[10].Description);
            StringAssert.Contains("interface Vlan10", TssCliCommandProcessor.BuildRunningConfig(clone));
        }
        finally { Object.DestroyImmediate(scenario); Object.DestroyImmediate(sessionObject); }
    }

    [Test]
    public void RouterRejectsSwitchSviButKeepsSubinterfaceDescriptions()
    {
        var sessionObject = new GameObject("DynamicRouterConfigSession");
        var scenario = ScriptableObject.CreateInstance<TssScenarioDefinition>();
        scenario.networkScenario = new NetworkScenarioConfig { enableLayer23Simulation = true };
        try
        {
            var session = sessionObject.AddComponent<TssTrainingSession>();
            session.BeginScenario(scenario);
            var cli = new TssCliCommandProcessor(session, "R1", EquipmentCategory.Router, "R1");
            cli.Execute("enable"); cli.Execute("configure terminal");
            Assert.IsFalse(cli.Execute("interface Vlan10").Accepted);
            Assert.IsTrue(cli.Execute("interface Fa0/0.10").Accepted);
            Assert.IsTrue(cli.Execute("description data gateway").Accepted);
            StringAssert.Contains("description data gateway", cli.Execute("do show running-config").Output);
        }
        finally { Object.DestroyImmediate(scenario); Object.DestroyImmediate(sessionObject); }
    }

    [Test]
    public void DescUsesExistingDescriptionBehavior()
    {
        var sessionObject = new GameObject("DescAliasSession");
        var scenario = ScriptableObject.CreateInstance<TssScenarioDefinition>();
        scenario.networkScenario = new NetworkScenarioConfig { enableLayer23Simulation = true };
        try
        {
            var session = sessionObject.AddComponent<TssTrainingSession>();
            session.BeginScenario(scenario);
            var cli = new TssCliCommandProcessor(session, "SW", EquipmentCategory.Switch, "SW");
            cli.Execute("enable"); cli.Execute("configure terminal");
            Assert.IsTrue(cli.Execute("interface range Fa0/1 - Fa0/2").Accepted);
            Assert.IsTrue(cli.Execute("desc Uplink Interface").Accepted);
            Assert.AreEqual("Uplink Interface", cli.Device.Interfaces["Fa0/1"].Description);
            Assert.AreEqual("Uplink Interface", cli.Device.Interfaces["Fa0/2"].Description);
            StringAssert.Contains(" description Uplink Interface", cli.Execute("do show running-config").Output);
            StringAssert.Contains("desc <text>", cli.Execute("?").Output);
            StringAssert.Contains("desc <text>", TssCliCommandProcessor.BuildCommandReference(EquipmentCategory.Switch));
            Assert.IsFalse(cli.Execute("desc").Accepted);
            Assert.IsFalse(cli.Execute("descriptionless text").Accepted);
            Assert.IsTrue(cli.Execute("no desc").Accepted);
            Assert.AreEqual(string.Empty, cli.Device.Interfaces["Fa0/1"].Description);
            Assert.IsTrue(cli.Execute("interface Vlan10").Accepted);
            Assert.IsTrue(cli.Execute("desc Management SVI").Accepted);
            Assert.AreEqual("Management SVI", cli.Device.Svis[10].Description);
            StringAssert.Contains(" description Management SVI", cli.Execute("do show running-config").Output);
            StringAssert.Contains("desc <text>", cli.Execute("?").Output);
            Assert.IsTrue(cli.Execute("no desc").Accepted);
            Assert.AreEqual(string.Empty, cli.Device.Svis[10].Description);

            var router = new TssCliCommandProcessor(session, "R1", EquipmentCategory.Router, "R1");
            router.Execute("enable"); router.Execute("configure terminal");
            Assert.IsTrue(router.Execute("interface Fa0/0.10").Accepted);
            Assert.IsTrue(router.Execute("desc Data Gateway").Accepted);
            Assert.AreEqual("Data Gateway", router.Device.Interfaces["Fa0/0.10"].Description);
            StringAssert.Contains(" description Data Gateway", router.Execute("do show running-config").Output);
            StringAssert.Contains("desc <text>", router.Execute("?").Output);
            Assert.IsTrue(router.Execute("no desc").Accepted);
            Assert.AreEqual(string.Empty, router.Device.Interfaces["Fa0/0.10"].Description);
            Assert.IsTrue(router.Execute("interface Fa0/0").Accepted);
            Assert.IsTrue(router.Execute("desc Parent Interface").Accepted);
            Assert.AreEqual("Parent Interface", router.Device.Interfaces["Fa0/0"].Description);
        }
        finally { Object.DestroyImmediate(scenario); Object.DestroyImmediate(sessionObject); }
    }

    [Test]
    public void EncapUsesExistingRouterEncapsulationBehavior()
    {
        var sessionObject = new GameObject("EncapAliasSession");
        var scenario = ScriptableObject.CreateInstance<TssScenarioDefinition>();
        scenario.networkScenario = new NetworkScenarioConfig { enableLayer23Simulation = true };
        try
        {
            var session = sessionObject.AddComponent<TssTrainingSession>();
            session.BeginScenario(scenario);
            var router = new TssCliCommandProcessor(session, "R1", EquipmentCategory.Router, "R1");
            router.Execute("enable"); router.Execute("configure terminal");
            Assert.IsTrue(router.Execute("interface Fa0/0.100").Accepted);
            Assert.IsTrue(router.Execute("encap dot1Q 100").Accepted);
            Assert.AreEqual(100, router.Device.Interfaces["Fa0/0.100"].Dot1qVlan);
            StringAssert.Contains(" encapsulation dot1q 100", router.Execute("do show running-config").Output);
            StringAssert.Contains("encap dot1q <vlan>", router.Execute("?").Output);
            StringAssert.Contains("encap dot1q <vlan>", TssCliCommandProcessor.BuildCommandReference(EquipmentCategory.Router));
            Assert.IsFalse(router.Execute("encap dot1Q 4095").Accepted);
            Assert.AreEqual(100, router.Device.Interfaces["Fa0/0.100"].Dot1qVlan);
            Assert.IsFalse(router.Execute("encapsulationless dot1Q 100").Accepted);
            Assert.IsTrue(router.Execute("no encap dot1Q").Accepted);
            Assert.AreEqual(0, router.Device.Interfaces["Fa0/0.100"].Dot1qVlan);

            var switchCli = new TssCliCommandProcessor(session, "SW", EquipmentCategory.Switch, "SW");
            switchCli.Execute("enable"); switchCli.Execute("configure terminal");
            Assert.IsTrue(switchCli.Execute("interface Fa0/1").Accepted);
            Assert.IsFalse(switchCli.Execute("encap dot1Q 100").Accepted);
            Assert.IsFalse(switchCli.Execute("no encap dot1Q").Accepted);
        }
        finally { Object.DestroyImmediate(scenario); Object.DestroyImmediate(sessionObject); }
    }
}
