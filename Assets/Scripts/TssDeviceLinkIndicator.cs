using System;
using System.Collections.Generic;
using UnityEngine;

public sealed class TssDeviceLinkIndicator : MonoBehaviour, ITssEndpointBindingConsumer
{
    [SerializeField] private LinkIndicatorBinding[] bindings;

    private TssTrainingSession _session;
    private readonly HashSet<string> _warnedBindings = new(StringComparer.Ordinal);

    private void OnEnable()
    {
        _session = TssTrainingSession.Instance;
        if (_session)
            _session.StateChanged += Refresh;

        Refresh();
    }

    private void OnDisable()
    {
        if (_session)
            _session.StateChanged -= Refresh;
    }

    private void Refresh()
    {
        var session = TssTrainingSession.Instance;
        if (bindings == null)
            return;

        foreach (var binding in bindings)
        {
            if (binding == null || !binding.renderer)
                continue;

            var endpoint = ResolveEndpoint(binding);
            var connected = endpoint && session && session.IsEndpointOnEffectivePhysicalPath(endpoint.EndpointId)
                && session.NetworkState != null && session.NetworkState.IsEndpointLayer1Available(endpoint.EndpointId);
            var material = connected && binding.connectedMaterial
                ? binding.connectedMaterial
                : binding.disconnectedMaterial;
            if (material)
                binding.renderer.sharedMaterial = material;
        }
    }

    private TssPortEndpoint ResolveEndpoint(LinkIndicatorBinding binding)
    {
        var endpoints = new List<TssPortEndpoint>(GetComponentsInChildren<TssPortEndpoint>(true));
        var endpoint = TssPortEndpointBinder.FindMatchingEndpoint(endpoints, binding.endpoint, binding.endpointId);
        if (!endpoint && !string.IsNullOrWhiteSpace(binding.endpointId) && _warnedBindings.Add(binding.endpointId.Trim()))
            Debug.LogWarning($"{name}: unresolved LED endpoint binding '{binding.endpointId}'.", this);
        return endpoint;
    }

    public void OnEndpointsBound(System.Collections.Generic.IReadOnlyList<TssPortEndpoint> endpoints, string ownerPrefix) => Refresh();

    [Serializable]
    private sealed class LinkIndicatorBinding
    {
        public TssPortEndpoint endpoint;
        public string endpointId;
        public Renderer renderer;
        public Material connectedMaterial;
        public Material disconnectedMaterial;
    }
}
