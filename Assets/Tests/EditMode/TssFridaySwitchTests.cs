using System;
using System.Linq;
using NUnit.Framework;
using UnityEngine;
using Object = UnityEngine.Object;

public sealed class TssFridaySwitchTests
{
    [Test]
    public void SwitchDefaultsAliasesAndPortFastAreConfigurable()
    {
        var go = new GameObject("FridaySwitchSession", typeof(TssTrainingSession));
        var scenario = ScriptableObject.CreateInstance<TssScenarioDefinition>();
        scenario.networkScenario = new NetworkScenarioConfig { enableLayer23Simulation = true };
        try
        {
            var session = go.GetComponent<TssTrainingSession>();
            session.BeginScenario(scenario);
            var cli = new TssCliCommandProcessor(session, "AN1", EquipmentCategory.Switch, "AN-1");
            var device = cli.Device;
            Assert.IsTrue(device.Svis[1].AdminUp);
            Assert.IsEmpty(device.DefaultGateway);
            StringAssert.DoesNotContain("ip default-gateway", TssCliCommandProcessor.BuildRunningConfig(device));

            cli.Execute("enable");
            cli.Execute("configure terminal");
            cli.Execute("interface Fa0/2");
            Assert.IsTrue(cli.Execute("spanning-tree portfast").Accepted);
            Assert.IsTrue(device.Interfaces["Fa0/2"].SpanningTreePortFast);
            StringAssert.Contains(" spanning-tree portfast", TssCliCommandProcessor.BuildRunningConfig(device));
            Assert.IsTrue(cli.Execute("no spanning-tree portfast").Accepted);
            Assert.IsFalse(device.Interfaces["Fa0/2"].SpanningTreePortFast);

            cli.Execute("interface Fa0/1");
            foreach (var alias in new[] { "sw tr native vlan 190", "swi trunk native vlan 190", "sw trunk native vlan 190", "swi tr native vlan 190" })
                Assert.IsTrue(cli.Execute(alias).Accepted, alias);
            Assert.AreEqual(190, device.Interfaces["Fa0/1"].NativeVlan);
            foreach (var alias in new[] { "sw tr all vlan 340, 40, 20", "swi trunk all vlan 340,40,20", "sw trunk allowed vlan 340,40,20", "swi tr allowed vlan 340,40,20" })
                Assert.IsTrue(cli.Execute(alias).Accepted, alias);
            CollectionAssert.AreEquivalent(new[] { 340, 40, 20 }, device.Interfaces["Fa0/1"].AllowedVlans);
            Assert.IsFalse(cli.Execute("sw tr all vlan 4095").Accepted);

            cli.Execute("interface Vlan1");
            Assert.IsTrue(cli.Execute("shutdown").Accepted);
            Assert.IsFalse(device.Svis[1].AdminUp);
            cli.Execute("end");
            Assert.IsFalse(cli.Execute("show ip route").Accepted);
            Assert.IsFalse(cli.Execute("ip route 0.0.0.0 0.0.0.0 10.1.95.2").Accepted);
            Assert.IsEmpty(device.Routes);
            Assert.IsFalse(cli.Execute("sh ip ro").Accepted);
            Assert.IsFalse(device.Evidence.Contains("show ip route"));
            StringAssert.Contains("% Invalid show command", TssCliCommandProcessor.BuildResultsShowOutput(device, "show ip route"));

            cli.Execute("configure terminal");
            cli.Execute("line vty 0 4");
            Assert.AreEqual(TssNetworkCliMode.VlanConfig, cli.Execute("vlan 20").Mode);
            Assert.AreEqual(TssNetworkCliMode.LineConfig, cli.Execute("line vty 5 15").Mode);
            Assert.AreEqual(TssNetworkCliMode.GlobalConfig, cli.Execute("exit").Mode);
        }
        finally
        {
            Object.DestroyImmediate(scenario);
            Object.DestroyImmediate(go);
        }
    }

    [Test]
    public void AccessPortAssessmentRequiresPortFast()
    {
        var config = new NetworkScenarioConfig
        {
            requiredPorts = new[] { new NetworkRequiredPort { deviceRole = "AN-1", interfaceName = "Fa0/2", endpointId = "IT1" } }
        };
        var state = new TssNetworkSessionState(TssNetworkRunProfile.Create(21, config));
        var sw = state.GetOrCreateDevice("AN1", EquipmentCategory.Switch, "AN-1");
        var port = new NetworkInterfaceState { Name = "Fa0/2", AdminUp = true, CarrierUp = true, AccessVlan = state.Profile.ManagementVlan };
        sw.Interfaces[port.Name] = port;
        var assessor = new TssNetworkWorkPackageEvaluator(state);
        Assert.IsFalse(assessor.EvaluateLayer2().IsComplete("WP2.Access:AN-1:Fa0/2"));
        port.SpanningTreePortFast = true;
        Assert.IsTrue(assessor.EvaluateLayer2().IsComplete("WP2.Access:AN-1:Fa0/2"));
    }
}
