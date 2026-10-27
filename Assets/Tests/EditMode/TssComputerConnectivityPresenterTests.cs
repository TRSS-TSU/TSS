using System;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;
using Object = UnityEngine.Object;

public sealed class TssComputerConnectivityPresenterTests
{
    [Test]
    public void WorkstationScreensFollowGatewayThenProviderReachability()
    {
        var sessionObject = new GameObject("ConnectivitySession");
        var session = sessionObject.AddComponent<TssTrainingSession>();
        typeof(TssTrainingSession).GetMethod("Awake", BindingFlags.Instance | BindingFlags.NonPublic).Invoke(session, null);
        var scenario = ScriptableObject.CreateInstance<TssScenarioDefinition>();
        scenario.networkScenario = new NetworkScenarioConfig { enableLayer23Simulation = true };
        session.BeginScenario(scenario);

        var state = session.NetworkState;
        var profile = state.Profile;
        var endpointState = state.GetOrCreateEndpoint("Endpoint:IT1", "IT1");
        endpointState.Address = profile.ManagementNetwork + ".11";
        endpointState.Mask = "255.255.255.0";
        endpointState.Gateway = profile.ManagementNetwork + ".1";
        var accessSwitch = ConfigureLocalTopology(state, endpointState);

        var presenterRoot = new GameObject("Workstation");
        presenterRoot.transform.position = new Vector3(10000f, 10000f, 10000f);
        CreateMonitor("Screen1", presenterRoot.transform, out var monitor, out _, out var monitorTextures1);
        CreateMonitor("Screen2", presenterRoot.transform, out var secondaryMonitor, out _, out var monitorTextures2);
        var endpointObject = new GameObject("Endpoint");
        var endpoint = endpointObject.AddComponent<TssPortEndpoint>();
        endpoint.Configure("Endpoint:IT1:Fa0/2", null);
        var presenter = presenterRoot.AddComponent<TssComputerConnectivityPresenter>();
        BindPresenter(presenter, endpoint, monitor, secondaryMonitor);
        typeof(TssComputerConnectivityPresenter).GetMethod("OnEnable", BindingFlags.Instance | BindingFlags.NonPublic).Invoke(presenter, null);

        try
        {
            endpointState.Layer1Connected = false;
            endpointState.Layer2Connected = false;
            session.NotifyNetworkStateChanged();
            AssertScreens(monitor, secondaryMonitor, monitorTextures1.Layer1Disconnected, monitorTextures2.Layer2Disconnected);

            endpointState.Layer1Connected = true;
            endpointState.Layer2Connected = false;
            accessSwitch.Interfaces["Fa0/2"].AccessVlan = profile.DeadVlan;
            Assert.AreEqual(2, new TssNetworkReachabilityEvaluator(state).Evaluate(endpointState.Key, endpointState.Gateway).FailedLayer);
            session.NotifyNetworkStateChanged();
            AssertScreens(monitor, secondaryMonitor, monitorTextures1.Layer1Connected, monitorTextures2.Layer2Disconnected);

            accessSwitch.Interfaces["Fa0/2"].AccessVlan = profile.ManagementVlan;
            endpointState.Gateway = "192.0.2.1";
            session.NotifyNetworkStateChanged();
            AssertScreens(monitor, secondaryMonitor, monitorTextures1.Layer2Connected, monitorTextures2.Layer3Disconnected);

            endpointState.Gateway = profile.ManagementNetwork + ".1";
            state.SetProviderDemarcConnected(false);
            session.NotifyNetworkStateChanged();
            AssertScreens(monitor, secondaryMonitor, monitorTextures1.Layer3Connected, monitorTextures2.Layer4Disconnected);

            state.TryGetDevice("CN-1", out var router);
            state.SetProviderDemarcConnected(true);
            router.Interfaces["Fa0/1"].Address = "192.0.2.2";
            session.NotifyNetworkStateChanged();
            AssertScreens(monitor, secondaryMonitor, monitorTextures1.Layer3Connected, monitorTextures2.Layer4Disconnected);

            router.Interfaces["Fa0/1"].Address = profile.WanCustomerAddress;
            session.NotifyNetworkStateChanged();
            AssertScreens(monitor, secondaryMonitor, monitorTextures1.Layer3Connected, monitorTextures2.Layer4Disconnected);

            router.Routes.Add(new NetworkRouteState { Network = "0.0.0.0", Mask = "0.0.0.0", NextHop = profile.WanProviderAddress });
            session.NotifyNetworkStateChanged();
            AssertScreens(monitor, secondaryMonitor, monitorTextures1.Layer3Connected, monitorTextures2.Layer4Connected);
        }
        finally
        {
            Object.DestroyImmediate(endpointObject);
            Object.DestroyImmediate(presenterRoot);
            Object.DestroyImmediate(scenario);
            Object.DestroyImmediate(sessionObject);
        }
    }

