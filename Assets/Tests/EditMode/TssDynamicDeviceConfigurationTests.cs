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
}
