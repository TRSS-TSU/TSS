using UnityEngine;

public sealed class TssComputerConnectivityPresenter : MonoBehaviour, ITssEndpointBindingConsumer
{
    [SerializeField] private TssPortEndpoint endpoint;
    [SerializeField] private MonitorTextureRotator monitor;
    [SerializeField] private Texture2D offlineScreen;

    private TssTrainingSession _session;

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
        var connected = endpoint && session && session.IsEndpointOnEffectivePhysicalPath(endpoint.EndpointId);
        if (session != null && session.NetworkState != null && endpoint)
        {
            var separator = endpoint.EndpointId.LastIndexOf(':');
            var key = separator > 0 ? endpoint.EndpointId.Substring(0, separator) : endpoint.EndpointId;
            if (session.NetworkState.TryGetEndpoint(key, out var state))
            {
                state.Layer1Connected = connected;
                var result = new TssNetworkReachabilityEvaluator(session.NetworkState).Evaluate(key, state.Gateway);
                var layer1 = result.FailedLayer != 1;
                var layer2 = result.FailedLayer == 0 || result.FailedLayer == 3;
                var layer3 = result.Success;
                state.Layer2Connected = layer2;
                monitor.SetLayerStates(layer1, layer1 && layer2, layer1 && layer2 && layer3);
                return;
            }
        }

        if (connected) monitor.ClearConnectivityOverride(); else monitor.SetConnectivityOverride(offlineScreen);
    }

    public void OnEndpointsBound(System.Collections.Generic.IReadOnlyList<TssPortEndpoint> endpoints, string ownerPrefix)
    {
        if (!endpoint && endpoints != null)
            foreach (var candidate in endpoints)
                if (candidate && !candidate.DisplayLabel.Contains("Console"))
                { endpoint = candidate; break; }
        Refresh();
    }
}
