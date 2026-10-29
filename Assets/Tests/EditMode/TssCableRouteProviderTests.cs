using NUnit.Framework;
using UnityEngine;

public sealed class TssCableRouteProviderTests
{
    [Test]
    public void FrontRackEndpointsChooseShortestAuthoredSide()
    {
        var root = new GameObject("RouteRoot");
        var switchPort = Endpoint("Switch", "Rack:Rack01:U36:Fa01", new Vector3(0f, 2f, 0f), EquipmentInterfaceFace.Front);
        var patchPort = Endpoint("Patch Panel", "PatchPanel:Rack01:Port01", new Vector3(3f, 1f, 0f), EquipmentInterfaceFace.Front);

        try
        {
            var left = RackNode("Rack01FrontLeft", root.transform, "Rack01", TssCableRouteNodeRole.RackFront, TssCableRouteSide.Left, new Vector3(-1f, 4f, 0f));
            var right = RackNode("Rack01FrontRight", root.transform, "Rack01", TssCableRouteNodeRole.RackFront, TssCableRouteSide.Right, new Vector3(4f, 4f, 0f));
            Link(left, right);
            var provider = root.AddComponent<TssCableRouteProvider>();

            var route = provider.GetRoute(switchPort, patchPort);

            Assert.AreEqual(left.transform.position, route[3]);
            AssertRouteIsOrthogonal(route);
        }
        finally
        {
            Object.DestroyImmediate(root);
            Object.DestroyImmediate(switchPort.transform.root.gameObject);
            Object.DestroyImmediate(patchPort.transform.root.gameObject);
        }
    }

    [Test]
    public void RearRackEndpointsUseRearNodes()
    {
        var root = new GameObject("RouteRoot");
        var routerPort = Endpoint("Router", "Rack:Rack01:U22:Fa00", new Vector3(0f, 2f, 4f), EquipmentInterfaceFace.Rear);
        var serverPort = Endpoint("Server", "Rack:Rack01:U05:GE01", new Vector3(3f, 1f, 4f), EquipmentInterfaceFace.Rear);

        try
        {
            var rearLeft = RackNode("RearLeft", root.transform, "Rack01", TssCableRouteNodeRole.RackRear, TssCableRouteSide.Left, new Vector3(-1f, 4f, 4f));
            var rearRight = RackNode("RearRight", root.transform, "Rack01", TssCableRouteNodeRole.RackRear, TssCableRouteSide.Right, new Vector3(4f, 4f, 4f));
            Link(rearLeft, rearRight);
            var provider = root.AddComponent<TssCableRouteProvider>();

            var route = provider.GetRoute(routerPort, serverPort);

            Assert.AreEqual(rearLeft.transform.position, route[3]);
            AssertRouteIsOrthogonal(route);
        }
        finally
        {
            Object.DestroyImmediate(root);
            Object.DestroyImmediate(routerPort.transform.root.gameObject);
            Object.DestroyImmediate(serverPort.transform.root.gameObject);
        }
    }

    [Test]
    public void FrontToRearRunsThroughAuthoredTrayPath()
    {
        var root = new GameObject("RouteRoot");
        var switchPort = Endpoint("Switch", "Rack:Rack01:U36:Fa01", new Vector3(0f, 2f, 0f), EquipmentInterfaceFace.Front);
        var routerPort = Endpoint("Router", "Rack:Rack01:U22:Fa00", new Vector3(0f, 1f, 4f), EquipmentInterfaceFace.Rear);

        try
        {
            var frontLeft = RackNode("FrontLeft", root.transform, "Rack01", TssCableRouteNodeRole.RackFront, TssCableRouteSide.Left, new Vector3(-1f, 4f, 0f));
            var trayLeft = RackNode("TrayLeft", root.transform, "Rack01", TssCableRouteNodeRole.Tray, TssCableRouteSide.Left, new Vector3(-1f, 4f, 2f));
            var rearLeft = RackNode("RearLeft", root.transform, "Rack01", TssCableRouteNodeRole.RackRear, TssCableRouteSide.Left, new Vector3(-1f, 4f, 4f));
            Link(frontLeft, trayLeft, rearLeft);
            var provider = root.AddComponent<TssCableRouteProvider>();

            var route = provider.GetRoute(switchPort, routerPort);

            Assert.Contains(trayLeft.transform.position, route);
            AssertRouteIsOrthogonal(route);
        }
        finally
        {
            Object.DestroyImmediate(root);
            Object.DestroyImmediate(switchPort.transform.root.gameObject);
            Object.DestroyImmediate(routerPort.transform.root.gameObject);
        }
    }

