using System;
using System.Collections;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;
using Object = UnityEngine.Object;

public sealed class TssLocalLayerLedTests
{
    [TestCase(false)]
    [TestCase(true)]
    public void EndpointLedFollowsLocalLayersWithoutAddressingOrApConfiguration(bool accessPoint)
    {
        var root = new GameObject("Local LED test");
        root.transform.position = Vector3.one * 10000f;
        var session = root.AddComponent<TssTrainingSession>();
        Invoke(session, "Awake");
        var scenario = ScriptableObject.CreateInstance<TssScenarioDefinition>();
        scenario.networkScenario = new NetworkScenarioConfig
        {
            enableLayer23Simulation = true, accessPointSwitchRole = "AN-1", accessPointSwitchPort = "Fa0/7"
        };
        session.BeginScenario(scenario);
        var state = session.NetworkState;
        var device = state.GetOrCreateDevice("LED-Switch", EquipmentCategory.Switch, "AN-1", "AN-1");
        var endpoint = Endpoint(root, "Endpoint:LedDevice:Eth01");
        var peer = Endpoint(root, "LED-Switch:Fa0/7");
        var port = new NetworkInterfaceState
        {
            Name = "Fa0/7", EndpointId = peer.EndpointId, AdminUp = true, CarrierUp = true,
            AccessVlan = state.Profile.ReceptionVlan
        };
        device.Interfaces[port.Name] = port;
        var renderer = new GameObject("LED").AddComponent<MeshRenderer>();
        renderer.transform.SetParent(root.transform, false);
        var material = new Material(Shader.Find("Universal Render Pipeline/Lit") ?? Shader.Find("Standard"));
        renderer.sharedMaterial = material;
        var orange = new Color(1f, 0.45f, 0f, 1f);
        var layer2Color = accessPoint ? new Color(0.1f, 0.55f, 1f, 1f) : new Color(0.1f, 1f, 0.2f, 1f);
        Component presenter;
        Material ledMaterial = null;
        if (accessPoint)
        {
            var ap = root.AddComponent<TssWirelessDevicePresentation>();
            Set(ap, "isAccessPoint", true);
            Set(ap, "statusLedRenderer", renderer);
            ledMaterial = new Material(material);
            Set(ap, "_ledMaterial", ledMaterial);
            ap.OnEndpointsBound(new[] { endpoint }, "Endpoint:LedDevice");
            presenter = ap;
        }
        else
        {
            var desktop = root.AddComponent<TssDeviceLinkIndicator>();
            var bindingType = typeof(TssDeviceLinkIndicator).GetNestedType("LinkIndicatorBinding", BindingFlags.NonPublic);
            var binding = Activator.CreateInstance(bindingType, true);
            bindingType.GetField("endpoint").SetValue(binding, endpoint);
            bindingType.GetField("renderer").SetValue(binding, renderer);
            bindingType.GetField("connectedMaterial").SetValue(binding, material);
            bindingType.GetField("disconnectedMaterial").SetValue(binding, material);
            bindingType.GetField("useLocalLayerColors").SetValue(binding, true);
            var bindings = Array.CreateInstance(bindingType, 1);
            bindings.SetValue(binding, 0);
            Set(desktop, "bindings", bindings);
            presenter = desktop;
        }

        try
        {
            Invoke(presenter, "Refresh");
            AssertOff(renderer, ledMaterial, accessPoint);
            var connections = (IList)Get(session, "_physicalConnections");
            connections.Add(new TssPhysicalConnectionRecord("LedCable", null, TssCableType.StraightThrough,
                endpoint.EndpointId, peer.EndpointId, null));
            session.NotifyNetworkStateChanged();
            Invoke(presenter, "Refresh");
            AssertColor(renderer, ledMaterial, orange);

            device.Vlans[port.AccessVlan] = "Reception";
            session.NotifyNetworkStateChanged();
            Invoke(presenter, "Refresh");
            AssertColor(renderer, ledMaterial, layer2Color);
            Assert.IsFalse(state.AccessPoint.Configured, "AP configuration must not be needed for local LED layers.");

            port.AdminUp = false;
            session.NotifyNetworkStateChanged();
            Invoke(presenter, "Refresh");
            AssertOff(renderer, ledMaterial, accessPoint);
            port.AdminUp = true;
            device.Vlans.Clear();
            session.NotifyNetworkStateChanged();
            Invoke(presenter, "Refresh");
            AssertColor(renderer, ledMaterial, orange);
            connections.Clear();
            session.NotifyNetworkStateChanged();
            Invoke(presenter, "Refresh");
            AssertOff(renderer, ledMaterial, accessPoint);
        }
        finally
        {
            Object.DestroyImmediate(root);
            Object.DestroyImmediate(scenario);
            if (ledMaterial) Object.DestroyImmediate(ledMaterial);
            Object.DestroyImmediate(material);
        }
    }

    private static TssPortEndpoint Endpoint(GameObject parent, string id)
    {
        var endpoint = new GameObject(id).AddComponent<TssPortEndpoint>();
        endpoint.transform.SetParent(parent.transform, false);
        endpoint.Configure(id, null);
        return endpoint;
    }

    private static void AssertColor(Renderer renderer, Material material, Color expected)
    {
        var properties = new MaterialPropertyBlock();
        renderer.GetPropertyBlock(properties);
        var actual = material ? material.GetColor("_EmissionColor") : properties.GetColor("_EmissionColor");
        Assert.That(actual.r, Is.EqualTo(expected.r).Within(0.0001f));
        Assert.That(actual.g, Is.EqualTo(expected.g).Within(0.0001f));
        Assert.That(actual.b, Is.EqualTo(expected.b).Within(0.0001f));
        Assert.That(actual.a, Is.EqualTo(expected.a).Within(0.0001f));
    }

    private static void AssertOff(Renderer renderer, Material material, bool accessPoint)
    {
        if (accessPoint)
            AssertColor(renderer, material, new Color(0.03f, 0.03f, 0.03f, 1f));
        else
        {
            var properties = new MaterialPropertyBlock();
            renderer.GetPropertyBlock(properties);
            Assert.IsTrue(properties.isEmpty);
        }
    }

    private static object Get(object target, string field) => target.GetType().GetField(field, BindingFlags.Instance | BindingFlags.NonPublic).GetValue(target);
    private static void Set(object target, string field, object value) => target.GetType().GetField(field, BindingFlags.Instance | BindingFlags.NonPublic).SetValue(target, value);
    private static void Invoke(object target, string method) => target.GetType().GetMethod(method, BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic).Invoke(target, null);
}
