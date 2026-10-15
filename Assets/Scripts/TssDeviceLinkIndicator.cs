using System;
using System.Collections.Generic;
using UnityEngine;

public sealed class TssDeviceLinkIndicator : MonoBehaviour, ITssEndpointBindingConsumer
{
    [SerializeField] private LinkIndicatorBinding[] bindings;

    private TssTrainingSession _session;
    private readonly HashSet<string> _warnedBindings = new(StringComparer.Ordinal);
    private bool _hasBeenBound;

    private void OnEnable()
    {
        BindSession();

        Refresh();
    }

    private void Start()
    {
        BindSession();
        Refresh();
    }

    private void OnDisable()
    {
        if (_session)
            _session.StateChanged -= Refresh;

        _session = null;
    }

    private void BindSession()
    {
        var session = TssTrainingSession.Instance;
        if (_session == session)
            return;

        if (_session)
            _session.StateChanged -= Refresh;

        _session = session;
        if (_session)
            _session.StateChanged += Refresh;
    }

    private void Refresh()
    {
        BindSession();
        var session = TssTrainingSession.Instance;
        if (bindings == null)
            return;

        foreach (var binding in bindings)
        {
            if (binding == null || !binding.renderer)
                continue;

            var endpoint = ResolveEndpoint(binding);
            var layer1Up = endpoint && session && session.IsEndpointOnEffectivePhysicalPath(endpoint.EndpointId)
                && session.NetworkState != null && session.NetworkState.IsEndpointLogicalLayer1Available(session, endpoint.EndpointId);
            var layer2Up = layer1Up && session.NetworkState.IsEndpointLogicalLayer2Available(session, endpoint.EndpointId);
            var layer3Up = layer1Up && session.NetworkState.IsEndpointLayer3Operational(endpoint.EndpointId);
            var material = binding.mode == IndicatorMode.Status
                ? layer1Up ? binding.connectedMaterial : binding.disconnectedMaterial
                : binding.mode == IndicatorMode.Protocol
                    ? !layer1Up
                        ? binding.disconnectedMaterial
                        : layer2Up
                            ? binding.connectedMaterial
                            : binding.protocolDownMaterial ? binding.protocolDownMaterial : binding.disconnectedMaterial
                    : binding.mode == IndicatorMode.Layer3
                        ? !layer1Up || !session.NetworkState.IsEndpointIpRoutingAvailable(endpoint.EndpointId)
                            ? binding.disconnectedMaterial
                            : layer3Up
                                ? binding.connectedMaterial
                                : binding.protocolDownMaterial ? binding.protocolDownMaterial : binding.disconnectedMaterial
                    : !layer1Up
                ? binding.disconnectedMaterial
                : layer2Up
                    ? binding.connectedMaterial
                    : binding.protocolDownMaterial ? binding.protocolDownMaterial : binding.disconnectedMaterial;
            if (material)
                binding.renderer.sharedMaterial = material;
        }
    }

    private TssPortEndpoint ResolveEndpoint(LinkIndicatorBinding binding)
    {
        var endpoints = new List<TssPortEndpoint>(GetComponentsInChildren<TssPortEndpoint>(true));
        var endpoint = TssPortEndpointBinder.FindMatchingEndpoint(endpoints, binding.endpoint, binding.endpointId);
        if (_hasBeenBound && !endpoint && !string.IsNullOrWhiteSpace(binding.endpointId) && _warnedBindings.Add(binding.endpointId.Trim()))
            Debug.LogWarning($"{name}: unresolved LED endpoint binding '{binding.endpointId}'.", this);
        return endpoint;
    }

    public void OnEndpointsBound(System.Collections.Generic.IReadOnlyList<TssPortEndpoint> endpoints, string ownerPrefix)
    {
        _hasBeenBound = true;
        Refresh();
    }

    [Serializable]
    private sealed class LinkIndicatorBinding
    {
        public IndicatorMode mode;
        public TssPortEndpoint endpoint;
        public string endpointId;
        public Renderer renderer;
        public Material connectedMaterial;
        public Material protocolDownMaterial;
        public Material disconnectedMaterial;
    }

    private enum IndicatorMode
    {
        Combined,
        Status,
        Protocol,
        Layer3
    }
}
