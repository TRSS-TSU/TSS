using System;
using System.Collections.Generic;
using UnityEngine;

public sealed class TssComputerConnectivityPresenter : MonoBehaviour, ITssEndpointBindingConsumer
{
    [SerializeField] private TssPortEndpoint endpoint;
    [SerializeField] private MonitorTextureRotator monitor;
    [SerializeField] private Texture2D offlineScreen;

    private TssTrainingSession _session;
    private readonly HashSet<string> _warnedBindings = new(StringComparer.Ordinal);

    private void Awake()
    {
        if (!endpoint)
            endpoint = GetComponentInChildren<TssPortEndpoint>(true);
        if (!monitor)
            monitor = GetComponentInChildren<MonitorTextureRotator>(true);
    }

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
        if (!monitor)
            return;

        var session = TssTrainingSession.Instance;
        if (session != null && session.NetworkState != null && endpoint)
        {
            var separator = endpoint.EndpointId.LastIndexOf(':');
            var key = separator > 0 ? endpoint.EndpointId.Substring(0, separator) : endpoint.EndpointId;
            if (session.NetworkState.TryGetEndpoint(key, out var state))
            {
                var result = new TssNetworkReachabilityEvaluator(session.NetworkState).Evaluate(key, state.Gateway);
                var layer1 = result.FailedLayer != 1;
                var layer2 = layer1 && result.FailedLayer != 2;
                var layer3 = layer2 && result.Success;
                monitor.SetLayerStates(layer1, layer2, layer3);
                return;
            }
        }

        if (endpoint && session && session.IsEndpointOnEffectivePhysicalPath(endpoint.EndpointId))
            monitor.ClearConnectivityOverride();
        else
            monitor.SetConnectivityOverride(offlineScreen);
    }

    public void OnEndpointsBound(System.Collections.Generic.IReadOnlyList<TssPortEndpoint> endpoints, string ownerPrefix)
    {
        endpoint = TssPortEndpointBinder.FindMatchingEndpoint(endpoints, endpoint, string.Empty);
        if (!endpoint)
            foreach (var candidate in endpoints ?? Array.Empty<TssPortEndpoint>())
                if (candidate && !candidate.DisplayLabel.Contains("Console", StringComparison.OrdinalIgnoreCase))
                {
                    endpoint = candidate;
                    break;
                }
        if (!endpoint && !string.IsNullOrWhiteSpace(ownerPrefix) && _warnedBindings.Add(ownerPrefix))
            Debug.LogWarning($"{name}: no data endpoint resolved after binding.", this);
        Refresh();
    }
}
