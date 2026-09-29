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
}
