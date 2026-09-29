using System.Collections.Generic;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;

public sealed class TssPhysicalConnectionPanelTests
{
    [Test]
    public void ConnectionRowsUseEndpointDisplayLabels()
    {
        var first = new GameObject("First").AddComponent<TssPortEndpoint>();
        var second = new GameObject("Second").AddComponent<TssPortEndpoint>();

        try
        {
            first.Configure("Rack:AN1:GE0", new EquipmentInterface { name = "ge0", label = "GE0/0" });
            second.Configure("Endpoint:Desk1:Eth", new EquipmentInterface { name = "eth", label = "Desk Ethernet" });

            var connection = new TssPhysicalConnectionRecord(
                "Cable001",
                null,
                TssCableType.StraightThrough,
                "Rack:AN1:GE0",
                "Endpoint:Desk1:Eth",
                null);

            Assert.AreEqual(
                "StraightThrough: GE0/0 <-> Desk Ethernet",
                TssPhysicalConnectionPanel.FormatConnectionRow(connection));
        }
        finally
        {
            Object.DestroyImmediate(first.gameObject);
            Object.DestroyImmediate(second.gameObject);
        }
    }

    [Test]
    public void ConnectionRowsAppendInstalledDeviceNames()
    {
        var session = new GameObject("Session").AddComponent<TssTrainingSession>();
        var first = new GameObject("First").AddComponent<TssPortEndpoint>();
        var second = new GameObject("Second").AddComponent<TssPortEndpoint>();

        try
        {
            first.Configure("Rack:Rack01:U03:GE0", new EquipmentInterface { name = "ge0", label = "GE0/0" });
            second.Configure("Endpoint:ITDesk:PC:1:Eth01", new EquipmentInterface { name = "eth", label = "Ethernet" });
            Installed(session).Add(new InstalledEquipmentRecord("Rack01", null, 3, 1, "AN-1"));
            EndpointPlacements(session).Add(new EndpointPlacementRecord("ITDesk:PC:1", "Desk", null, "IT1"));

            var connection = new TssPhysicalConnectionRecord(
                "Cable001",
                null,
                TssCableType.StraightThrough,
                "Rack:Rack01:U03:GE0",
                "Endpoint:ITDesk:PC:1:Eth01",
                null);

            Assert.AreEqual(
                "StraightThrough: AN-1 GE0/0 <-> IT1 Ethernet",
                TssPhysicalConnectionPanel.FormatConnectionRow(connection, session));
        }
        finally
        {
            Object.DestroyImmediate(first.gameObject);
            Object.DestroyImmediate(second.gameObject);
            Object.DestroyImmediate(session.gameObject);
        }
    }

    private static List<InstalledEquipmentRecord> Installed(TssTrainingSession session)
    {
        return (List<InstalledEquipmentRecord>)typeof(TssTrainingSession)
            .GetField("_installed", BindingFlags.Instance | BindingFlags.NonPublic)
            .GetValue(session);
    }

    private static List<EndpointPlacementRecord> EndpointPlacements(TssTrainingSession session)
    {
        return (List<EndpointPlacementRecord>)typeof(TssTrainingSession)
            .GetField("_endpointPlacements", BindingFlags.Instance | BindingFlags.NonPublic)
            .GetValue(session);
    }
}
