using System;
using System.Collections.Generic;
using UnityEngine;

public sealed class TssTrainingSession : MonoBehaviour
{
    public static TssTrainingSession Instance { get; private set; }

    [SerializeField] private TssScenarioDefinition fallbackScenario;
    [SerializeField] private Transform carryAnchor;
    [SerializeField] private GameObject scenarioMenuHost;

    private readonly Dictionary<EquipmentDefinition, int> _remaining = new();
    private readonly Dictionary<TssCableDefinition, int> _remainingCables = new();
    private readonly List<InstalledEquipmentRecord> _installed = new();
    private readonly List<EndpointPlacementRecord> _endpointPlacements = new();
    private readonly List<TssPhysicalConnectionRecord> _physicalConnections = new();
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

    private void Awake()
    {
        Instance = this;
    }

    private void Start()
    {
        var selected = TssScenarioLaunchState.ConsumeSelectedScenario();
        if (selected)
        {
            if (scenarioMenuHost)
                scenarioMenuHost.SetActive(false);
            BeginScenario(selected);
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

    public void BeginScenario(TssScenarioDefinition scenario)
    {
        Scenario = scenario;
        HeldItem = null;
        HeldCable = null;
        _selectedCableEndpoint = null;
        _nextConnectionNumber = 1;
        _remaining.Clear();
        _remainingCables.Clear();
        _installed.Clear();
        _endpointPlacements.Clear();
        ClearPhysicalConnections();
        ClearCarryVisual();

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

    public bool TryCheckoutCable(TssCableDefinition cable, out string message)
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
            StateChanged?.Invoke();
            return true;
        }

        return false;
    }

    public TssScenarioDefinition GetFallbackScenario()
    {
        return fallbackScenario;
    }

    public bool TryUsePort(TssPortEndpoint endpoint, out string message)
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

        if (!endpoint.Supports(HeldCable.cableType))
        {
            message = $"{endpoint.DisplayLabel} does not support {HeldCable.cableType}.";
            return false;
        }

        if (!_selectedCableEndpoint)
        {
            _selectedCableEndpoint = endpoint;
            message = $"Selected {endpoint.DisplayLabel}";
            StateChanged?.Invoke();
            return true;
        }

        if (_selectedCableEndpoint == endpoint)
        {
            _selectedCableEndpoint = null;
            message = "Cleared first port.";
            StateChanged?.Invoke();
            return false;
        }

        if (!ConnectsTo(_selectedCableEndpoint, endpoint, out message))
            return false;

        var visual = TssCableVisual.Create(HeldCable, _selectedCableEndpoint, endpoint);
        var record = new TssPhysicalConnectionRecord(
            $"Cable{_nextConnectionNumber++:000}",
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
        StateChanged?.Invoke();
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
                playerEndpoints.Add(connection.EndpointAId);
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
                if (HasPhysicalPath(leaves[i], leaves[j], true))
                    paths.Add(new TssPhysicalPathRecord(leaves[i], leaves[j]));
            }
        }

        return paths;
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
            StateChanged?.Invoke();
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
    }

    private bool ConnectsTo(TssPortEndpoint first, TssPortEndpoint second, out string message)
    {
        if (!string.Equals(first.ConnectorType, second.ConnectorType, StringComparison.OrdinalIgnoreCase))
        {
            message = "Connector types do not match.";
            return false;
        }

        if (!second.Supports(HeldCable.cableType))
        {
            message = $"{second.DisplayLabel} does not support {HeldCable.cableType}.";
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

            var patchPanelId = link.patchPanelEndpointId.Trim();
            var wallportId = link.wallportEndpointId.Trim();
            if (HasConnection(patchPanelId, wallportId))
                continue;

            var patchPanel = FindEndpoint(patchPanelId);
            var wallport = FindEndpoint(wallportId);
            if (!patchPanel || !wallport)
                continue;

            var cable = link.cable ? link.cable : FindCable(link.cableType);
            _physicalConnections.Add(new TssPhysicalConnectionRecord(
                string.IsNullOrWhiteSpace(link.connectionId) ? $"Infrastructure{i + 1:000}" : link.connectionId.Trim(),
                cable,
                cable ? cable.cableType : link.cableType,
                patchPanelId,
                wallportId,
                null,
                true));
        }
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

    private bool HasPhysicalPath(string endpointAId, string endpointBId, bool requirePermanent)
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
            foreach (var connection in _physicalConnections)
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

    public static void Select(TssScenarioDefinition scenario)
    {
        _selectedScenario = scenario;
    }

    public static TssScenarioDefinition ConsumeSelectedScenario()
    {
        var scenario = _selectedScenario;
        _selectedScenario = null;
        return scenario;
    }
}
