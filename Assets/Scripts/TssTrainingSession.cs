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
    private GameObject _carryVisual;

    public event Action StateChanged;
    public TssScenarioDefinition Scenario { get; private set; }
    public EquipmentDefinition HeldItem { get; private set; }
    public GameObject CarryVisual => _carryVisual;
    public IReadOnlyList<InstalledEquipmentRecord> Installed => _installed;

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

    public bool TryInstallHeld(RackMountController rack, int startingU, out string message)
    {
        if (!HeldItem)
        {
            message = "Pick up equipment first.";
            return false;
        }

        var equipment = HeldItem;
        if (!rack.TryInstall(equipment, startingU, out message))
            return false;

        _installed.Add(new InstalledEquipmentRecord(rack.RackId, equipment, startingU, equipment.rackUnits));
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
}

public readonly struct InstalledEquipmentRecord
{
    public readonly string RackId;
    public readonly EquipmentDefinition Equipment;
    public readonly int StartingU;
    public readonly int RackUnits;

    public InstalledEquipmentRecord(string rackId, EquipmentDefinition equipment, int startingU, int rackUnits)
    {
        RackId = rackId;
        Equipment = equipment;
        StartingU = startingU;
        RackUnits = rackUnits;
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
