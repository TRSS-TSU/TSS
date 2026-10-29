using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;

public sealed class TssCableSplineReadinessTests
{
    [Test]
    public void LaneAllocatorKeepsExistingSlotsStableAndReusesReleasedSlots()
    {
        var allocator = new CableLaneAllocator();
        var first = allocator.Reserve("Cable001", new[] { "A<->B" });
        var second = allocator.Reserve("Cable002", new[] { "A<->B" });

        Assert.AreEqual(0, first.MaximumSlot);
        Assert.AreEqual(1, second.MaximumSlot);

        allocator.Release("Cable001");
        var third = allocator.Reserve("Cable003", new[] { "A<->B" });
        Assert.AreEqual(0, third.MaximumSlot);
        Assert.AreEqual(1, second.MaximumSlot);
    }

    [Test]
    public void CurveBuilderRoundsFiniteRouteAndPreservesExactEndpoints()
    {
        var route = TssCableRouteResult.Valid(
            "A",
            "B",
            TssCableRouteKind.SameRack,
            new List<TssCableRouteNode>(),
            new List<TssCableRouteEdge>(),
            new[] { Vector3.zero, new Vector3(1f, 0f, 0f), new Vector3(1f, 0f, 1f) });
        var reservation = new CableLaneReservation("Cable001", new Dictionary<string, int>(), 0);

        Assert.IsTrue(CableCurveBuilder.TryBuild(route, reservation, CableVisualSettings.Defaults(), out var samples, out var failure), failure);
        Assert.GreaterOrEqual(samples.Length, 2);
        Assert.AreEqual(Vector3.zero, samples[0]);
        Assert.AreEqual(new Vector3(1f, 0f, 1f), samples[samples.Length - 1]);
        Assert.IsFalse(System.Array.Exists(samples, point => !float.IsFinite(point.x) || !float.IsFinite(point.y) || !float.IsFinite(point.z)));
    }

    [Test]
    public void TopologyAuditReportsMissingReverseAndRoleTransition()
    {
        var root = new GameObject("RouteRoot");
        var firstObject = new GameObject("Front");
        var secondObject = new GameObject("Rear");
        firstObject.transform.SetParent(root.transform);
        secondObject.transform.SetParent(root.transform);
        var first = firstObject.AddComponent<TssCableRouteNode>();
        var second = secondObject.AddComponent<TssCableRouteNode>();
        first.role = TssCableRouteNodeRole.RackFront;
        second.role = TssCableRouteNodeRole.RackRear;
        first.neighbors = new[] { second };
        second.neighbors = System.Array.Empty<TssCableRouteNode>();

        try
        {
            var report = TssCableRouteTopologyAudit.Report(new[] { first, second });
            StringAssert.Contains("Missing reverse entries: 1", report);
            StringAssert.Contains("RackFront->RackRear=1", report);
        }
        finally
        {
            Object.DestroyImmediate(root);
        }
    }
}
