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
    private readonly List<InstalledEquipmentRecord> _installed = new();
    private readonly List<EndpointPlacementRecord> _endpointPlacements = new();
    private GameObject _carryVisual;

    public event Action StateChanged;
    public TssScenarioDefinition Scenario { get; private set; }
    public EquipmentDefinition HeldItem { get; private set; }
    public GameObject CarryVisual => _carryVisual;
    public IReadOnlyList<InstalledEquipmentRecord> Installed => _installed;
    public IReadOnlyList<EndpointPlacementRecord> EndpointPlacements => _endpointPlacements;

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
        _remaining.Clear();
        _installed.Clear();
        _endpointPlacements.Clear();
        ClearCarryVisual();

        if (scenario && scenario.inventory != null)
        {
            foreach (var item in scenario.inventory)
            {
                if (item != null && item.equipment)
                    _remaining[item.equipment] = Mathf.Max(0, item.quantity);
            }
        }

        StateChanged?.Invoke();
    }

    public IEnumerable<ScenarioInventoryItem> GetScenarioInventory()
    {
        return Scenario ? Scenario.inventory : Array.Empty<ScenarioInventoryItem>();
    }

    public int GetRemaining(EquipmentDefinition equipment)
    {
        return equipment && _remaining.TryGetValue(equipment, out var count) ? count : 0;
    }

    public bool TryCheckout(EquipmentDefinition equipment, out string message)
    {
        if (!Scenario)
        {
            message = "Select a scenario first.";
            return false;
        }

        if (HeldItem)
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
        StateChanged?.Invoke();
        return true;
    }

    public bool TryPickupInstalled(RackMountController rack, EquipmentDefinition equipment, int startingU, int rackUnits, out string message)
    {
        if (HeldItem)
        {
            message = "Place or return the carried item first.";
            return false;
        }

        if (!rack || !equipment)
        {
            message = "No installed equipment selected.";
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
        if (HeldItem)
        {
            message = "Place or return the carried item first.";
            return false;
        }

        if (!equipment)
        {
            message = "No placed equipment selected.";
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

    private void ClearCarryVisual()
    {
        if (_carryVisual)
            Destroy(_carryVisual);
        _carryVisual = null;
    }

    private static string CleanDeviceName(string deviceName)
    {
        return string.IsNullOrWhiteSpace(deviceName) ? string.Empty : deviceName.Trim();
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