    private static NetworkDeviceState ConfigureLocalTopology(TssNetworkSessionState state, EndpointNetworkState endpoint)
    {
        var profile = state.Profile;
        var accessSwitch = state.GetOrCreateDevice("AN-1", EquipmentCategory.Switch, "AN-1");
        accessSwitch.Vlans[profile.ManagementVlan] = profile.ManagementVlanName;
        accessSwitch.Interfaces["Fa0/2"] = new NetworkInterfaceState
        {
            Name = "Fa0/2", AdminUp = true, CarrierUp = true, AccessVlan = profile.ManagementVlan
        };
        accessSwitch.Interfaces["Fa0/6"] = new NetworkInterfaceState
        {
            Name = "Fa0/6", AdminUp = true, CarrierUp = true, IsTrunk = true,
            NativeVlan = profile.NativeVlan,
            AllowedVlans = new System.Collections.Generic.HashSet<int>
            {
                profile.ManagementVlan, profile.ReceptionVlan, profile.DataVlan
            }
        };

        var router = state.GetOrCreateDevice("CN-1", EquipmentCategory.Router, "CN-1");
        router.Interfaces["Fa0/0"] = new NetworkInterfaceState
        {
            Name = "Fa0/0", AdminUp = true, CarrierUp = true
        };
        router.Interfaces["Fa0/0." + profile.ManagementVlan] = new NetworkInterfaceState
        {
            Name = "Fa0/0." + profile.ManagementVlan, AdminUp = true, CarrierUp = true,
            Address = profile.ManagementNetwork + ".1", Mask = "255.255.255.0", Dot1qVlan = profile.ManagementVlan
        };
        router.Interfaces["Fa0/1"] = new NetworkInterfaceState
        {
            Name = "Fa0/1", AdminUp = true, CarrierUp = true,
            Address = profile.WanCustomerAddress, Mask = "255.255.255.252"
        };

        endpoint.Layer1Connected = true;
        endpoint.Layer2Connected = true;
        return accessSwitch;
    }

    private static void CreateMonitor(string name, Transform parent, out MonitorTextureRotator monitor, out Renderer renderer, out MonitorTextures textures)
    {
        var root = new GameObject(name);
        root.transform.SetParent(parent, false);
        root.AddComponent<MeshFilter>();
        renderer = root.AddComponent<MeshRenderer>();
        var shader = Shader.Find("Universal Render Pipeline/Lit") ?? Shader.Find("Standard") ?? Shader.Find("Unlit/Texture");
        if (shader) renderer.sharedMaterial = new Material(shader);
        monitor = root.AddComponent<MonitorTextureRotator>();
        textures = new MonitorTextures();
        SetTextureReferences(monitor, renderer, textures);
    }

    private static void SetTextureReferences(MonitorTextureRotator monitor, Renderer renderer, MonitorTextures textures)
    {
        SetPrivateField(monitor, "targetRenderer", renderer);
        SetPrivateField(monitor, "layer1Connected", textures.Layer1Connected);
        SetPrivateField(monitor, "layer1Disconnected", textures.Layer1Disconnected);
        SetPrivateField(monitor, "layer2Connected", textures.Layer2Connected);
        SetPrivateField(monitor, "layer2Disconnected", textures.Layer2Disconnected);
        SetPrivateField(monitor, "layer3Connected", textures.Layer3Connected);
        SetPrivateField(monitor, "layer3Disconnected", textures.Layer3Disconnected);
        SetPrivateField(monitor, "layer4Connected", textures.Layer4Connected);
        SetPrivateField(monitor, "layer4Disconnected", textures.Layer4Disconnected);
    }

    private static void BindPresenter(TssComputerConnectivityPresenter presenter, TssPortEndpoint endpoint, MonitorTextureRotator monitor, MonitorTextureRotator secondaryMonitor)
    {
        SetPrivateField(presenter, "endpoint", endpoint);
        SetPrivateField(presenter, "monitor", monitor);
        SetPrivateField(presenter, "secondaryMonitor", secondaryMonitor);
    }

    private static void SetPrivateField(object target, string fieldName, object value)
    {
        target.GetType().GetField(fieldName, BindingFlags.Instance | BindingFlags.NonPublic).SetValue(target, value);
    }

    private static void AssertScreens(MonitorTextureRotator first, MonitorTextureRotator second, Texture expectedFirst, Texture expectedSecond)
    {
        var firstScreens = (Texture2D[])first.GetType().GetField("screens", BindingFlags.Instance | BindingFlags.NonPublic).GetValue(first);
        var secondScreens = (Texture2D[])second.GetType().GetField("screens", BindingFlags.Instance | BindingFlags.NonPublic).GetValue(second);
        Assert.AreEqual(1, firstScreens.Length);
        Assert.AreEqual(1, secondScreens.Length);
        Assert.AreSame(expectedFirst, firstScreens[0]);
        Assert.AreSame(expectedSecond, secondScreens[0]);
    }

    private sealed class MonitorTextures
    {
        public readonly Texture2D Layer1Connected = NewTexture("Layer1Connected");
        public readonly Texture2D Layer1Disconnected = NewTexture("Layer1Disconnected");
        public readonly Texture2D Layer2Connected = NewTexture("Layer2Connected");
        public readonly Texture2D Layer2Disconnected = NewTexture("Layer2Disconnected");
        public readonly Texture2D Layer3Connected = NewTexture("Layer3Connected");
        public readonly Texture2D Layer3Disconnected = NewTexture("Layer3Disconnected");
        public readonly Texture2D Layer4Connected = NewTexture("Layer4Connected");
        public readonly Texture2D Layer4Disconnected = NewTexture("Layer4Disconnected");

        private static Texture2D NewTexture(string name)
        {
            var texture = new Texture2D(1, 1) { name = name };
            texture.SetPixel(0, 0, Color.white);
            texture.Apply();
            return texture;
        }
    }
}
