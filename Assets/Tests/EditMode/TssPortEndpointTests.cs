using System.Reflection;
using NUnit.Framework;
using UnityEngine;

public sealed class TssPortEndpointTests
{
    [Test]
    public void ConfiguredHighlightRenderersLimitVisualState()
    {
        var root = new GameObject("Endpoint");
        var selected = GameObject.CreatePrimitive(PrimitiveType.Cube);
        var ignored = GameObject.CreatePrimitive(PrimitiveType.Cube);
        try
        {
            selected.transform.SetParent(root.transform, false);
            ignored.transform.SetParent(root.transform, false);
            var endpoint = root.AddComponent<TssPortEndpoint>();
            SetPrivateField(endpoint, "highlightRenderers", new[] { selected.GetComponent<Renderer>() });
            InvokePrivate(endpoint, "CacheMaterials");

            var renderers = (Renderer[])GetPrivateField(endpoint, "_renderers");

            Assert.AreEqual(1, renderers.Length);
            Assert.AreSame(selected.GetComponent<Renderer>(), renderers[0]);
        }
        finally
        {
            Object.DestroyImmediate(root);
            Object.DestroyImmediate(selected);
            Object.DestroyImmediate(ignored);
        }
    }

    [Test]
    public void DisableClearsHoverAndSelectionState()
    {
        var root = GameObject.CreatePrimitive(PrimitiveType.Cube);
        try
        {
            var endpoint = root.AddComponent<TssPortEndpoint>();
            endpoint.SetSelectable(true);
            endpoint.SetSelected(true);
            endpoint.SetHovered(true);

            root.SetActive(false);

            Assert.IsFalse((bool)GetPrivateField(endpoint, "_selectable"));
            Assert.IsFalse((bool)GetPrivateField(endpoint, "_selected"));
            Assert.IsFalse((bool)GetPrivateField(endpoint, "_hovered"));
        }
        finally
        {
            Object.DestroyImmediate(root);
        }
    }

    private static void InvokePrivate(object target, string methodName)
    {
        typeof(TssPortEndpoint).GetMethod(methodName, BindingFlags.Instance | BindingFlags.NonPublic).Invoke(target, null);
    }

    private static object GetPrivateField(object target, string fieldName)
    {
        return typeof(TssPortEndpoint).GetField(fieldName, BindingFlags.Instance | BindingFlags.NonPublic).GetValue(target);
    }

    private static void SetPrivateField(object target, string fieldName, object value)
    {
        typeof(TssPortEndpoint).GetField(fieldName, BindingFlags.Instance | BindingFlags.NonPublic).SetValue(target, value);
    }
}
