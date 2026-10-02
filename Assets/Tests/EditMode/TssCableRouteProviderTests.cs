using NUnit.Framework;
using UnityEngine;

public sealed class TssCableRouteProviderTests
{
    [Test]
    public void RackEndpointsUseRackFrontEntryNodes()
    {
        var root = new GameObject("RouteRoot");
        var switchPort = Endpoint("Cisco 2960 PoE Switch U36", "Rack:Rack01:U36:Fa01", new Vector3(0f, 2f, 0f));
        var patchPort = Endpoint("Patch Panel", "PatchPanel:Rack01:Port01", new Vector3(3f, 1f, 0f));

        try
        {
            var rackEntry = Node("Route_Rack01_FrontUpper", root.transform, new Vector3(1f, 0f, 0f));
            var tray = Node("Route_Rack01_TrayEntry", root.transform, new Vector3(2f, 0f, 0f));
            var provider = root.AddComponent<TssCableRouteProvider>();

            rackEntry.neighbors = new[] { tray };
            tray.neighbors = new[] { rackEntry };

            var route = provider.GetRoute(switchPort, patchPort);

            Assert.AreEqual(6, route.Length);
            Assert.AreEqual(switchPort.CableAnchorPosition, route[0]);
            Assert.AreEqual(new Vector3(rackEntry.transform.position.x, switchPort.CableAnchorPosition.y, rackEntry.transform.position.z), route[1]);
            Assert.AreEqual(rackEntry.transform.position, route[2]);
            Assert.AreEqual(tray.transform.position, route[3]);
            Assert.AreEqual(new Vector3(tray.transform.position.x, patchPort.CableAnchorPosition.y, tray.transform.position.z), route[4]);
            Assert.AreEqual(patchPort.CableAnchorPosition, route[5]);
        }
        finally
        {
            Object.DestroyImmediate(root);
            Object.DestroyImmediate(switchPort.transform.root.gameObject);
            Object.DestroyImmediate(patchPort.transform.root.gameObject);
        }
    }

    [Test]
    public void RearRackEndpointsStayRearBeforeEnteringRackGraph()
    {
        var root = new GameObject("RouteRoot");
        var routerPort = Endpoint("Router U22", "Rack:Rack01:U22:GE00", new Vector3(0f, 2f, 4f));
        var serverPort = Endpoint("Server U5", "Rack:Rack01:U05:GE01", new Vector3(3f, 1f, 4f));

        try
        {
            var rackEntry = Node("Route_Rack01_FrontUpper", root.transform, new Vector3(1f, 0f, 0f));
            var provider = root.AddComponent<TssCableRouteProvider>();

            var route = provider.GetRoute(routerPort, serverPort);

            Assert.AreEqual(7, route.Length);
            Assert.AreEqual(routerPort.CableAnchorPosition, route[0]);
            Assert.AreEqual(new Vector3(rackEntry.transform.position.x, routerPort.CableAnchorPosition.y, routerPort.CableAnchorPosition.z), route[1]);
            Assert.AreEqual(new Vector3(rackEntry.transform.position.x, rackEntry.transform.position.y, routerPort.CableAnchorPosition.z), route[2]);
            Assert.AreEqual(rackEntry.transform.position, route[3]);
            Assert.AreEqual(new Vector3(rackEntry.transform.position.x, rackEntry.transform.position.y, serverPort.CableAnchorPosition.z), route[4]);
            Assert.AreEqual(new Vector3(rackEntry.transform.position.x, serverPort.CableAnchorPosition.y, serverPort.CableAnchorPosition.z), route[5]);
            Assert.AreEqual(serverPort.CableAnchorPosition, route[6]);
        }
        finally
        {
            Object.DestroyImmediate(root);
            Object.DestroyImmediate(routerPort.transform.root.gameObject);
            Object.DestroyImmediate(serverPort.transform.root.gameObject);
        }
    }

    [Test]
    public void RackEndpointWithoutRackNodesStaysOnItsRackSide()
    {
        var root = new GameObject("RouteRoot");
        var patchPort = Endpoint("Patch Panel", "PatchPanel:Rack99:Port01", new Vector3(0f, 2f, 0f));
        var switchPort = Endpoint("Cisco 2960 PoE Switch U36", "Rack:Rack99:U36:Fa01", new Vector3(1f, 1f, 0f));

        try
        {
            Node("Route_Rack01_FrontUpper", root.transform, new Vector3(100f, 0f, 0f));
            var provider = root.AddComponent<TssCableRouteProvider>();

            var route = provider.GetRoute(patchPort, switchPort);

            Assert.AreEqual(5, route.Length);
            Assert.AreEqual(patchPort.CableAnchorPosition, route[0]);
            Assert.AreEqual(new Vector3(0f, 2f, 0.12f), route[1]);
            Assert.AreEqual(new Vector3(0f, 1f, 0.12f), route[2]);
            Assert.AreEqual(new Vector3(1f, 1f, 0.12f), route[3]);
            Assert.AreEqual(switchPort.CableAnchorPosition, route[4]);
        }
        finally
        {
            Object.DestroyImmediate(root);
            Object.DestroyImmediate(patchPort.transform.root.gameObject);
            Object.DestroyImmediate(switchPort.transform.root.gameObject);
        }
    }

    private static TssCableRouteNode Node(string name, Transform parent, Vector3 position)
    {
        var go = new GameObject(name);
        go.transform.SetParent(parent);
        go.transform.position = position;
        return go.AddComponent<TssCableRouteNode>();
    }

    private static TssPortEndpoint Endpoint(string parentName, string endpointId, Vector3 position)
    {
        var parent = new GameObject(parentName);
        var child = new GameObject("Port");
        child.transform.SetParent(parent.transform);
        child.transform.position = position;
        var endpoint = child.AddComponent<TssPortEndpoint>();
        endpoint.Configure(endpointId, new EquipmentInterface { name = "Port" });
        return endpoint;
    }
}
