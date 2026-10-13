using NUnit.Framework;
using UnityEngine;

public sealed class TssWirelessLobbyTableRouteTests
{
    [Test]
    public void AccessPointUsesLobbyTable01DropBeforeReceptionWallPort()
    {
        var root = new GameObject("RouteRoot");
        var ap = Endpoint("Access Point", "Endpoint:LobbyTable01:EQ_ACCESS_POINT:1:Eth01", new Vector3(10f, 3f, 0f));
        var wall = Endpoint("Reception Wall Port", "WallPort:Reception:Port01", Vector3.zero);

        try
        {
            var drop = Node("LobbyDrop", root.transform, new Vector3(10f, 3f, 0f), "LobbyTable01", TssCableRouteNodeRole.StationDrop);
            var floor = Node("LobbyFloor", root.transform, new Vector3(10f, 0f, 0f), string.Empty, TssCableRouteNodeRole.Tray);
            var reception = Node("Reception", root.transform, Vector3.zero, string.Empty, TssCableRouteNodeRole.Tray);
            Link(drop, floor);
            Link(floor, reception);
            var provider = root.AddComponent<TssCableRouteProvider>();

            var route = provider.GetRoute(ap, wall);

            Assert.Contains(drop.transform.position, route);
            Assert.Contains(floor.transform.position, route);
        }
        finally
        {
            Object.DestroyImmediate(root);
            Object.DestroyImmediate(ap.gameObject);
            Object.DestroyImmediate(wall.gameObject);
        }
    }

    private static TssPortEndpoint Endpoint(string name, string id, Vector3 position)
    {
        var go = new GameObject(name);
        go.transform.position = position;
        var endpoint = go.AddComponent<TssPortEndpoint>();
        endpoint.Configure(id, new EquipmentInterface { name = "Eth01", label = "Ethernet" });
        return endpoint;
    }

    private static TssCableRouteNode Node(string name, Transform parent, Vector3 position, string stationId, TssCableRouteNodeRole role)
    {
        var go = new GameObject(name, typeof(TssCableRouteNode));
        go.transform.SetParent(parent);
        go.transform.position = position;
        var node = go.GetComponent<TssCableRouteNode>();
        node.stationId = stationId;
        node.role = role;
        return node;
    }

    private static void Link(TssCableRouteNode left, TssCableRouteNode right)
    {
        left.neighbors = new[] { right };
        right.neighbors = new[] { left };
    }
}
