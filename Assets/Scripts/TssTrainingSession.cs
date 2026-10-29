using System;
using System.Linq;
using System.Collections.Generic;
using UnityEngine;

public sealed class TssTrainingSession : MonoBehaviour
{
    private const string ProviderDemarcEndpointId = "Demarc:Provider:Port01";
    public static TssTrainingSession Instance { get; private set; }

    [SerializeField] private TssScenarioDefinition fallbackScenario;
    [SerializeField] private Transform carryAnchor;
    [SerializeField] private GameObject scenarioMenuHost;
    [SerializeField] private GameObject endpointConnectorPrefab;

    private readonly Dictionary<EquipmentDefinition, int> _remaining = new();
    private readonly Dictionary<TssCableDefinition, int> _remainingCables = new();
    private readonly List<InstalledEquipmentRecord> _installed = new();
    private readonly List<EndpointPlacementRecord> _endpointPlacements = new();
    private readonly List<TssPhysicalConnectionRecord> _physicalConnections = new();
    private readonly Dictionary<string, TssTerminationModuleState> _terminationStates = new();
    private readonly Dictionary<string, bool> _terminationResults = new();
    private readonly CableLaneAllocator _cableLaneAllocator = new();
    private GameObject _carryVisual;
    private TssPortEndpoint _selectedCableEndpoint;
    private int _nextConnectionNumber = 1;

    public event Action StateChanged;
    public TssScenarioDefinition Scenario { get; private set; }
    public EquipmentDefinition HeldItem { get; private set; }
    public TssCableDefinition HeldCable { get; private set; }
    public TssPortEndpoint SelectedCableEndpoint => _selectedCableEndpoint;
    public GameObject CarryVisual => _carryVisual;
    public IReadOnlyList<InstalledEquipmentRecord> Installed => _installed;
    public IReadOnlyList<EndpointPlacementRecord> EndpointPlacements => _endpointPlacements;
    public IReadOnlyList<TssPhysicalConnectionRecord> PhysicalConnections => _physicalConnections;
    public TssNetworkRunProfile NetworkProfile { get; private set; }
    public TssNetworkSessionState NetworkState { get; private set; }
    public string StudentId { get; private set; }

    public void NotifyStateChanged() => StateChanged?.Invoke();

    private void Awake()
    {
        Instance = this;
    }

    private void Start()
    {
        var selected = TssScenarioLaunchState.ConsumeSelectedScenario(out var studentId);
        if (selected)
        {
            if (scenarioMenuHost)
                scenarioMenuHost.SetActive(false);
            BeginScenario(selected, studentId);
        }
        else if (fallbackScenario && scenarioMenuHost)
        {
            scenarioMenuHost.SetActive(true);
        }
        else if (fallbackScenario)
        {
            BeginScenario(fallbackScenario);
        }
    }

    public void BeginScenario(TssScenarioDefinition scenario, string studentId = null)
    {
        if (Scenario != scenario)
            TssScenarioLaunchState.ReleaseRuntime(Scenario);
        Scenario = scenario;
        StudentId = studentId ?? string.Empty;
        HeldItem = null;
        HeldCable = null;
        _selectedCableEndpoint = null;
        _nextConnectionNumber = 1;
        _remaining.Clear();
        _remainingCables.Clear();
        _installed.Clear();
        _endpointPlacements.Clear();
        _terminationStates.Clear();
        _terminationResults.Clear();
        ClearPhysicalConnections();
        ClearCarryVisual();
        var profileSeed = string.IsNullOrWhiteSpace(StudentId)
            ? scenario?.networkScenario?.profileSeed ?? 0
            : TssNetworkRunProfile.DeriveSeed(StudentId, scenario?.scenarioId);
        NetworkProfile = scenario && scenario.networkScenario != null && scenario.networkScenario.enableLayer23Simulation
            ? TssNetworkRunProfile.Create(profileSeed, scenario.networkScenario, StudentId)
            : null;
        NetworkState = NetworkProfile != null ? new TssNetworkSessionState(NetworkProfile) : null;

        if (scenario && scenario.inventory != null)
        {
            foreach (var item in scenario.inventory)
            {
                if (item != null && item.equipment)
                    _remaining[item.equipment] = Mathf.Max(0, item.quantity);
            }
        }

        if (scenario && scenario.cableInventory != null)
        {
            foreach (var item in scenario.cableInventory)
            {
                if (item != null && item.cable)
                    _remainingCables[item.cable] = Mathf.Max(0, item.quantity);
            }
        }

        ApplyPermanentConnections();
        StateChanged?.Invoke();
    }

    public void NotifyNetworkStateChanged()
    {
        var effectiveEndpoints = new HashSet<string>();
        foreach (var path in GetEffectivePhysicalPaths())
        {
            effectiveEndpoints.Add(path.EndpointAId);
            effectiveEndpoints.Add(path.EndpointBId);
        }

        RefreshInterfaceCarriers(effectiveEndpoints);
        RefreshPlacedDesktopConnectivity(effectiveEndpoints);
        StateChanged?.Invoke();
    }

    private void OnDestroy()
    {
        TssScenarioLaunchState.ReleaseRuntime(Scenario);
        if (Instance == this) Instance = null;
    }

