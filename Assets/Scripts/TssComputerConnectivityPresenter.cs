using System;
using System.Collections.Generic;
using UnityEngine;

public sealed class TssComputerConnectivityPresenter : MonoBehaviour, ITssEndpointBindingConsumer
{
    [SerializeField] private TssPortEndpoint endpoint;
    [SerializeField] private MonitorTextureRotator monitor;
    [SerializeField] private MonitorTextureRotator secondaryMonitor;
    private EndpointPlacementStation _placementStation;

    private TssTrainingSession _session;
    private readonly HashSet<string> _warnedBindings = new(StringComparer.Ordinal);

    private void Awake()
    {
        if (!endpoint)
            endpoint = GetComponentInChildren<TssPortEndpoint>(true);
        if (!monitor)
            monitor = GetComponentInChildren<MonitorTextureRotator>(true);
        if (!secondaryMonitor)
            foreach (var candidate in GetComponentsInChildren<MonitorTextureRotator>(true))
                if (candidate && candidate != monitor)
                {
                    secondaryMonitor = candidate;
                    break;
                }

        _placementStation = FindPlacementStation();
    }

    private void OnEnable()
    {
        _session = TssTrainingSession.Instance;
        if (!_session)
            _session = FindFirstObjectByType<TssTrainingSession>();

        if (_session)
            _session.StateChanged += Refresh;

        Refresh();
    }

    private void Start()
    {
        // Scene OnEnable can run before the training session's Awake assigns Instance.
        if (!_session)
        {
            _session = TssTrainingSession.Instance;
            if (!_session)
                _session = FindFirstObjectByType<TssTrainingSession>();
            if (_session)
                _session.StateChanged += Refresh;
        }

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
        var session = TssTrainingSession.Instance ? TssTrainingSession.Instance : _session;
        if (session && endpoint)
        {
            var connected = session.IsEndpointOnEffectivePhysicalPath(endpoint.EndpointId);
            if (session.NetworkState != null)
            {
                var layer1 = connected && session.NetworkState.IsEndpointLogicalLayer1Available(session, endpoint.EndpointId);
                var layer2 = layer1 && session.NetworkState.IsEndpointLogicalLayer2Available(session, endpoint.EndpointId);
                var separator = endpoint.EndpointId.LastIndexOf(':');
                var key = separator > 0 ? endpoint.EndpointId.Substring(0, separator) : endpoint.EndpointId;
                if (session.NetworkState.TryGetEndpoint(key, out var state))
                {
                    var result = new TssNetworkReachabilityEvaluator(session.NetworkState).Evaluate(key, state.Gateway);
                    var layer3 = layer2 && session.NetworkState.IsEndpointLayer3Operational(endpoint.EndpointId);
                    var wanReachable = layer3 && result.Success;
                    ApplyLayerStates(layer1, layer2, layer3, wanReachable);
                    return;
                }

                ApplyLayerStates(layer1, layer2, false, false);
                return;
            }

            ApplyLayerStates(connected, connected, false, false);
            return;
        }

        if (monitor)
            monitor.SetPowerOff();
        if (secondaryMonitor && secondaryMonitor != monitor)
            secondaryMonitor.SetPowerOff();
    }

    private void ApplyLayerStates(bool layer1, bool layer2, bool layer3, bool wanReachable)
    {
        if (!monitor)
            return;

        if (!layer1)
        {
            monitor.SetLayerState(1, false);
            if (secondaryMonitor && secondaryMonitor != monitor)
                secondaryMonitor.SetLayerState(2, false);
            return;
        }

        if (!layer2)
        {
            monitor.SetLayerState(1, true);
            if (secondaryMonitor && secondaryMonitor != monitor)
                secondaryMonitor.SetLayerState(2, false);
            return;
        }

        if (!layer3)
        {
            monitor.SetLayerState(2, true);
            if (secondaryMonitor && secondaryMonitor != monitor)
                secondaryMonitor.SetLayerState(3, false);
            return;
        }

        monitor.SetLayerState(3, true);
        if (secondaryMonitor && secondaryMonitor != monitor)
            secondaryMonitor.SetLayerState(4, wanReachable);
    }

    private TssPortEndpoint ResolveEndpoint()
    {
        if (!_placementStation)
            return endpoint;

        foreach (var target in FindObjectsByType<EndpointPlacedEquipmentTarget>(FindObjectsInactive.Exclude, FindObjectsSortMode.None))
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
