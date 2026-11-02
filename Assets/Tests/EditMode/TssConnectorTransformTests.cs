using NUnit.Framework;
using UnityEngine;

public sealed class TssConnectorTransformTests
{
    [Test]
    public void WallportConnectorUsesPortTransformDirectionAndScale()
    {
        var gameObject = new GameObject("Wallport Endpoint Test");
        try
        {
            var endpoint = gameObject.AddComponent<TssPortEndpoint>();
            endpoint.Configure("WallPort:Test:Port01", null);
            gameObject.transform.rotation = Quaternion.Euler(0f, 90f, 0f);
            gameObject.transform.localScale = new Vector3(0.055f, 0.05f, 0.02f);

            Assert.That(endpoint.CableConnectorDirection(Vector3.right), Is.EqualTo(gameObject.transform.forward));
            Assert.That(endpoint.CableConnectorScale(0.012f), Is.EqualTo(gameObject.transform.lossyScale));
        }
        finally
        {
            Object.DestroyImmediate(gameObject);
        }
    }

    [Test]
    public void RackConnectorKeepsFrontRearConventionAndCableScale()
    {
        var gameObject = new GameObject("Rack Endpoint Test");
        try
        {
            var endpoint = gameObject.AddComponent<TssPortEndpoint>();
            endpoint.Configure("Rack:Rack01:U01:Fa01", null);

            Assert.That(endpoint.CableConnectorDirection(Vector3.right), Is.EqualTo(Vector3.back));
            Assert.That(endpoint.CableConnectorScale(0.012f), Is.EqualTo(Vector3.one * 0.024f));
        }
        finally
        {
            Object.DestroyImmediate(gameObject);
        }
    }
}
