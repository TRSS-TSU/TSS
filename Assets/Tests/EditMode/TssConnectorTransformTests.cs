using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;

public sealed class TssConnectorTransformTests
{
    [Test]
    public void WallportConnectorUsesCableDirectionAndPortRollAndScale()
    {
        var gameObject = new GameObject("Wallport Endpoint Test");
        try
        {
            var endpoint = gameObject.AddComponent<TssPortEndpoint>();
            endpoint.Configure("WallPort:Test:Port01", null);
            gameObject.transform.rotation = Quaternion.Euler(0f, 90f, 0f);
            gameObject.transform.localScale = new Vector3(0.055f, 0.05f, 0.02f);

            var rotation = endpoint.CableConnectorRotation(Vector3.right);
            Assert.That(rotation * Vector3.forward, Is.EqualTo(Vector3.right).Using(Vector3Comparer.Instance));
            Assert.That(rotation * Vector3.up, Is.EqualTo(RollAxis(gameObject.transform.up, Vector3.right)).Using(Vector3Comparer.Instance));
            Assert.That(endpoint.CableConnectorScale(0.012f), Is.EqualTo(gameObject.transform.lossyScale));
        }
        finally
        {
            Object.DestroyImmediate(gameObject);
        }
    }

    [Test]
    public void RackConnectorUsesCableDirectionAndPortRollAndScale()
    {
        var gameObject = new GameObject("Rack Endpoint Test");
        try
        {
            var endpoint = gameObject.AddComponent<TssPortEndpoint>();
            endpoint.Configure("Rack:Rack01:U01:Fa01", null);
            gameObject.transform.rotation = Quaternion.Euler(25f, 40f, 15f);

            var rotation = endpoint.CableConnectorRotation(Vector3.right);
            Assert.That(rotation * Vector3.forward, Is.EqualTo(Vector3.right).Using(Vector3Comparer.Instance));
            Assert.That(rotation * Vector3.up, Is.EqualTo(RollAxis(gameObject.transform.up, Vector3.right)).Using(Vector3Comparer.Instance));
            Assert.That(endpoint.CableConnectorScale(0.012f), Is.EqualTo(Vector3.one * 0.024f));
        }
        finally
        {
            Object.DestroyImmediate(gameObject);
        }
    }

    [Test]
    public void DesktopAndPatchPanelEndpointsUseCableDirectionAndPortRoll()
    {
        var desktopObject = new GameObject("Desktop Endpoint Test");
        var patchPanelObject = new GameObject("Patch Panel Endpoint Test");
        try
        {
            var desktop = desktopObject.AddComponent<TssPortEndpoint>();
            desktop.Configure("Endpoint:ITDesk:EQ_DESKTOP_PC:1:Eth01", null);
            desktopObject.transform.rotation = Quaternion.Euler(0f, 90f, 0f);

            var patchPanel = patchPanelObject.AddComponent<TssPortEndpoint>();
            patchPanel.Configure("PatchPanel:Rack01:Port18", null);
            patchPanelObject.transform.rotation = Quaternion.Euler(90f, 0f, 0f);

            AssertRouteFrame(desktop, desktopObject.transform, Vector3.right);
            AssertRouteFrame(patchPanel, patchPanelObject.transform, Vector3.right);
        }
        finally
        {
            Object.DestroyImmediate(desktopObject);
            Object.DestroyImmediate(patchPanelObject);
        }
    }

    [Test]
    public void ZeroRouteDirectionStillProducesValidRotation()
    {
        var gameObject = new GameObject("Fallback Endpoint Test");
        try
        {
            var endpoint = gameObject.AddComponent<TssPortEndpoint>();
            endpoint.Configure("Endpoint:Fallback", null);

            var rotation = endpoint.CableConnectorRotation(Vector3.zero);

            Assert.That(rotation * Vector3.forward, Is.EqualTo(gameObject.transform.forward).Using(Vector3Comparer.Instance));
            Assert.That(rotation * Vector3.up, Is.EqualTo(gameObject.transform.up).Using(Vector3Comparer.Instance));
        }
        finally
        {
            Object.DestroyImmediate(gameObject);
        }
    }

    private static void AssertRouteFrame(TssPortEndpoint endpoint, Transform expected, Vector3 routeDirection)
    {
        var rotation = endpoint.CableConnectorRotation(routeDirection);
        Assert.That(rotation * Vector3.forward, Is.EqualTo(routeDirection).Using(Vector3Comparer.Instance));
        Assert.That(rotation * Vector3.up, Is.EqualTo(RollAxis(expected.up, routeDirection)).Using(Vector3Comparer.Instance));
    }

    private static Vector3 RollAxis(Vector3 up, Vector3 forward)
    {
        return Vector3.ProjectOnPlane(up, forward).normalized;
    }

    private sealed class Vector3Comparer : IEqualityComparer<Vector3>
    {
        public static readonly Vector3Comparer Instance = new();

        public bool Equals(Vector3 x, Vector3 y) => Vector3.Distance(x, y) <= 0.0001f;
        public int GetHashCode(Vector3 value) => value.GetHashCode();
    }
}
