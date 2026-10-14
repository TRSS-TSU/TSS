using System;
using System.Collections.Generic;
using UnityEngine;

public sealed class TssComputerConnectivityPresenter : MonoBehaviour, ITssEndpointBindingConsumer
{
    [SerializeField] private TssPortEndpoint endpoint;
    [SerializeField] private MonitorTextureRotator monitor;
    [SerializeField] private Texture2D offlineScreen;
    private EndpointPlacementStation _placementStation;

    private TssTrainingSession _session;
    private readonly HashSet<string> _warnedBindings = new(StringComparer.Ordinal);

    private void Awake()
    {
        if (!endpoint)
            endpoint = GetComponentInChildren<TssPortEndpoint>(true);
        if (!monitor)
            monitor = GetComponentInChildren<MonitorTextureRotator>(true);

        _placementStation = FindPlacementStation();
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

        endpoint = ResolveEndpoint();
        var session = TssTrainingSession.Instance;
        if (session && endpoint)
        {
            var connected = session.IsEndpointOnEffectivePhysicalPath(endpoint.EndpointId);
            if (session.NetworkState != null)
            {
                var separator = endpoint.EndpointId.LastIndexOf(':');
                var key = separator > 0 ? endpoint.EndpointId.Substring(0, separator) : endpoint.EndpointId;
                if (session.NetworkState.TryGetEndpoint(key, out var state))
                {
                    var result = new TssNetworkReachabilityEvaluator(session.NetworkState).Evaluate(key, state.Gateway);
                    var layer1 = connected && result.FailedLayer != 1;
                    var layer2 = layer1 && result.FailedLayer != 2;
                    var layer3 = layer2 && result.Success;
                    monitor.SetLayerStates(layer1, layer2, layer3);
                    return;
                }
            }

            monitor.SetLayerStates(connected, connected, false);
            return;
        }

        monitor.SetConnectivityOverride(offlineScreen);
    }

    private TssPortEndpoint ResolveEndpoint()
    {
        if (endpoint || !_placementStation)
            return endpoint;

        foreach (var target in FindObjectsByType<EndpointPlacedEquipmentTarget>(FindObjectsInactive.Include, FindObjectsSortMode.None))
            if (target && target.Station == _placementStation && target.Equipment
                && target.Equipment.category == EquipmentCategory.DesktopPc)
                return target.GetComponentInChildren<TssPortEndpoint>(true);

        return null;
    }

    private EndpointPlacementStation FindPlacementStation()
    {
        // ponytail: colocated desk station binding; use an explicit reference if desk layouts diverge.
        EndpointPlacementStation nearest = null;
        var nearestDistance = 2f;
        foreach (var station in FindObjectsByType<EndpointPlacementStation>(FindObjectsInactive.Include, FindObjectsSortMode.None))
        {
            var distance = Vector3.Distance(transform.position, station.transform.position);
            if (distance < nearestDistance)
            {
                nearest = station;
                nearestDistance = distance;
            }
        }

        return nearest;
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