    [Test]
    public void PatchPanelToDeviceUsesRackSideThenStationDrop()
    {
        var root = new GameObject("RouteRoot");
        var patchPort = Endpoint("Patch Panel", "PatchPanel:Rack01:Port01", new Vector3(0f, 2f, 0f), EquipmentInterfaceFace.Front);
        var devicePort = Endpoint("Desk", "Endpoint:Desk01:Eth01", new Vector3(5f, 1f, 0f), EquipmentInterfaceFace.Front);

        try
        {
            var rackLeft = RackNode("RackLeft", root.transform, "Rack01", TssCableRouteNodeRole.RackFront, TssCableRouteSide.Left, new Vector3(-1f, 4f, 0f));
            var drop = StationNode("DeskDrop", root.transform, "Desk01", new Vector3(5f, 4f, 0f));
            Link(rackLeft, drop);
            var provider = root.AddComponent<TssCableRouteProvider>();

            var route = provider.GetRoute(patchPort, devicePort);

            Assert.Contains(drop.transform.position, route);
            AssertRouteIsOrthogonal(route);
        }
        finally
        {
            Object.DestroyImmediate(root);
            Object.DestroyImmediate(patchPort.transform.root.gameObject);
            Object.DestroyImmediate(devicePort.transform.root.gameObject);
        }
    }

    [Test]
    public void WallPortAvoidsUnrelatedStationDrop()
    {
        var root = new GameObject("RouteRoot");
        var printerPort = Endpoint("Printer", "Endpoint:ReceptionDesk:EQ_PRINTER:2:Eth01", new Vector3(0f, 1f, 0f), EquipmentInterfaceFace.Front);
        var wallPort = Endpoint("Reception Wall Port", "WallPort:Reception:Port02", new Vector3(5f, 1f, 0f), EquipmentInterfaceFace.Front);

        try
        {
            var printerNode = Node("PrinterNode", root.transform, new Vector3(0f, 1f, 1f));
            printerNode.stationId = "ReceptionDesk";
            var normal = Node("ReceptionTray", root.transform, new Vector3(4f, 1f, 1f));
            var unrelatedDrop = StationNode("UnrelatedDrop", root.transform, "LobbyTable01", new Vector3(5f, 1f, 0f));
            Link(printerNode, normal);
            var provider = root.AddComponent<TssCableRouteProvider>();

            var route = provider.GetRoute(printerPort, wallPort);

            Assert.Contains(normal.transform.position, route);
            Assert.IsFalse(System.Array.Exists(route, point => point == unrelatedDrop.transform.position));
        }
        finally
        {
            Object.DestroyImmediate(root);
            Object.DestroyImmediate(printerPort.transform.root.gameObject);
            Object.DestroyImmediate(wallPort.transform.root.gameObject);
        }
    }

