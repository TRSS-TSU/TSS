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
            SetPanelContent(panel, "PermanentInfrastructure");

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

    [Test]
    public void RefreshShowsEffectiveDevicePathsThroughPermanentInfrastructure()
    {
        var session = new GameObject("Session").AddComponent<TssTrainingSession>();
        var patch = new GameObject("Patch").AddComponent<TssPortEndpoint>();
        var wall = new GameObject("Wall").AddComponent<TssPortEndpoint>();
        var switchPort = new GameObject("Switch").AddComponent<TssPortEndpoint>();
        var printer = new GameObject("Printer").AddComponent<TssPortEndpoint>();
        var panelRoot = new GameObject("Panel", typeof(RectTransform));
        var rows = new GameObject("Rows", typeof(RectTransform));
        rows.transform.SetParent(panelRoot.transform, false);

        try
        {
            patch.Configure("PatchPanel:Rack02:Port06", new EquipmentInterface { label = "Patch2 Port 6" });
            wall.Configure("WallPort:SecondFloor:Office02:Port02", new EquipmentInterface { label = "Office 2 Wallport Port 2" });
            switchPort.Configure("Rack:Rack02:U35:Port04", new EquipmentInterface { label = "AN-2 Port 4" });
            printer.Configure("Endpoint:Office2Printer:Eth01", new EquipmentInterface { label = "Office 2 Printer Eth01" });
            PhysicalConnections(session).Add(new TssPhysicalConnectionRecord("Infrastructure001", null, TssCableType.StraightThrough, patch.EndpointId, wall.EndpointId, null, true));
            PhysicalConnections(session).Add(new TssPhysicalConnectionRecord("Cable001", null, TssCableType.StraightThrough, patch.EndpointId, switchPort.EndpointId, null));
            PhysicalConnections(session).Add(new TssPhysicalConnectionRecord("Cable002", null, TssCableType.StraightThrough, wall.EndpointId, printer.EndpointId, null));

            var panel = panelRoot.AddComponent<TssPhysicalConnectionPanel>();
            typeof(TssPhysicalConnectionPanel)
                .GetField("session", BindingFlags.Instance | BindingFlags.NonPublic)
                .SetValue(panel, session);
            SetPanelContent(panel, "EffectiveDevicePaths");

            panel.Refresh();

            var content = rows.transform.Find("ScrollContent");
            Assert.IsTrue(HasRow(content, "Effective Device Paths"));
            Assert.IsTrue(HasRowContaining(content, "AN-2 Port 4", "Office 2 Printer Eth01"));
        }
        finally
        {
            Object.DestroyImmediate(panelRoot);
            Object.DestroyImmediate(patch.gameObject);
            Object.DestroyImmediate(wall.gameObject);
            Object.DestroyImmediate(switchPort.gameObject);
            Object.DestroyImmediate(printer.gameObject);
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

    private static void SetPanelContent(TssPhysicalConnectionPanel panel, string value)
    {
        var field = typeof(TssPhysicalConnectionPanel)
            .GetField("content", BindingFlags.Instance | BindingFlags.NonPublic);
        field.SetValue(panel, System.Enum.Parse(field.FieldType, value));
    }

    private static bool HasRow(Transform content, string text)
    {
        for (var i = 0; i < content.childCount; i++)
        {
            if (content.GetChild(i).GetComponent<Text>().text == text)
                return true;
        }

        return false;
    }

    private static bool HasRowContaining(Transform content, string first, string second)
    {
        for (var i = 0; i < content.childCount; i++)
        {
            var text = content.GetChild(i).GetComponent<Text>().text;
            if (text.Contains(first) && text.Contains(second))
                return true;
        }

        return false;
    }
}
