using System.Collections.Generic;
using UnityEngine;

public enum TssWirelessPresentationState
{
    Offline,
    Online
}

/// <summary>Maps the live training-session connection state onto wireless-device visuals.</summary>
public sealed class TssWirelessDevicePresentation : MonoBehaviour, ITssEndpointBindingConsumer
{
    [SerializeField] private bool isAccessPoint;
    [SerializeField] private Renderer statusLedRenderer;
    [SerializeField] private Renderer screenRenderer;
    [SerializeField] private MonitorTextureRotator monitor;
    [SerializeField] private Texture onlineScreen;
    [SerializeField] private Color onlineLedColor = new(0.1f, 0.55f, 1f, 1f);
    [SerializeField] private Color layer1LedColor = new(1f, 0.45f, 0f, 1f);
    [SerializeField] private Color offlineLedColor = new(0.03f, 0.03f, 0.03f, 1f);

    private readonly List<TssPortEndpoint> _endpoints = new();
    private Material _ledMaterial;
    private Material _screenMaterial;
    private Texture _offlineScreen;

    public TssWirelessPresentationState State { get; private set; }
    public bool IsOnline => State == TssWirelessPresentationState.Online;

    private void Awake()
    {
        if (statusLedRenderer)
            _ledMaterial = statusLedRenderer.material;
        if (screenRenderer)
        {
            _screenMaterial = screenRenderer.material;
            _offlineScreen = GetTexture(_screenMaterial);
        }
    }

    private void OnEnable()
    {
        if (TssTrainingSession.Instance)
            TssTrainingSession.Instance.StateChanged += Refresh;
        Refresh();
    }

    private void OnDisable()
    {
        if (TssTrainingSession.Instance)
            TssTrainingSession.Instance.StateChanged -= Refresh;
    }

    public void OnEndpointsBound(IReadOnlyList<TssPortEndpoint> endpoints, string ownerPrefix)
    {
        _endpoints.Clear();
        if (endpoints != null)
            _endpoints.AddRange(endpoints);
        Refresh();
    }

    public void Refresh()
    {
        var online = isAccessPoint ? IsAccessPointConnected() : RefreshLaptopScreen();
        State = online ? TssWirelessPresentationState.Online : TssWirelessPresentationState.Offline;

        if (_ledMaterial)
        {
            var color = online ? onlineLedColor : offlineLedColor;
            if (isAccessPoint)
            {
                GetAccessPointLinkLayers(out var layer1Up, out var layer2Up);
                color = !layer1Up ? offlineLedColor : layer2Up ? onlineLedColor : layer1LedColor;
            }
            if (_ledMaterial.HasProperty("_BaseColor")) _ledMaterial.SetColor("_BaseColor", color);
            if (_ledMaterial.HasProperty("_EmissionColor")) _ledMaterial.SetColor("_EmissionColor", color);
        }

        if (_screenMaterial)
        {
            SetTexture(_screenMaterial, online && onlineScreen ? onlineScreen : _offlineScreen);
            if (_screenMaterial.HasProperty("_EmissionColor"))
                _screenMaterial.SetColor("_EmissionColor", online ? Color.white : new Color(0.08f, 0.08f, 0.08f, 1f));
        }
    }

    private bool RefreshLaptopScreen()
    {
        var session = TssTrainingSession.Instance;
        var network = session ? session.NetworkState : null;
        var connected = false;
        foreach (var endpoint in _endpoints)
        {
            if (!endpoint)
                continue;

            var separator = endpoint.EndpointId.LastIndexOf(':');
            var key = separator > 0 ? endpoint.EndpointId.Substring(0, separator) : endpoint.EndpointId;
            if (network == null || !network.TryGetEndpoint(key, out var state) || state.Category != EquipmentCategory.Laptop)
                continue;

            var result = new TssNetworkReachabilityEvaluator(network).Evaluate(key, state.Gateway);
            var layer1 = result.FailedLayer != 1;
            var layer2 = layer1 && result.FailedLayer != 2;
            var layer3 = layer2 && result.Success && network.IsWirelessAssociated(key);
            if (monitor)
                monitor.SetLayerStates(layer1, layer2, layer3);
            connected = layer3;
            break;
        }

        if (!connected && monitor)
            monitor.SetLayerStates(false, false, false);
        return connected;
    }

    private bool IsAccessPointConnected()
    {
        var session = TssTrainingSession.Instance;
        if (!session || session.NetworkState == null || !session.NetworkState.AccessPoint.Configured)
            return false;

        foreach (var endpoint in _endpoints)
            if (endpoint && session.IsEndpointOnEffectivePhysicalPath(endpoint.EndpointId))
                return true;
        return false;
    }

    private void GetAccessPointLinkLayers(out bool layer1Up, out bool layer2Up)
    {
        layer1Up = layer2Up = false;
        var session = TssTrainingSession.Instance;
        var network = session ? session.NetworkState : null;
        if (network == null)
            return;

        foreach (var endpoint in _endpoints)
        {
            if (!endpoint || !session.IsEndpointOnEffectivePhysicalPath(endpoint.EndpointId)
                || !network.IsEndpointLogicalLayer1Available(session, endpoint.EndpointId))
                continue;

            layer1Up = true;
            foreach (var path in session.GetEffectivePhysicalPaths())
            {
                var peer = string.Equals(path.EndpointAId, endpoint.EndpointId, System.StringComparison.OrdinalIgnoreCase)
                    ? path.EndpointBId : string.Equals(path.EndpointBId, endpoint.EndpointId, System.StringComparison.OrdinalIgnoreCase)
                        ? path.EndpointAId : null;
                if (peer != null && network.IsAccessPointSwitchPortReady()
                    && network.IsEndpointLogicalLayer2Available(session, peer))
                    layer2Up = true;
            }
        }
    }

    private static Texture GetTexture(Material material) => material && material.HasProperty("_BaseMap") ? material.GetTexture("_BaseMap") : material ? material.mainTexture : null;

    private static void SetTexture(Material material, Texture texture)
    {
        if (!material)
            return;
        if (material.HasProperty("_BaseMap")) material.SetTexture("_BaseMap", texture);
        else material.mainTexture = texture;
    }
}
