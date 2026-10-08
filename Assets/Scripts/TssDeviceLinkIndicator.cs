using System;
using UnityEngine;

public sealed class TssDeviceLinkIndicator : MonoBehaviour, ITssEndpointBindingConsumer
{
    [SerializeField] private LinkIndicatorBinding[] bindings;

    private TssTrainingSession _session;

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
            var connected = endpoint && session && session.IsEndpointOnEffectivePhysicalPath(endpoint.EndpointId);
            var material = connected && binding.connectedMaterial
                ? binding.connectedMaterial
                : binding.disconnectedMaterial;
            if (material)
                binding.renderer.sharedMaterial = material;
        }
    }

    private TssPortEndpoint ResolveEndpoint(LinkIndicatorBinding binding)
    {
        if (binding.endpoint)
            return binding.endpoint;

        if (string.IsNullOrWhiteSpace(binding.endpointId))
            return null;

        foreach (var endpoint in GetComponentsInChildren<TssPortEndpoint>(true))
        {
            if (endpoint.EndpointId == binding.endpointId.Trim() || endpoint.EndpointId.EndsWith(":" + binding.endpointId.Trim(), StringComparison.Ordinal))
                return endpoint;
        }

        return null;
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
