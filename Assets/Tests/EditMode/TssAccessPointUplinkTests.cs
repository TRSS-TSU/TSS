using System.Collections.Generic;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;

public sealed class TssAccessPointUplinkTests
{
    [TestCase(8)]
    [TestCase(37)]
    public void AccessPointCanPingAnotherRouterSubnetOnlyThroughItsValidGatewayAndTrunk(int seed)
    {
        var profile = TssNetworkRunProfile.Create(seed, new NetworkScenarioConfig
        {
            routerRole = "CN-1", routerParentInterface = "Fa0/0",
            accessPointSwitchRole = "AN-1", accessPointSwitchPort = "Fa0/7",
            routerSubinterfaceRequirements = new[]
            {
                new NetworkRouterSubinterfaceRequirement { vlanName = "Reception", networkName = "Reception" },
                new NetworkRouterSubinterfaceRequirement { vlanName = "Management", networkName = "Management" }
            },
            trunkRequirements = new[]
            {
                new NetworkTrunkRequirement { deviceRole = "AN-1", interfaceName = "Fa0/6", peerDeviceRole = "CN-1", peerInterfaceName = "Fa0/0" }
            }
        });
        var state = new TssNetworkSessionState(profile);
        var ap = state.GetOrCreateEndpoint("AP-1", "AP-1", EquipmentCategory.AccessPoint);
        ap.Address = profile.ApManagementAddress;
        ap.Mask = "255.255.255.0";
        ap.Gateway = profile.ReceptionNetwork + ".1";
        ap.Layer1Connected = ap.Layer2Connected = true;
        var access = state.GetOrCreateDevice("AN-1", EquipmentCategory.Switch, "AN-1");
        access.Vlans[profile.ReceptionVlan] = profile.ReceptionVlanName;
        access.Interfaces["Fa0/7"] = new NetworkInterfaceState { Name = "Fa0/7", AdminUp = true, CarrierUp = true, AccessVlan = profile.ReceptionVlan };
        var trunk = access.Interfaces["Fa0/6"] = new NetworkInterfaceState
        {
            Name = "Fa0/6", AdminUp = true, CarrierUp = true, IsTrunk = true,
            NativeVlan = profile.NativeVlan, AllowedVlans = new HashSet<int> { profile.ReceptionVlan }
        };
        var router = state.GetOrCreateDevice("CN-1", EquipmentCategory.Router, "CN-1");
        var parent = router.Interfaces["Fa0/0"] = new NetworkInterfaceState { Name = "Fa0/0", AdminUp = true, CarrierUp = true };
        router.Interfaces["Fa0/0." + profile.ReceptionVlan] = new NetworkInterfaceState
        {
            Name = "Fa0/0." + profile.ReceptionVlan, Dot1qVlan = profile.ReceptionVlan,
            Address = ap.Gateway, Mask = "255.255.255.0"
        };
        var managementName = "Fa0/0." + profile.ManagementVlan;
        var management = router.Interfaces[managementName] = new NetworkInterfaceState
        {
            Name = managementName, Dot1qVlan = profile.ManagementVlan,
            Address = profile.ManagementNetwork + ".1", Mask = "255.255.255.0"
        };
        var target = management.Address;
        var evaluator = new TssNetworkReachabilityEvaluator(state);
        var reachable = evaluator.Evaluate(ap.Key, target);
        Assert.IsTrue(reachable.Success, reachable.Diagnostic);
        CollectionAssert.AreEqual(new[] { ap.Address, ap.Gateway, target }, reachable.TraceHops);
        Assert.IsTrue(evaluator.Evaluate(ap.Key, ap.Gateway).Success);

        access.Interfaces["Fa0/7"].AccessVlan = profile.ManagementVlan;
        Assert.AreEqual(2, evaluator.Evaluate(ap.Key, target).FailedLayer);
        access.Interfaces["Fa0/7"].AccessVlan = profile.ReceptionVlan;
        trunk.AllowedVlans.Clear();
        Assert.AreEqual(2, evaluator.Evaluate(ap.Key, target).FailedLayer);
        trunk.AllowedVlans.Add(profile.ReceptionVlan);
        parent.AdminUp = false;
        Assert.AreEqual(TssReachabilityFaultCategory.InterfaceUnavailable, evaluator.Evaluate(ap.Key, target).FaultCategory);
        parent.AdminUp = true;
        management.Mask = "255.255.0.0";
        Assert.AreEqual(3, evaluator.Evaluate(ap.Key, target).FailedLayer);
        management.Mask = "255.255.255.0";
        management.Dot1qVlan = profile.DataVlan;
        Assert.AreEqual(3, evaluator.Evaluate(ap.Key, target).FailedLayer);
        management.Dot1qVlan = profile.ManagementVlan;
        management.Address = profile.ManagementNetwork + ".99";
        Assert.AreEqual(3, evaluator.Evaluate(ap.Key, target).FailedLayer);
        management.Address = target;
        router.Interfaces.Remove(managementName);
        Assert.AreEqual(3, evaluator.Evaluate(ap.Key, target).FailedLayer);
        router.Interfaces[managementName] = management;
        ap.Gateway = profile.ReceptionNetwork + ".99";
        Assert.AreEqual(3, evaluator.Evaluate(ap.Key, target).FailedLayer);
        ap.Gateway = profile.ReceptionNetwork + ".1";
        ap.Address = string.Empty;
        Assert.AreEqual(3, evaluator.Evaluate(ap.Key, target).FailedLayer);
        ap.Address = profile.ApManagementAddress;
        ap.Gateway = string.Empty;
        Assert.AreEqual(3, evaluator.Evaluate(ap.Key, target).FailedLayer);
        ap.Gateway = profile.ReceptionNetwork + ".1";
        Assert.IsTrue(evaluator.Evaluate(ap.Key, target).Success);
    }