    [Test]
    public void SameRackOppositeSideUsesBothAuthoredEdges()
    {
        var root = new GameObject("RouteRoot");
        var leftPort = Endpoint("Switch Left", "Rack:Rack01:U36:Fa01", new Vector3(-2f, 2f, 0f), EquipmentInterfaceFace.Front);
        var rightPort = Endpoint("Switch Right", "Rack:Rack01:U36:Fa24", new Vector3(5f, 1f, 0f), EquipmentInterfaceFace.Front);

        try
        {
            var left = RackNode("FrontLeft", root.transform, "Rack01", TssCableRouteNodeRole.RackFront, TssCableRouteSide.Left, new Vector3(-1f, 4f, 0f));
            var right = RackNode("FrontRight", root.transform, "Rack01", TssCableRouteNodeRole.RackFront, TssCableRouteSide.Right, new Vector3(4f, 4f, 0f));
            Link(left, right);
            var provider = root.AddComponent<TssCableRouteProvider>();

            var route = provider.GetRoute(leftPort, rightPort);

            Assert.Contains(left.transform.position, route);
            Assert.Contains(right.transform.position, route);
            AssertRouteIsOrthogonal(route);
        }
        finally
        {
            Object.DestroyImmediate(root);
            Object.DestroyImmediate(leftPort.transform.root.gameObject);
            Object.DestroyImmediate(rightPort.transform.root.gameObject);
        }
    }

    [Test]
    public void DifferentRackEndpointsStopAtLocalTrays()
    {
        var root = new GameObject("RouteRoot");
        var rack1Port = Endpoint("Rack1 Switch", "Rack:Rack01:U36:Fa01", new Vector3(0f, 2f, 0f), EquipmentInterfaceFace.Front);
        var rack2Port = Endpoint("Rack2 Switch", "Rack:Rack02:U36:Fa01", new Vector3(10f, 2f, 0f), EquipmentInterfaceFace.Front);

        try
        {
            var rack1Front = RackNode("Rack1Front", root.transform, "Rack01", TssCableRouteNodeRole.RackFront, TssCableRouteSide.Left, new Vector3(-1f, 2f, 0f));
            var rack1Tray = RackNode("Rack1Tray", root.transform, "Rack01", TssCableRouteNodeRole.Tray, TssCableRouteSide.Left, new Vector3(-1f, 4f, 0f));
            var rack2Front = RackNode("Rack2Front", root.transform, "Rack02", TssCableRouteNodeRole.RackFront, TssCableRouteSide.Left, new Vector3(9f, 2f, 0f));
            var rack2Tray = RackNode("Rack2Tray", root.transform, "Rack02", TssCableRouteNodeRole.Tray, TssCableRouteSide.Left, new Vector3(9f, 4f, 0f));
            Link(rack1Front, rack1Tray);
            Link(rack2Front, rack2Tray);
            var provider = root.AddComponent<TssCableRouteProvider>();

            var route = provider.GetRoute(rack1Port, rack2Port);

            Assert.IsTrue(System.Array.Exists(route, IsBreak));
            Assert.Contains(rack1Tray.transform.position, route);
            Assert.Contains(rack2Tray.transform.position, route);
            AssertRouteIsOrthogonal(route);
        }
        finally
        {
            Object.DestroyImmediate(root);
            Object.DestroyImmediate(rack1Port.transform.root.gameObject);
            Object.DestroyImmediate(rack2Port.transform.root.gameObject);
        }
    }

    [Test]
    public void SameRackSwitchToPatchPanelRunsThroughTray()
    {
        var root = new GameObject("RouteRoot");
        var switchPort = Endpoint("Switch", "Rack:Rack01:U36:Fa01", new Vector3(0f, 2f, 0f), EquipmentInterfaceFace.Front);
        var patchPort = Endpoint("Patch Panel", "PatchPanel:Rack01:Port01", new Vector3(0f, 1f, 0f), EquipmentInterfaceFace.Front);

        try
        {
            var edge = RackNode("FrontLeft", root.transform, "Rack01", TssCableRouteNodeRole.RackFront, TssCableRouteSide.Left, new Vector3(-1f, 2f, -1f));
            var tray = RackNode("TrayLeft", root.transform, "Rack01", TssCableRouteNodeRole.Tray, TssCableRouteSide.Left, new Vector3(-1f, 4f, 2f));
            Link(edge, tray);
            var provider = root.AddComponent<TssCableRouteProvider>();

            var route = provider.GetRoute(switchPort, patchPort);

            Assert.IsTrue(System.Array.Exists(route, IsBreak));
            Assert.IsFalse(System.Array.Exists(route, point => point == edge.transform.position));
            Assert.Contains(tray.transform.position, route);
            AssertRouteIsOrthogonal(route);
        }
        finally
        {
            Object.DestroyImmediate(root);
            Object.DestroyImmediate(switchPort.transform.root.gameObject);
            Object.DestroyImmediate(patchPort.transform.root.gameObject);
        }
    }

