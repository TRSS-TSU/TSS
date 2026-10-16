using System.Collections.Generic;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;

public sealed class TssAccessPointUplinkTests
{
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
        session.BeginScenario(scenario);
        Assert.AreEqual(4, session.PhysicalConnections.Count);
        Placements(session).Add(new EndpointPlacementRecord(
            "APPlacement", "ReceptionDesk", Equipment(EquipmentCategory.AccessPoint), "AP-1"));

        try
        {
            for (var port = 0; port < 4; port++)
            {
                ClearPlayerConnections(session);
                PlayerConnections(session).Add(Connection(ap, walls[port]));
                PlayerConnections(session).Add(Connection(panels[port], switchport));
                Assert.IsTrue(session.IsAccessPointUplinkConnected(), $"Port {port + 1} should connect");
            }

            ClearPlayerConnections(session);
            PlayerConnections(session).Add(Connection(ap, walls[0]));
            PlayerConnections(session).Add(Connection(panels[1], switchport));
            Assert.IsFalse(session.IsAccessPointUplinkConnected());

            ClearPlayerConnections(session);
            PlayerConnections(session).Add(Connection(ap, walls[0]));
            Assert.IsFalse(session.IsAccessPointUplinkConnected());

            ClearPlayerConnections(session);
            PlayerConnections(session).Add(Connection(ap, walls[0], TssCableType.Crossover));
            PlayerConnections(session).Add(Connection(panels[0], switchport));
            Assert.IsFalse(session.IsAccessPointUplinkConnected());
        }
        finally
        {
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
