using NUnit.Framework;
using UnityEngine;

public sealed class RackCableRoutePlannerTests
{
    [Test]
    public void SameRackAndPatchPanelRoutesUseProfileWithoutAuthoredNodes()
    {
        var rack = new GameObject("Rack");
        var profile = rack.AddComponent<RackCableRouteProfile>();
        profile.Configure("Rack01", -1f, 1f, 0f, 2f, 3f);
        var front = Endpoint("Rack:Rack01:U36:Fa01", new Vector3(-0.8f, 1f, 0f), EquipmentInterfaceFace.Front);
        var patch = Endpoint("PatchPanel:Rack01:Port01", new Vector3(0.8f, 2f, 0f), EquipmentInterfaceFace.Front);

        try
        {
            Assert.IsTrue(RackCableRoutePlanner.TryBuild(front, patch, profile, out var route));
            Assert.AreEqual(TssCableRouteKind.SameRack, route.Kind);
            Assert.IsEmpty(route.OrderedNodes);
            Assert.AreEqual(1, route.Edges.Count);
            Assert.IsTrue(route.Edges[0].Key.Contains("Left:Duct"));
            Assert.AreEqual(new Vector3(-1f, 1f, 0f), route.RenderPoints[1]);
            Assert.AreEqual(new Vector3(-1f, 2f, 0f), route.RenderPoints[2]);
            AssertOrthogonal(route.RenderPoints);
        }
        finally
        {
            Object.DestroyImmediate(rack);
            Object.DestroyImmediate(front.transform.root.gameObject);
            Object.DestroyImmediate(patch.transform.root.gameObject);
        }
    }

    [Test]
    public void FrontToRearRouteUsesTrayAndHasNoGraphDependency()
    {
        var rack = new GameObject("Rack");
        var profile = rack.AddComponent<RackCableRouteProfile>();
        profile.Configure("Rack01", -1f, 1f, 0f, 2f, 3f);
        var front = Endpoint("Rack:Rack01:U36:Fa01", new Vector3(-0.8f, 1f, 0f), EquipmentInterfaceFace.Front);
        var rear = Endpoint("Rack:Rack01:U22:Fa00", new Vector3(-0.7f, 2f, 2f), EquipmentInterfaceFace.Rear);

        try
        {
            Assert.IsTrue(RackCableRoutePlanner.TryBuild(front, rear, profile, out var route));
            Assert.AreEqual(TssCableRouteKind.FrontToRear, route.Kind);
            Assert.AreEqual(3, route.Edges.Count);
            Assert.IsTrue(route.Edges[0].Key.Contains("Left:Duct"));
            var usesTray = false;
            foreach (var point in route.RenderPoints)
                if (Mathf.Abs(point.y - 3f) < 0.0001f)
                    usesTray = true;
            Assert.IsTrue(usesTray);
            AssertOrthogonal(route.RenderPoints);
        }
        finally
        {
            Object.DestroyImmediate(rack);
            Object.DestroyImmediate(front.transform.root.gameObject);
            Object.DestroyImmediate(rear.transform.root.gameObject);
        }
    }

    [Test]
    public void RearToRearTieUsesLeftDuct()
    {
        var rack = new GameObject("Rack");
        var profile = rack.AddComponent<RackCableRouteProfile>();
        profile.Configure("Rack01", -1f, 1f, 0f, 2f, 3f);
        var first = Endpoint("Rack:Rack01:U22:Fa00", new Vector3(0f, 1f, 2f), EquipmentInterfaceFace.Rear);
        var second = Endpoint("Rack:Rack01:U21:Fa00", new Vector3(0f, 2f, 2f), EquipmentInterfaceFace.Rear);

        try
        {
            Assert.IsTrue(RackCableRoutePlanner.TryBuild(first, second, profile, out var route));
            Assert.AreEqual(TssCableRouteKind.SameRack, route.Kind);
            Assert.IsTrue(route.Edges[0].Key.Contains("Rear:Left:Duct"));
            AssertOrthogonal(route.RenderPoints);
        }
        finally
        {
            Object.DestroyImmediate(rack);
            Object.DestroyImmediate(first.transform.root.gameObject);
            Object.DestroyImmediate(second.transform.root.gameObject);
        }
    }

    [Test]
    public void ProviderUsesProfileForSeparateRackAndPatchPanelRoots()
    {
        var root = new GameObject("Rack route provider test");
        var profile = root.AddComponent<RackCableRouteProfile>();
        profile.Configure("RackRouteTest", -1f, 1f, 0f, 2f, 3f);
        var provider = root.AddComponent<TssCableRouteProvider>();
        var equipment = Endpoint("Rack:RackRouteTest:U36:Fa0/15", new Vector3(-0.8f, 1f, 0f), EquipmentInterfaceFace.Front);
        var patch = Endpoint("PatchPanel:RackRouteTest:Port15", new Vector3(0.8f, 2f, 0f), EquipmentInterfaceFace.Front);

        try
        {
            var route = provider.GetRouteResult(equipment, patch);
            Assert.IsTrue(route.IsValid);
            Assert.IsEmpty(route.OrderedNodes);
            Assert.AreEqual(1, route.Edges.Count);
            Assert.IsTrue(route.Edges[0].Key.Contains("Left:Duct"));
            AssertOrthogonal(route.RenderPoints);
        }
        finally
        {
            Object.DestroyImmediate(root);
            Object.DestroyImmediate(equipment.gameObject);
            Object.DestroyImmediate(patch.gameObject);
        }
    }

    private static TssPortEndpoint Endpoint(string endpointId, Vector3 position, EquipmentInterfaceFace face)
    {
        var root = new GameObject(endpointId);
        root.transform.position = position;
        var endpoint = root.AddComponent<TssPortEndpoint>();
        endpoint.Configure(endpointId, new EquipmentInterface { name = endpointId, interfaceFace = face });
        return endpoint;
    }

    private static void AssertOrthogonal(System.Collections.Generic.IReadOnlyList<Vector3> points)
    {
        for (var i = 1; i < points.Count; i++)
        {
            var delta = points[i] - points[i - 1];
            var changedAxes = (Mathf.Abs(delta.x) > 0.0001f ? 1 : 0)
                + (Mathf.Abs(delta.y) > 0.0001f ? 1 : 0)
                + (Mathf.Abs(delta.z) > 0.0001f ? 1 : 0);
            Assert.LessOrEqual(changedAxes, 1, $"Segment {i - 1}->{i} is diagonal.");
        }
    }
}