    [Test]
    public void CyclicRoutePredecessorsAreRejected()
    {
        var root = new GameObject("RouteRoot");

        try
        {
            var first = Node("First", root.transform, Vector3.zero);
            var second = Node("Second", root.transform, Vector3.one);
            var previous = new System.Collections.Generic.Dictionary<TssCableRouteNode, TssCableRouteNode>
            {
                [first] = second,
                [second] = first
            };
            var rebuild = typeof(TssCableRouteProvider).GetMethod("RebuildPath", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Static);

            var route = (System.Collections.Generic.List<TssCableRouteNode>)rebuild.Invoke(null, new object[] { previous, first });

            Assert.IsEmpty(route);
        }
        finally
        {
            Object.DestroyImmediate(root);
        }
    }

    private static TssCableRouteNode RackNode(string name, Transform parent, string rackId, TssCableRouteNodeRole role, TssCableRouteSide side, Vector3 position)
    {
        var node = Node(name, parent, position);
        node.rackId = rackId;
        node.role = role;
        node.side = side;
        return node;
    }

    private static TssCableRouteNode StationNode(string name, Transform parent, string stationId, Vector3 position)
    {
        var node = Node(name, parent, position);
        node.stationId = stationId;
        node.role = TssCableRouteNodeRole.StationDrop;
        return node;
    }

    private static TssCableRouteNode Node(string name, Transform parent, Vector3 position)
    {
        var go = new GameObject(name);
        go.transform.SetParent(parent);
        go.transform.position = position;
        return go.AddComponent<TssCableRouteNode>();
    }

    private static void Link(params TssCableRouteNode[] nodes)
    {
        for (var i = 0; i < nodes.Length; i++)
            nodes[i].neighbors = i == 0 ? new[] { nodes[1] } : i == nodes.Length - 1 ? new[] { nodes[i - 1] } : new[] { nodes[i - 1], nodes[i + 1] };
    }

    private static TssPortEndpoint Endpoint(string parentName, string endpointId, Vector3 position, EquipmentInterfaceFace face)
    {
        var parent = new GameObject(parentName);
        var child = new GameObject("Port");
        child.transform.SetParent(parent.transform);
        child.transform.position = position;
        var endpoint = child.AddComponent<TssPortEndpoint>();
        endpoint.Configure(endpointId, new EquipmentInterface { name = "Port", interfaceFace = face });
        return endpoint;
    }

    private static void AssertRouteIsOrthogonal(Vector3[] route)
    {
        for (var i = 1; i < route.Length; i++)
        {
            var delta = route[i] - route[i - 1];
            if (IsBreak(route[i]) || IsBreak(route[i - 1]))
                continue;

            var changedAxes = (Mathf.Abs(delta.x) > 0.0001f ? 1 : 0)
                + (Mathf.Abs(delta.y) > 0.0001f ? 1 : 0)
                + (Mathf.Abs(delta.z) > 0.0001f ? 1 : 0);
            Assert.LessOrEqual(changedAxes, 1, $"Segment {i - 1}->{i} is diagonal.");
        }
    }

    private static bool IsBreak(Vector3 point)
    {
        return float.IsNaN(point.x) || float.IsNaN(point.y) || float.IsNaN(point.z);
    }
}
