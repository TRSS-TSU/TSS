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
    [SerializeField] private Texture onlineScreen;
    [SerializeField] private Color onlineLedColor = new(0.1f, 0.55f, 1f, 1f);
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
        var online = isAccessPoint ? IsAccessPointConnected() : IsAnyAccessPointConnected();
        State = online ? TssWirelessPresentationState.Online : TssWirelessPresentationState.Offline;

        if (_ledMaterial)
        {
            var color = online ? onlineLedColor : offlineLedColor;
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

    private bool IsAccessPointConnected()
    {
        var session = TssTrainingSession.Instance;
        if (!session || session.NetworkState == null || !session.NetworkState.AccessPoint.Configured)
            return false;

        foreach (var endpoint in _endpoints)
            if (endpoint && session.IsEndpointConnected(endpoint.EndpointId))
                return true;
        return false;
    }

    private bool IsAnyAccessPointConnected()
    {
        foreach (var presentation in FindObjectsByType<TssWirelessDevicePresentation>(FindObjectsInactive.Include, FindObjectsSortMode.None))
            if (presentation && presentation != this && presentation.isAccessPoint && presentation.IsAccessPointConnected())
                return true;
        return false;
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