    private void RefreshPlacedDesktopConnectivity(HashSet<string> effectiveEndpoints)
    {
        if (NetworkState == null)
            return;

        var endpoints = FindObjectsByType<TssPortEndpoint>(FindObjectsInactive.Include, FindObjectsSortMode.None);
        foreach (var placement in _endpointPlacements)
        {
            if (!placement.Equipment || placement.Equipment.category != EquipmentCategory.DesktopPc)
                continue;

            var key = GetEndpointOwnerPrefix(placement.PlacementId);
            if (!NetworkState.TryGetEndpoint(key, out var state))
                continue;

            var prefix = key + ":";
            foreach (var endpoint in endpoints)
            {
                if (!endpoint || !endpoint.EndpointId.StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
                    continue;

                state.Layer1Connected = effectiveEndpoints.Contains(endpoint.EndpointId);
                state.Layer2Connected = state.Layer1Connected;
                break;
            }
        }
    }

    private void RefreshInterfaceCarriers(HashSet<string> effectiveEndpoints)
    {
        if (NetworkState == null)
            return;

        foreach (var device in NetworkState.Devices)
        foreach (var iface in device.Interfaces.Values)
        {
            if (device.Category == EquipmentCategory.Router && iface.Name.Contains('.'))
                continue;
            if (string.IsNullOrWhiteSpace(iface.EndpointId)) continue;

            iface.CarrierUp = device.Category == EquipmentCategory.Router && iface.Name == "Fa0/1"
                ? IsProviderDemarcConnected(iface.EndpointId)
                : effectiveEndpoints.Contains(iface.EndpointId);
        }

        foreach (var device in NetworkState.Devices)
        {
            if (device.Category != EquipmentCategory.Router) continue;
            foreach (var iface in device.Interfaces.Values)
            {
                var separator = iface.Name.IndexOf('.');
                if (separator > 0 && device.Interfaces.TryGetValue(iface.Name.Substring(0, separator), out var parent))
                    iface.CarrierUp = parent.CarrierUp;
            }
        }
    }

    public IEnumerable<ScenarioInventoryItem> GetScenarioInventory()
    {
        return Scenario && Scenario.inventory != null ? Scenario.inventory : Array.Empty<ScenarioInventoryItem>();
    }

    public IEnumerable<ScenarioCableInventoryItem> GetScenarioCableInventory()
    {
        return Scenario && Scenario.cableInventory != null ? Scenario.cableInventory : Array.Empty<ScenarioCableInventoryItem>();
    }

    public int GetRemaining(EquipmentDefinition equipment)
    {
        return equipment && _remaining.TryGetValue(equipment, out var count) ? count : 0;
    }

    public int GetRemaining(TssCableDefinition cable)
    {
        return cable && _remainingCables.TryGetValue(cable, out var count) ? count : 0;
    }

    public bool TryCheckout(EquipmentDefinition equipment, out string message)
    {
        if (!Scenario)
        {
            message = "Select a scenario first.";
            return false;
        }

        if (HeldItem || HeldCable)
        {
            message = "Return the carried item first.";
            return false;
        }

        if (!equipment || GetRemaining(equipment) <= 0)
        {
            message = "None remaining.";
            return false;
        }

        _remaining[equipment]--;
        HeldItem = equipment;
        CreateCarryVisual(equipment);
        message = $"Carrying {equipment.displayName}";
        StateChanged?.Invoke();
        return true;
    }

    public bool TryCheckoutCable(TssCableDefinition cable, out string message, bool notifyStateChanged = true)
    {
        if (!Scenario)
        {
            message = "Select a scenario first.";
            return false;
        }

        if (HeldItem || HeldCable)
        {
            message = "Return the carried item first.";
            return false;
        }

        if (!cable || GetRemaining(cable) <= 0)
        {
            message = "No cables remaining.";
            return false;
        }

        _remainingCables[cable]--;
        HeldCable = cable;
        _selectedCableEndpoint = null;
        CreateCableCarryVisual(cable);
        message = $"Carrying {cable.displayName}";
        if (notifyStateChanged)
            StateChanged?.Invoke();
        return true;
    }

    public bool ReturnHeldItem(out string message)
    {
        if (!HeldItem)
        {
            message = "No item to return.";
            return false;
        }

        _remaining[HeldItem] = GetRemaining(HeldItem) + 1;
        message = $"Returned {HeldItem.displayName}";
        HeldItem = null;
        ClearCarryVisual();
        StateChanged?.Invoke();
        return true;
    }

    public bool ReturnHeldCable(out string message)
    {
        if (!HeldCable)
        {
            message = "No cable to return.";
            return false;
        }

        _remainingCables[HeldCable] = GetRemaining(HeldCable) + 1;
        message = $"Returned {HeldCable.displayName}";
        HeldCable = null;
        _selectedCableEndpoint = null;
        ClearCarryVisual();
        StateChanged?.Invoke();
        return true;
    }

    public bool TryInstallHeld(RackMountController rack, int startingU, string deviceName, out string message)
    {
        if (!HeldItem)
        {
            message = "Pick up equipment first.";
            return false;
        }

        var equipment = HeldItem;
        if (!rack.TryInstall(equipment, startingU, out message))
            return false;

        _installed.Add(new InstalledEquipmentRecord(rack.RackId, equipment, startingU, equipment.rackUnits, CleanDeviceName(deviceName)));
        HeldItem = null;
        ClearCarryVisual();
        ApplyPermanentConnections();
        StateChanged?.Invoke();
        return true;
    }

    public bool TryPickupInstalled(RackMountController rack, EquipmentDefinition equipment, int startingU, int rackUnits, out string message)
    {
        if (HeldItem || HeldCable)
        {
            message = "Place or return the carried item first.";
            return false;
        }

        if (!rack || !equipment)
        {
            message = "No installed equipment selected.";
            return false;
        }

        if (HasConnectionForOwnerPrefix(GetRackOwnerPrefix(rack.RackId, startingU)))
        {
            message = "Disconnect cables before moving this equipment.";
            return false;
        }

        HeldItem = equipment;
        CreateCarryVisual(equipment);

        for (var i = _installed.Count - 1; i >= 0; i--)
        {
            var record = _installed[i];
            if (record.RackId == rack.RackId && record.Equipment == equipment && record.StartingU == startingU && record.RackUnits == rackUnits)
            {
                _installed.RemoveAt(i);
                break;
            }
        }

        NetworkState?.RemoveDevice(GetRackOwnerPrefix(rack.RackId, startingU));

        message = $"Picked up {equipment.displayName}";
        StateChanged?.Invoke();
        return true;
    }

    public bool TryPlaceHeldEndpoint(EndpointPlacementStation station, string deviceName, out EndpointPlacementRecord record, out string message)
    {
        record = default;
        if (!HeldItem)
        {
            message = "Pick up equipment first.";
            return false;
        }

        if (!station || !station.CanPlace(HeldItem))
        {
            message = "That equipment cannot be placed here.";
            return false;
        }

        var equipment = HeldItem;
        var placementId = station.NextPlacementId(equipment);
        record = new EndpointPlacementRecord(placementId, station.StationId, equipment, CleanDeviceName(deviceName));
        _endpointPlacements.Add(record);
        HeldItem = null;
        ClearCarryVisual();
        message = $"Placed {equipment.displayName} at {station.StationId}";
        StateChanged?.Invoke();
        return true;
    }

    public bool TryPickupEndpoint(string placementId, EquipmentDefinition equipment, out string message)
    {
        if (HeldItem || HeldCable)
        {
            message = "Place or return the carried item first.";
            return false;
        }

        if (!equipment)
        {
            message = "No placed equipment selected.";
            return false;
        }

        if (HasConnectionForOwnerPrefix(GetEndpointOwnerPrefix(placementId)))
        {
            message = "Disconnect cables before moving this equipment.";
            return false;
        }

        for (var i = _endpointPlacements.Count - 1; i >= 0; i--)
        {
            if (_endpointPlacements[i].PlacementId != placementId)
                continue;

            _endpointPlacements.RemoveAt(i);
            NetworkState?.RemoveDevice(GetEndpointOwnerPrefix(placementId));
            NetworkState?.RemoveEndpoint(GetEndpointOwnerPrefix(placementId));
            HeldItem = equipment;
            CreateCarryVisual(equipment);
            message = $"Picked up {equipment.displayName}";
            StateChanged?.Invoke();
            return true;
        }

        message = "Placed equipment was not found.";
        return false;
    }

    public bool RenameRackInstalled(string rackId, int startingU, string deviceName)
    {
        for (var i = 0; i < _installed.Count; i++)
        {
            var record = _installed[i];
            if (record.RackId != rackId || record.StartingU != startingU)
                continue;

            _installed[i] = record.WithDeviceName(CleanDeviceName(deviceName));
            if (NetworkState != null && NetworkState.TryGetDevice(GetRackOwnerPrefix(rackId, startingU), out var device))
                device.Hostname = _installed[i].DeviceName;
            StateChanged?.Invoke();
            return true;
        }

        return false;
    }

    public bool RenameEndpoint(string placementId, string deviceName)
    {
        for (var i = 0; i < _endpointPlacements.Count; i++)
        {
            var record = _endpointPlacements[i];
            if (record.PlacementId != placementId)
                continue;

            _endpointPlacements[i] = record.WithDeviceName(CleanDeviceName(deviceName));
            if (NetworkState != null && NetworkState.TryGetDevice(GetEndpointOwnerPrefix(placementId), out var device))
                device.Hostname = _endpointPlacements[i].DeviceName;
            StateChanged?.Invoke();
            return true;
        }

        return false;
    }

    public bool RenameNetworkDevice(string deviceKey, string deviceName)
    {
        if (string.IsNullOrWhiteSpace(deviceKey))
            return false;

        var cleanName = CleanDeviceName(deviceName);
        if (string.IsNullOrWhiteSpace(cleanName))
            return false;

        for (var i = 0; i < _installed.Count; i++)
        {
            var record = _installed[i];
            if (GetRackOwnerPrefix(record.RackId, record.StartingU).Equals(deviceKey, StringComparison.OrdinalIgnoreCase))
                return RenameRackInstalled(record.RackId, record.StartingU, cleanName);
        }

        for (var i = 0; i < _endpointPlacements.Count; i++)
        {
            var record = _endpointPlacements[i];
            if (GetEndpointOwnerPrefix(record.PlacementId).Equals(deviceKey, StringComparison.OrdinalIgnoreCase))
                return RenameEndpoint(record.PlacementId, cleanName);
        }

        return false;
    }

    public TssScenarioDefinition GetFallbackScenario()
    {
        return fallbackScenario;
    }

    public bool TryUsePort(TssPortEndpoint endpoint, out string message, bool notifyStateChanged = true)
    {
        if (!endpoint)
        {
            message = "No port selected.";
            return false;
        }

        if (!HeldCable)
        {
            if (HeldItem)
            {
                message = "Return the carried item first.";
                return false;
            }

            if (TryDisconnectEndpoint(endpoint.EndpointId, out message))
                return true;

            message = "Select a cable first.";
            return false;
        }

        if (IsEndpointConnected(endpoint.EndpointId))
        {
            message = $"{endpoint.DisplayLabel} is already connected.";
            return false;
        }

        if (!_selectedCableEndpoint)
        {
            _selectedCableEndpoint = endpoint;
            message = $"Selected {endpoint.DisplayLabel}";
            if (notifyStateChanged)
                StateChanged?.Invoke();
            return true;
        }

        if (_selectedCableEndpoint == endpoint)
        {
            _selectedCableEndpoint = null;
            message = "Cleared first port.";
            if (notifyStateChanged)
                StateChanged?.Invoke();
            return false;
        }

        if (!ConnectsTo(_selectedCableEndpoint, endpoint, out message))
            return false;

        var connectionId = $"Cable{_nextConnectionNumber++:000}";
        var visual = TssCableVisual.Create(HeldCable, _selectedCableEndpoint, endpoint, connectionId, _cableLaneAllocator, endpointConnectorPrefab);
        var record = new TssPhysicalConnectionRecord(
            connectionId,
            HeldCable,
            HeldCable.cableType,
            _selectedCableEndpoint.EndpointId,
            endpoint.EndpointId,
            visual ? visual.gameObject : null);
        _physicalConnections.Add(record);
        HeldCable = null;
        _selectedCableEndpoint = null;
        ClearCarryVisual();
        message = $"Connected {record.EndpointAId} to {record.EndpointBId}";
        if (notifyStateChanged)
            NotifyNetworkStateChanged();
        return true;
    }

    public bool IsEndpointConnected(string endpointId)
    {
        foreach (var connection in _physicalConnections)
        {
            if (connection.IsPermanent)
                continue;

            if (connection.EndpointAId == endpointId || connection.EndpointBId == endpointId)
                return true;
        }

        return false;
    }

    public bool HasPhysicalPath(string endpointAId, string endpointBId)
    {
        return HasPhysicalPath(endpointAId, endpointBId, false);
    }

    public List<TssPhysicalPathRecord> GetEffectivePhysicalPaths()
    {
        var endpoints = FindObjectsByType<TssPortEndpoint>(FindObjectsInactive.Include, FindObjectsSortMode.None);
        var validConnections = new List<TssPhysicalConnectionRecord>();
        foreach (var connection in _physicalConnections)
            if (IsCableTypeCorrect(connection, endpoints))
                validConnections.Add(connection);

        var permanentEndpoints = new HashSet<string>();
        var playerEndpoints = new HashSet<string>();
        foreach (var connection in _physicalConnections)
        {
            if (connection.IsPermanent)
            {
                permanentEndpoints.Add(connection.EndpointAId);
                permanentEndpoints.Add(connection.EndpointBId);
            }
            else
            {
                if (!IsConfiguredPermanentEndpoint(connection.EndpointAId))
                    playerEndpoints.Add(connection.EndpointAId);
                if (!IsConfiguredPermanentEndpoint(connection.EndpointBId))
                    playerEndpoints.Add(connection.EndpointBId);
            }
        }

        var leaves = new List<string>();
        foreach (var endpointId in playerEndpoints)
        {
            if (!permanentEndpoints.Contains(endpointId))
                leaves.Add(endpointId);
        }

        var paths = new List<TssPhysicalPathRecord>();
        for (var i = 0; i < leaves.Count; i++)
        {
            for (var j = i + 1; j < leaves.Count; j++)
            {
                // ponytail: direct device-to-device player cables are effective without a permanent segment.
                if (HasPhysicalPath(leaves[i], leaves[j], true, validConnections)
                    || HasConnection(validConnections, leaves[i], leaves[j]))
                    paths.Add(new TssPhysicalPathRecord(leaves[i], leaves[j]));
            }
        }

        return paths;
    }

    public bool IsEndpointOnEffectivePhysicalPath(string endpointId)
    {
        if (string.IsNullOrWhiteSpace(endpointId))
            return false;

        foreach (var path in GetEffectivePhysicalPaths())
        {
            if (path.EndpointAId == endpointId || path.EndpointBId == endpointId)
                return true;
        }

        return false;
    }

    public bool IsAccessPointUplinkConnected()
    {
        foreach (var placement in _endpointPlacements)
        {
            if (!placement.Equipment || placement.Equipment.category != EquipmentCategory.AccessPoint)
                continue;

            var prefix = GetEndpointOwnerPrefix(placement.PlacementId) + ":";
            foreach (var endpoint in FindObjectsByType<TssPortEndpoint>(FindObjectsInactive.Include, FindObjectsSortMode.None))
            {
                if (!endpoint.EndpointId.StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
                    continue;

                foreach (var device in NetworkState?.Devices ?? Array.Empty<NetworkDeviceState>())
                {
                    if (!IsOperationalAccessSwitch(device))
                        continue;

                    foreach (var iface in device.Interfaces.Values)
                        if (iface != null && iface.AdminUp && iface.CarrierUp
                            && !string.IsNullOrWhiteSpace(iface.EndpointId)
                            && HasPhysicalPath(endpoint.EndpointId, iface.EndpointId, true))
                            return true;
                }
            }
        }

        return false;
    }

    private static bool IsOperationalAccessSwitch(NetworkDeviceState device)
    {
        if (device == null
            || (!string.Equals(device.ExpectedRole, "AN-1", StringComparison.OrdinalIgnoreCase)
                && !string.Equals(device.ExpectedRole, "AN-2", StringComparison.OrdinalIgnoreCase)
                && !string.Equals(device.Hostname, "AN-1", StringComparison.OrdinalIgnoreCase)
                && !string.Equals(device.Hostname, "AN-2", StringComparison.OrdinalIgnoreCase)))
            return false;

        foreach (var iface in device.Interfaces.Values)
            if (iface != null && iface.Name.StartsWith("Fa0/", StringComparison.OrdinalIgnoreCase)
                && int.TryParse(iface.Name.Substring(4), out var port) && port >= 1 && port <= 24)
                return true;

        return false;
    }

    public bool IsProviderDemarcConnected(string routerEndpointId)
    {
        return !string.IsNullOrWhiteSpace(routerEndpointId)
            && HasPhysicalPath(routerEndpointId, ProviderDemarcEndpointId, true);
    }

    public bool IsCableTypeCorrect(TssPhysicalConnectionRecord connection)
    {
        var endpoints = FindObjectsByType<TssPortEndpoint>(FindObjectsInactive.Include, FindObjectsSortMode.None);
        return IsCableTypeCorrect(connection, endpoints);
    }

    public IReadOnlyList<TssCableTypeStatus> GetCableTypeStatuses()
    {
        var statuses = new List<TssCableTypeStatus>();
        var endpoints = FindObjectsByType<TssPortEndpoint>(FindObjectsInactive.Include, FindObjectsSortMode.None);
        foreach (var connection in _physicalConnections)
        {
            if (connection.IsPermanent)
                continue;

            var required = RequiredCableType(connection.EndpointAId, connection.EndpointBId, endpoints);
            statuses.Add(new TssCableTypeStatus(
                connection.ConnectionId,
                connection.EndpointAId,
                connection.EndpointBId,
                connection.CableType,
                required,
                true,
                connection.CableType == required));
        }

        foreach (var requirement in Scenario?.cableTypeRequirements ?? System.Array.Empty<ScenarioCableTypeRequirement>())
        {
            if (requirement == null)
                continue;
            var endpointA = ResolveRequiredRackEndpoint(requirement.endpointAId, endpoints);
            var endpointB = ResolveRequiredRackEndpoint(requirement.endpointBId, endpoints);
            if (HasPlayerConnection(endpointA, endpointB))
                continue;

            statuses.Add(new TssCableTypeStatus(
                requirement.requirementId,
                endpointA,
                endpointB,
                requirement.requiredCableType,
                requirement.requiredCableType,
                false,
                false));
        }

        return statuses;
    }

    public bool TryGetTerminationLinks(string endpointId, out IReadOnlyList<ScenarioPermanentCableConnection> links)
    {
        var results = new List<ScenarioPermanentCableConnection>();
        if (!string.IsNullOrWhiteSpace(endpointId) && Scenario && Scenario.permanentConnections != null)
        {
            var cleanedEndpoint = endpointId.Trim();
            foreach (var link in Scenario.permanentConnections)
            {
                if (link == null || !link.requiresPunchDownTermination)
                    continue;

                if (EndpointMatches(link.patchPanelEndpointId, cleanedEndpoint)
                    || EndpointMatches(link.wallportEndpointId, cleanedEndpoint))
                    results.Add(link);
            }
        }

        links = results;
        return results.Count > 0;
    }

    public bool SetTerminationModuleState(TssTerminationModuleState state, bool isElectricallyCorrect, out string message, bool notifyStateChanged = true)
    {
        if (!TryGetTerminationLink(state, out var link, out message))
            return false;

        if (!TssT568BValidator.HasExactEightPins(state))
        {
            message = "Termination module must include exactly eight pins.";
            return false;
        }

        var role = GetEndpointRole(link, state.endpointId);
        var expectedModuleId = GetTerminationModuleId(link, role);
        if (!string.Equals(Clean(state.moduleId), expectedModuleId, StringComparison.Ordinal))
        {
            message = "Termination module ID does not match the permanent run.";
            return false;
        }

        state = state.Clone();
        state.connectionId = Clean(link.connectionId);
        state.endpointId = Clean(state.endpointId);
        state.moduleId = expectedModuleId;
        state.endpointRole = role;

        var key = TerminationKey(state.connectionId, state.endpointId);
        _terminationStates[key] = state;
        _terminationResults[key] = isElectricallyCorrect && TssT568BValidator.IsElectricallyCorrect(state);
        ReconcilePermanentConnection(state.connectionId);
        message = "Termination state saved.";
        if (notifyStateChanged)
            StateChanged?.Invoke();
        return true;
    }

    public bool TryGetTerminationModuleState(string connectionId, string endpointId, out TssTerminationModuleState state)
    {
        if (_terminationStates.TryGetValue(TerminationKey(connectionId, endpointId), out var saved))
        {
            state = saved.Clone();
            return true;
        }

        state = null;
        return false;
    }

    public IReadOnlyList<TssTerminationModuleState> GetTerminationModuleStates()
    {
        var states = new List<TssTerminationModuleState>(_terminationStates.Count);
        foreach (var state in _terminationStates.Values)
            states.Add(state.Clone());
        return states;
    }

    public bool IsTerminationCorrectForResults(string connectionId, string endpointId)
    {
        return IsTerminationCorrect(connectionId, endpointId);
    }

    public bool IsFirstSelectedEndpoint(TssPortEndpoint endpoint)
    {
        return endpoint && _selectedCableEndpoint == endpoint;
    }

    public bool CanSelectEndpoint(TssPortEndpoint endpoint)
    {
        return endpoint && (HeldCable || IsEndpointConnected(endpoint.EndpointId));
    }

    public bool HasConnectionForOwnerPrefix(string ownerPrefix)
    {
        if (string.IsNullOrWhiteSpace(ownerPrefix))
            return false;

        foreach (var connection in _physicalConnections)
        {
            if (connection.IsPermanent)
                continue;

            if (connection.EndpointAId.StartsWith(ownerPrefix, StringComparison.Ordinal)
                || connection.EndpointBId.StartsWith(ownerPrefix, StringComparison.Ordinal))
                return true;
        }

        return false;
    }

    private void CreateCarryVisual(EquipmentDefinition equipment)
    {
        ClearCarryVisual();
        if (!carryAnchor || !equipment)
            return;

        var prefab = equipment.carryPrefab ? equipment.carryPrefab : equipment.rackPrefab;
        if (!prefab)
            return;

        _carryVisual = Instantiate(prefab, carryAnchor);
        _carryVisual.transform.localPosition = equipment.carryLocalOffset;
        _carryVisual.transform.localRotation = Quaternion.Euler(equipment.carryLocalEuler);
        _carryVisual.transform.localScale = equipment.carryLocalScale;

        foreach (var collider in _carryVisual.GetComponentsInChildren<Collider>(true))
            collider.enabled = false;

        foreach (var rigidbody in _carryVisual.GetComponentsInChildren<Rigidbody>(true))
            rigidbody.isKinematic = true;
    }

    private void CreateCableCarryVisual(TssCableDefinition cable)
    {
        ClearCarryVisual();
        if (!carryAnchor || !cable || !cable.carryPrefab)
            return;

        _carryVisual = Instantiate(cable.carryPrefab, carryAnchor);
        foreach (var collider in _carryVisual.GetComponentsInChildren<Collider>(true))
            collider.enabled = false;

        foreach (var rigidbody in _carryVisual.GetComponentsInChildren<Rigidbody>(true))
            rigidbody.isKinematic = true;
    }

    private void ClearCarryVisual()
    {
        if (_carryVisual)
            DestroyUnityObject(_carryVisual);
        _carryVisual = null;
    }

    private static string CleanDeviceName(string deviceName)
    {
        return string.IsNullOrWhiteSpace(deviceName) ? string.Empty : deviceName.Trim();
    }

    public static string GetRackOwnerPrefix(string rackId, int startingU)
    {
        return $"Rack:{rackId}:U{startingU:00}";
    }

    public static string GetEndpointOwnerPrefix(string placementId)
    {
        return $"Endpoint:{placementId}";
    }

    private bool TryDisconnectEndpoint(string endpointId, out string message)
    {
        for (var i = _physicalConnections.Count - 1; i >= 0; i--)
        {
            var connection = _physicalConnections[i];
            if (connection.IsPermanent)
                continue;

            if (connection.EndpointAId != endpointId && connection.EndpointBId != endpointId)
                continue;

            if (connection.Visual)
                DestroyUnityObject(connection.Visual);

            if (connection.Cable)
            {
                HeldCable = connection.Cable;
                _selectedCableEndpoint = null;
                CreateCableCarryVisual(connection.Cable);
            }

            _physicalConnections.RemoveAt(i);
            message = $"Disconnected {endpointId}";
            NotifyNetworkStateChanged();
            return true;
        }

        message = "No cable connected.";
        return false;
    }

    private void ClearPhysicalConnections()
    {
        foreach (var connection in _physicalConnections)
        {
            if (connection.Visual)
                DestroyUnityObject(connection.Visual);
        }

        _physicalConnections.Clear();
        _cableLaneAllocator.Clear();
    }

    private bool ConnectsTo(TssPortEndpoint first, TssPortEndpoint second, out string message)
    {
        if (!string.Equals(first.ConnectorType, second.ConnectorType, StringComparison.OrdinalIgnoreCase))
        {
            message = "Connector types do not match.";
            return false;
        }

        message = string.Empty;
        return true;
    }

    private void ApplyPermanentConnections()
    {
        if (!Scenario || Scenario.permanentConnections == null)
            return;

        for (var i = 0; i < Scenario.permanentConnections.Length; i++)
        {
            var link = Scenario.permanentConnections[i];
            if (link == null
                || string.IsNullOrWhiteSpace(link.patchPanelEndpointId)
                || string.IsNullOrWhiteSpace(link.wallportEndpointId))
                continue;

            if (link.requiresPunchDownTermination && !IsPermanentLinkTerminated(link))
                continue;

            AddPermanentConnection(link, i);
        }
    }

    private void ReconcilePermanentConnection(string connectionId)
    {
        var link = FindPermanentConnection(connectionId, out var index);
        if (link == null)
            return;

        for (var i = _physicalConnections.Count - 1; i >= 0; i--)
        {
            var connection = _physicalConnections[i];
            if (connection.IsPermanent && Clean(connection.ConnectionId) == Clean(connectionId))
                _physicalConnections.RemoveAt(i);
        }

        if (!link.requiresPunchDownTermination || IsPermanentLinkTerminated(link))
            AddPermanentConnection(link, index);
    }

    private void AddPermanentConnection(ScenarioPermanentCableConnection link, int index)
    {
        var patchPanelId = link.patchPanelEndpointId.Trim();
        var wallportId = link.wallportEndpointId.Trim();
        if (HasConnection(patchPanelId, wallportId))
            return;

        var patchPanel = FindEndpoint(patchPanelId);
        var wallport = FindEndpoint(wallportId);
        if (!patchPanel || !wallport)
            return;

        var cable = link.cable ? link.cable : FindCable(link.cableType);
        _physicalConnections.Add(new TssPhysicalConnectionRecord(
            string.IsNullOrWhiteSpace(link.connectionId) ? $"Infrastructure{index + 1:000}" : link.connectionId.Trim(),
            cable,
            cable ? cable.cableType : link.cableType,
            patchPanelId,
            wallportId,
            null,
            true));
    }

    private bool IsPermanentLinkTerminated(ScenarioPermanentCableConnection link)
    {
        var connectionId = Clean(link.connectionId);
        if (string.IsNullOrEmpty(connectionId))
            return false;

        return IsTerminationCorrect(connectionId, link.patchPanelEndpointId)
            && IsTerminationCorrect(connectionId, link.wallportEndpointId);
    }

    private bool IsConfiguredPermanentEndpoint(string endpointId)
    {
        if (!Scenario || Scenario.permanentConnections == null || string.IsNullOrWhiteSpace(endpointId))
            return false;

        foreach (var link in Scenario.permanentConnections)
        {
            if (link == null)
                continue;

            if (EndpointMatches(link.patchPanelEndpointId, endpointId)
                || EndpointMatches(link.wallportEndpointId, endpointId))
                return true;
        }

        return false;
    }

    private bool IsTerminationCorrect(string connectionId, string endpointId)
    {
        return _terminationResults.TryGetValue(TerminationKey(connectionId, endpointId), out var correct) && correct;
    }

    private bool TryGetTerminationLink(TssTerminationModuleState state, out ScenarioPermanentCableConnection link, out string message)
    {
        link = null;
        if (state == null)
        {
            message = "Termination state is missing.";
            return false;
        }

        var connectionId = Clean(state.connectionId);
        var endpointId = Clean(state.endpointId);
        if (string.IsNullOrEmpty(connectionId) || string.IsNullOrEmpty(endpointId))
        {
            message = "Termination state needs a connection ID and endpoint ID.";
            return false;
        }

        link = FindPermanentConnection(connectionId, out _);
        if (link == null || !link.requiresPunchDownTermination)
        {
            message = "Permanent run is not configured for punch-down termination.";
            return false;
        }

        if (GetEndpointRole(link, endpointId) == null)
        {
            message = "Endpoint is not part of the permanent run.";
            return false;
        }

        message = string.Empty;
        return true;
    }

    private ScenarioPermanentCableConnection FindPermanentConnection(string connectionId, out int index)
    {
        index = -1;
        if (!Scenario || Scenario.permanentConnections == null)
            return null;

        var cleaned = Clean(connectionId);
        for (var i = 0; i < Scenario.permanentConnections.Length; i++)
        {
            var link = Scenario.permanentConnections[i];
            if (link != null && Clean(link.connectionId) == cleaned)
            {
                index = i;
                return link;
            }
        }

        return null;
    }

    private static string GetEndpointRole(ScenarioPermanentCableConnection link, string endpointId)
    {
        if (EndpointMatches(link.patchPanelEndpointId, endpointId))
            return "A";
        if (EndpointMatches(link.wallportEndpointId, endpointId))
            return "B";
        return null;
    }

    private static string GetTerminationModuleId(ScenarioPermanentCableConnection link, string role)
    {
        var configured = role == "A" ? link.endpointATerminationModuleId : link.endpointBTerminationModuleId;
        if (!string.IsNullOrWhiteSpace(configured))
            return configured.Trim();

        return $"{Clean(link.connectionId)}:{role}";
    }

    private static string TerminationKey(string connectionId, string endpointId)
    {
        return $"{Clean(connectionId)}|{Clean(endpointId)}";
    }

    private static bool EndpointMatches(string left, string right)
    {
        return string.Equals(Clean(left), Clean(right), StringComparison.Ordinal);
    }

    private static string Clean(string value)
    {
        return string.IsNullOrWhiteSpace(value) ? string.Empty : value.Trim();
    }

    private bool HasConnection(string endpointAId, string endpointBId)
    {
        foreach (var connection in _physicalConnections)
        {
            var sameDirection = connection.EndpointAId == endpointAId && connection.EndpointBId == endpointBId;
            var reverseDirection = connection.EndpointAId == endpointBId && connection.EndpointBId == endpointAId;
            if (sameDirection || reverseDirection)
                return true;
        }

        return false;
    }

    private bool HasEffectiveConnection(string endpointAId, string endpointBId)
    {
        foreach (var connection in _physicalConnections)
        {
            if (!IsCableTypeCorrect(connection))
                continue;

            var sameDirection = connection.EndpointAId == endpointAId && connection.EndpointBId == endpointBId;
            var reverseDirection = connection.EndpointAId == endpointBId && connection.EndpointBId == endpointAId;
            if (sameDirection || reverseDirection)
                return true;
        }

        return false;
    }

    private bool HasPhysicalPath(string endpointAId, string endpointBId, bool requirePermanent)
    {
        var endpoints = FindObjectsByType<TssPortEndpoint>(FindObjectsInactive.Include, FindObjectsSortMode.None);
        var validConnections = new List<TssPhysicalConnectionRecord>();
        foreach (var connection in _physicalConnections)
            if (IsCableTypeCorrect(connection, endpoints))
                validConnections.Add(connection);
        return HasPhysicalPath(endpointAId, endpointBId, requirePermanent, validConnections);
    }

    private bool HasPhysicalPath(string endpointAId, string endpointBId, bool requirePermanent, IReadOnlyList<TssPhysicalConnectionRecord> connections)
    {
        if (string.IsNullOrWhiteSpace(endpointAId) || string.IsNullOrWhiteSpace(endpointBId))
            return false;

        if (endpointAId == endpointBId)
            return !requirePermanent;

        var seen = new HashSet<string>();
        var queue = new Queue<TssPhysicalPathSearchNode>();
        queue.Enqueue(new TssPhysicalPathSearchNode(endpointAId, false));
        seen.Add($"{endpointAId}|False");

        while (queue.Count > 0)
        {
            var node = queue.Dequeue();
            foreach (var connection in connections)
            {
                var other = ConnectedOtherEndpoint(connection, node.EndpointId);
                if (string.IsNullOrEmpty(other))
                    continue;

                var usedPermanent = node.UsedPermanent || connection.IsPermanent;
                if (other == endpointBId && (!requirePermanent || usedPermanent))
                    return true;

                var key = $"{other}|{usedPermanent}";
                if (seen.Add(key))
                    queue.Enqueue(new TssPhysicalPathSearchNode(other, usedPermanent));
            }
        }

        return false;
    }

    private bool IsCableTypeCorrect(TssPhysicalConnectionRecord connection, TssPortEndpoint[] endpoints)
    {
        return connection.IsPermanent || connection.CableType == RequiredCableType(connection.EndpointAId, connection.EndpointBId, endpoints);
    }

    private TssCableType RequiredCableType(string endpointAId, string endpointBId, TssPortEndpoint[] endpoints)
    {
        foreach (var requirement in Scenario?.cableTypeRequirements ?? System.Array.Empty<ScenarioCableTypeRequirement>())
        {
            if (requirement != null && EndpointPairMatches(ResolveRequiredRackEndpoint(requirement.endpointAId, endpoints), ResolveRequiredRackEndpoint(requirement.endpointBId, endpoints), endpointAId, endpointBId))
                return requirement.requiredCableType;
        }

        return Scenario ? Scenario.defaultPlayerCableType : TssCableType.StraightThrough;
    }

    private string ResolveRequiredRackEndpoint(string endpointId, TssPortEndpoint[] endpoints)
    {
        if (string.IsNullOrEmpty(endpointId)) return endpointId;
        foreach (var endpoint in endpoints)
            if (endpoint && string.Equals(endpoint.DeviceName, endpointId.Trim(), StringComparison.OrdinalIgnoreCase))
                return endpoint.EndpointId;
        // A role's authored U identifies its port; allowed placement ranges can move that device.
        // Resolve only scenario requirements. Never rewrite physical endpoint IDs or authored data.
        foreach (var binding in Scenario?.networkScenario?.roleBindings ?? Array.Empty<NetworkRoleBinding>())
        {
            if (binding == null || string.IsNullOrEmpty(binding.deviceKey) || !endpointId.StartsWith(binding.deviceKey + ":", StringComparison.Ordinal)) continue;
            foreach (var expected in Scenario.expectedRackPlacements ?? Array.Empty<ExpectedRackPlacement>())
            {
                if (expected == null || expected.requiredName != binding.role || expected.category != binding.category) continue;
                var firstU = expected.endingU == 0 ? expected.startingU : Math.Min(expected.startingU, expected.endingU);
                var lastU = Math.Max(expected.startingU, expected.endingU);
                var prefix = $"Rack:{expected.rackId}:U";
                if (!binding.deviceKey.StartsWith(prefix, StringComparison.Ordinal) ||
                    !int.TryParse(binding.deviceKey.Substring(prefix.Length), out var authoredU) || authoredU < firstU || authoredU > lastU) continue;
                foreach (var installed in _installed)
                    if (installed.RackId == expected.rackId && installed.DeviceName == expected.requiredName &&
                        installed.Equipment && installed.Equipment.category == expected.category &&
                        installed.StartingU >= firstU && installed.StartingU <= lastU)
                        return GetRackOwnerPrefix(installed.RackId, installed.StartingU) + endpointId.Substring(binding.deviceKey.Length);
            }
        }
        return endpointId;
    }

    private static bool HasConnection(IReadOnlyList<TssPhysicalConnectionRecord> connections, string endpointAId, string endpointBId)
    {
        foreach (var connection in connections)
            if (EndpointPairMatches(connection.EndpointAId, connection.EndpointBId, endpointAId, endpointBId))
                return true;
        return false;
    }

    private bool HasPlayerConnection(string endpointAId, string endpointBId)
    {
        foreach (var connection in _physicalConnections)
        {
            if (!connection.IsPermanent && EndpointPairMatches(connection.EndpointAId, connection.EndpointBId, endpointAId, endpointBId))
                return true;
        }

        return false;
    }

    private bool HasCorrectPlayerConnection(string endpointAId, string endpointBId)
    {
        foreach (var connection in _physicalConnections)
            if (!connection.IsPermanent
                && IsCableTypeCorrect(connection)
                && EndpointPairMatches(connection.EndpointAId, connection.EndpointBId, endpointAId, endpointBId))
                return true;

        return false;
    }

    private bool HasPermanentConnection(string endpointAId, string endpointBId)
    {
        foreach (var connection in _physicalConnections)
            if (connection.IsPermanent
                && EndpointPairMatches(connection.EndpointAId, connection.EndpointBId, endpointAId, endpointBId))
                return true;

        return false;
    }

    private static bool EndpointPairMatches(string firstA, string firstB, string secondA, string secondB)
    {
        return (string.Equals(firstA, secondA, StringComparison.OrdinalIgnoreCase)
                && string.Equals(firstB, secondB, StringComparison.OrdinalIgnoreCase))
            || (string.Equals(firstA, secondB, StringComparison.OrdinalIgnoreCase)
                && string.Equals(firstB, secondA, StringComparison.OrdinalIgnoreCase));
    }

    private static string ConnectedOtherEndpoint(TssPhysicalConnectionRecord connection, string endpointId)
    {
        if (connection.EndpointAId == endpointId)
            return connection.EndpointBId;
        if (connection.EndpointBId == endpointId)
            return connection.EndpointAId;
        return string.Empty;
    }

    private TssCableDefinition FindCable(TssCableType cableType)
    {
        if (!Scenario || Scenario.cableInventory == null)
            return null;

        foreach (var item in Scenario.cableInventory)
        {
            if (item != null && item.cable && item.cable.cableType == cableType)
                return item.cable;
        }

        return null;
    }

    private static TssPortEndpoint FindEndpoint(string endpointId)
    {
        foreach (var endpoint in FindObjectsByType<TssPortEndpoint>(FindObjectsInactive.Include, FindObjectsSortMode.None))
        {
            if (endpoint.EndpointId == endpointId)
                return endpoint;
        }

        return null;
    }

    private static void DestroyUnityObject(UnityEngine.Object target)
    {
        if (!target)
            return;

        if (Application.isPlaying)
            Destroy(target);
        else
            DestroyImmediate(target);
    }

    private readonly struct TssPhysicalPathSearchNode
    {
        public readonly string EndpointId;
        public readonly bool UsedPermanent;

        public TssPhysicalPathSearchNode(string endpointId, bool usedPermanent)
        {
            EndpointId = endpointId;
            UsedPermanent = usedPermanent;
        }
    }
}

public readonly struct TssPhysicalPathRecord
{
    public readonly string EndpointAId;
    public readonly string EndpointBId;

    public TssPhysicalPathRecord(string endpointAId, string endpointBId)
    {
        EndpointAId = endpointAId;
        EndpointBId = endpointBId;
    }
}

public readonly struct TssPhysicalConnectionRecord
{
    public readonly string ConnectionId;
    public readonly TssCableDefinition Cable;
    public readonly TssCableType CableType;
    public readonly string EndpointAId;
    public readonly string EndpointBId;
    public readonly GameObject Visual;
    public readonly bool IsPermanent;

    public TssPhysicalConnectionRecord(string connectionId, TssCableDefinition cable, TssCableType cableType, string endpointAId, string endpointBId, GameObject visual, bool isPermanent = false)
    {
        ConnectionId = connectionId;
        Cable = cable;
        CableType = cableType;
        EndpointAId = endpointAId;
        EndpointBId = endpointBId;
        Visual = visual;
        IsPermanent = isPermanent;
    }
}

public readonly struct TssCableTypeStatus
{
    public readonly string ConnectionId;
    public readonly string EndpointAId;
    public readonly string EndpointBId;
    public readonly TssCableType ActualCableType;
    public readonly TssCableType RequiredCableType;
    public readonly bool IsConnected;
    public readonly bool IsCorrect;

    public TssCableTypeStatus(string connectionId, string endpointAId, string endpointBId, TssCableType actualCableType, TssCableType requiredCableType, bool isConnected, bool isCorrect)
    {
        ConnectionId = connectionId;
        EndpointAId = endpointAId;
        EndpointBId = endpointBId;
        ActualCableType = actualCableType;
        RequiredCableType = requiredCableType;
        IsConnected = isConnected;
        IsCorrect = isCorrect;
    }
}

public readonly struct InstalledEquipmentRecord
{
    public readonly string RackId;
    public readonly EquipmentDefinition Equipment;
    public readonly int StartingU;
    public readonly int RackUnits;
    public readonly string DeviceName;

    public InstalledEquipmentRecord(string rackId, EquipmentDefinition equipment, int startingU, int rackUnits, string deviceName)
    {
        RackId = rackId;
        Equipment = equipment;
        StartingU = startingU;
        RackUnits = rackUnits;
        DeviceName = deviceName;
    }

    public InstalledEquipmentRecord WithDeviceName(string deviceName)
    {
        return new InstalledEquipmentRecord(RackId, Equipment, StartingU, RackUnits, deviceName);
    }
}

public readonly struct EndpointPlacementRecord
{
    public readonly string PlacementId;
    public readonly string StationId;
    public readonly EquipmentDefinition Equipment;
    public readonly string DeviceName;

    public EndpointPlacementRecord(string placementId, string stationId, EquipmentDefinition equipment, string deviceName)
    {
        PlacementId = placementId;
        StationId = stationId;
        Equipment = equipment;
        DeviceName = deviceName;
    }

    public EndpointPlacementRecord WithDeviceName(string deviceName)
    {
        return new EndpointPlacementRecord(PlacementId, StationId, Equipment, deviceName);
    }
}

public static class TssScenarioLaunchState
{
    private static TssScenarioDefinition _selectedScenario;
    private static string _studentId;

    public static void Select(TssScenarioDefinition scenario, string studentId = null)
    {
        if (_selectedScenario != scenario) ReleaseRuntime(_selectedScenario);
        _selectedScenario = scenario;
        _studentId = studentId ?? string.Empty;
    }

    public static void ReleaseRuntime(TssScenarioDefinition scenario)
    {
        if (!scenario || !scenario.IsExternalRuntime) return;
        if (Application.isPlaying) UnityEngine.Object.Destroy(scenario);
        else UnityEngine.Object.DestroyImmediate(scenario);
    }

    public static bool TryCreateStudentId(string lastName, string computerNumber, out string studentId)
    {
        studentId = string.Empty;
        if (string.IsNullOrWhiteSpace(lastName) || !int.TryParse(computerNumber?.Trim(), out var number) || number <= 0)
            return false;

        studentId = lastName.Trim().ToUpperInvariant() + "-" + number;
        return true;
    }

    public static TssScenarioDefinition ConsumeSelectedScenario(out string studentId)
    {
        var scenario = _selectedScenario;
        _selectedScenario = null;
        studentId = _studentId;
        _studentId = null;
        return scenario;
    }
}
