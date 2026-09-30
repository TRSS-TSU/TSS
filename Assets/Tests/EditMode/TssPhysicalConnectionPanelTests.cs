using System.Collections.Generic;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.UI;

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

    [Test]
    public void RefreshCreatesScrollableRowsForLargePermanentMaps()
    {
        var session = new GameObject("Session").AddComponent<TssTrainingSession>();
        var panelRoot = new GameObject("Panel", typeof(RectTransform));
        var rows = new GameObject("Rows", typeof(RectTransform));
        rows.transform.SetParent(panelRoot.transform, false);
        rows.GetComponent<RectTransform>().sizeDelta = new Vector2(320f, 100f);

        try
        {
            for (var i = 0; i < 17; i++)
            {
                PhysicalConnections(session).Add(new TssPhysicalConnectionRecord(
                    $"Infrastructure{i + 1:000}",
                    null,
                    TssCableType.StraightThrough,
                    $"PatchPanel:Rack01:Port{i + 1:00}",
                    $"WallPort:Room:Port{i + 1:00}",
                    null,
                    true));
            }

            var panel = panelRoot.AddComponent<TssPhysicalConnectionPanel>();
            typeof(TssPhysicalConnectionPanel)
                .GetField("session", BindingFlags.Instance | BindingFlags.NonPublic)
                .SetValue(panel, session);

            panel.Refresh();

            var content = rows.transform.Find("ScrollContent").GetComponent<RectTransform>();
            Assert.Greater(content.childCount, 17);
            Assert.Greater(content.sizeDelta.y, rows.GetComponent<RectTransform>().rect.height);
        }
        finally
        {
            Object.DestroyImmediate(panelRoot);
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

    private static List<TssPhysicalConnectionRecord> PhysicalConnections(TssTrainingSession session)
    {
        return (List<TssPhysicalConnectionRecord>)typeof(TssTrainingSession)
            .GetField("_physicalConnections", BindingFlags.Instance | BindingFlags.NonPublic)
            .GetValue(session);
    }
}
