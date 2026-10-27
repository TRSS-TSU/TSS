using System.Collections;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using Object = UnityEngine.Object;

public sealed class TssComputerConnectivityPresenterPlayModeTests
{
    [UnityTest]
    public IEnumerator WorkstationScreensRefreshFromStateChanged()
    {
        var sessionObject = new GameObject("ConnectivityPlayModeSession");
        var session = sessionObject.AddComponent<TssTrainingSession>();
        var scenario = ScriptableObject.CreateInstance<TssScenarioDefinition>();
        scenario.networkScenario = new NetworkScenarioConfig { enableLayer23Simulation = true };
        session.BeginScenario(scenario);

        var state = session.NetworkState;
        var profile = state.Profile;
        var endpointState = state.GetOrCreateEndpoint("Endpoint:IT1", "IT1");
        endpointState.Address = profile.ManagementNetwork + ".11";
        endpointState.Mask = "255.255.255.0";
        endpointState.Gateway = profile.ManagementNetwork + ".1";

        var root = new GameObject("ConnectivityPlayModeWorkstation");
        root.transform.position = new Vector3(10000f, 10000f, 10000f);
        var first = CreateMonitor(root.transform, "Screen1", out var firstTexture);
        var second = CreateMonitor(root.transform, "Screen2", out var secondTexture);
        var endpointObject = new GameObject("Endpoint");
        var endpoint = endpointObject.AddComponent<TssPortEndpoint>();
        endpoint.Configure("Endpoint:IT1:Fa0/2", null);
        var presenter = root.AddComponent<TssComputerConnectivityPresenter>();
        SetPrivateField(presenter, "endpoint", endpoint);
        SetPrivateField(presenter, "monitor", first);
        SetPrivateField(presenter, "secondaryMonitor", second);

        try
        {
            yield return null;

            endpointState.Layer1Connected = true;
            endpointState.Layer2Connected = false;
            session.NotifyNetworkStateChanged();
            yield return null;
            AssertScreens(first, second, firstTexture.Layer1Connected, secondTexture.Layer2Disconnected);

            endpointState.Layer2Connected = true;
            endpointState.Gateway = "192.0.2.1";
            session.NotifyNetworkStateChanged();
            yield return null;
            AssertScreens(first, second, firstTexture.Layer2Connected, secondTexture.Layer3Disconnected);
        }
        finally
        {
            Object.Destroy(endpointObject);
            Object.Destroy(root);
            Object.Destroy(scenario);
            Object.Destroy(sessionObject);
        }
    }

    private static MonitorTextureRotator CreateMonitor(Transform parent, string name, out MonitorTextures textures)
    {
        var root = new GameObject(name);
        root.transform.SetParent(parent, false);
        root.AddComponent<MeshFilter>();
        var renderer = root.AddComponent<MeshRenderer>();
        var monitor = root.AddComponent<MonitorTextureRotator>();
        textures = new MonitorTextures();
        SetPrivateField(monitor, "targetRenderer", renderer);
        SetPrivateField(monitor, "layer1Connected", textures.Layer1Connected);
        SetPrivateField(monitor, "layer1Disconnected", textures.Layer1Disconnected);
        SetPrivateField(monitor, "layer2Connected", textures.Layer2Connected);
        SetPrivateField(monitor, "layer2Disconnected", textures.Layer2Disconnected);
        SetPrivateField(monitor, "layer3Connected", textures.Layer3Connected);
        SetPrivateField(monitor, "layer3Disconnected", textures.Layer3Disconnected);
        SetPrivateField(monitor, "layer4Connected", textures.Layer4Connected);
        SetPrivateField(monitor, "layer4Disconnected", textures.Layer4Disconnected);
        return monitor;
    }

    private static void AssertScreens(MonitorTextureRotator first, MonitorTextureRotator second, Texture expectedFirst, Texture expectedSecond)
    {
        var firstScreens = (Texture2D[])first.GetType().GetField("screens", BindingFlags.Instance | BindingFlags.NonPublic).GetValue(first);
        var secondScreens = (Texture2D[])second.GetType().GetField("screens", BindingFlags.Instance | BindingFlags.NonPublic).GetValue(second);
        Assert.AreSame(expectedFirst, firstScreens[0]);
        Assert.AreSame(expectedSecond, secondScreens[0]);
    }

    private static void SetPrivateField(object target, string fieldName, object value)
    {
        target.GetType().GetField(fieldName, BindingFlags.Instance | BindingFlags.NonPublic).SetValue(target, value);
    }

    private sealed class MonitorTextures
    {
        public readonly Texture2D Layer1Connected = NewTexture();
        public readonly Texture2D Layer1Disconnected = NewTexture();
        public readonly Texture2D Layer2Connected = NewTexture();
        public readonly Texture2D Layer2Disconnected = NewTexture();
        public readonly Texture2D Layer3Connected = NewTexture();
        public readonly Texture2D Layer3Disconnected = NewTexture();
        public readonly Texture2D Layer4Connected = NewTexture();
        public readonly Texture2D Layer4Disconnected = NewTexture();

        private static Texture2D NewTexture()
        {
            var texture = new Texture2D(1, 1);
            texture.SetPixel(0, 0, Color.white);
            texture.Apply();
            return texture;
        }
    }
}