    [Test]
    public void AccessPointUplinkRequiresMatchingReceptionAndPatchPanelPorts()
    {
        var root = new GameObject("AP Uplink Test");
        var session = root.AddComponent<TssTrainingSession>();
        var ap = Endpoint(root, "Endpoint:APPlacement:Eth01");
        var switchport = Endpoint(root, "Rack:Rack01:U35:Fa07");
        var walls = new List<TssPortEndpoint>();
        var panels = new List<TssPortEndpoint>();
        var permanent = new ScenarioPermanentCableConnection[4];
        for (var port = 1; port <= 4; port++)
        {
            var suffix = port.ToString("00");
            walls.Add(Endpoint(root, $"WallPort:Reception:Port{suffix}"));
            panels.Add(Endpoint(root, $"PatchPanel:Rack01:Port{suffix}"));
            permanent[port - 1] = new ScenarioPermanentCableConnection
            {
                connectionId = $"Infrastructure{port:000}",
                patchPanelEndpointId = panels[port - 1].EndpointId,
                wallportEndpointId = walls[port - 1].EndpointId
            };
        }

        var scenario = ScriptableObject.CreateInstance<TssScenarioDefinition>();
        scenario.permanentConnections = permanent;
        scenario.networkScenario = new NetworkScenarioConfig
        {
            enableLayer23Simulation = true,
            roleBindings = new[]
            {
                new NetworkRoleBinding { role = "AN-1", deviceKey = "Rack:Rack01:U35", category = EquipmentCategory.Switch }
            }
        };
        session.BeginScenario(scenario);
        Assert.AreEqual(4, session.PhysicalConnections.Count);
        var accessSwitch = session.NetworkState.GetOrCreateDevice("Rack:Rack01:U35", EquipmentCategory.Switch, "AN-1");
        accessSwitch.ExpectedRole = "AN-1";
        accessSwitch.Interfaces["Fa0/7"] = new NetworkInterfaceState
        {
            Name = "Fa0/7",
            EndpointId = switchport.EndpointId,
            AdminUp = true,
            CarrierUp = true
        };
        var apEquipment = Equipment(EquipmentCategory.AccessPoint);
        Placements(session).Add(new EndpointPlacementRecord(
            "APPlacement", "ReceptionDesk", apEquipment, "AP-1"));

        try
        {
            for (var port = 0; port < 4; port++)
            {
                ClearPlayerConnections(session);
                PlayerConnections(session).Add(Connection(ap, walls[port]));
                PlayerConnections(session).Add(Connection(panels[port], switchport));
                session.NotifyNetworkStateChanged();
                Assert.IsTrue(session.IsAccessPointUplinkConnected(), $"Port {port + 1} should connect");
            }

            ClearPlayerConnections(session);
            PlayerConnections(session).Add(Connection(ap, walls[0]));
            PlayerConnections(session).Add(Connection(panels[1], switchport));
            session.NotifyNetworkStateChanged();
            Assert.IsFalse(session.IsAccessPointUplinkConnected());

            ClearPlayerConnections(session);
            PlayerConnections(session).Add(Connection(ap, walls[0]));
            session.NotifyNetworkStateChanged();
            Assert.IsFalse(session.IsAccessPointUplinkConnected());

            ClearPlayerConnections(session);
            PlayerConnections(session).Add(Connection(ap, walls[0], TssCableType.Crossover));
            PlayerConnections(session).Add(Connection(panels[0], switchport));
            session.NotifyNetworkStateChanged();
            Assert.IsFalse(session.IsAccessPointUplinkConnected());
        }
        finally
        {
            Object.DestroyImmediate(apEquipment);
            Object.DestroyImmediate(scenario);
            Object.DestroyImmediate(root);
        }
    }

    private static TssPortEndpoint Endpoint(GameObject parent, string id)
    {
        var endpoint = new GameObject(id).AddComponent<TssPortEndpoint>();
        endpoint.transform.SetParent(parent.transform);
        endpoint.Configure(id, new EquipmentInterface { name = "Eth01", label = id, connectorType = "RJ45" });
        return endpoint;
    }

    private static TssPhysicalConnectionRecord Connection(TssPortEndpoint a, TssPortEndpoint b, TssCableType type = TssCableType.StraightThrough)
    {
        return new TssPhysicalConnectionRecord("Player", null, type, a.EndpointId, b.EndpointId, null);
    }

    private static EquipmentDefinition Equipment(EquipmentCategory category)
    {
        var equipment = ScriptableObject.CreateInstance<EquipmentDefinition>();
        equipment.category = category;
        return equipment;
    }

    private static List<EndpointPlacementRecord> Placements(TssTrainingSession session)
    {
        return (List<EndpointPlacementRecord>)typeof(TssTrainingSession)
            .GetField("_endpointPlacements", BindingFlags.Instance | BindingFlags.NonPublic)
            .GetValue(session);
    }

    private static List<TssPhysicalConnectionRecord> PlayerConnections(TssTrainingSession session)
    {
        return (List<TssPhysicalConnectionRecord>)typeof(TssTrainingSession)
            .GetField("_physicalConnections", BindingFlags.Instance | BindingFlags.NonPublic)
            .GetValue(session);
    }

    private static void ClearPlayerConnections(TssTrainingSession session)
    {
        var connections = PlayerConnections(session);
        for (var i = connections.Count - 1; i >= 0; i--)
            if (!connections[i].IsPermanent)
                connections.RemoveAt(i);
    }
}
