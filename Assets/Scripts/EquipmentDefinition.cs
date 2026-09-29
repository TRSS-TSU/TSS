using System;
using UnityEngine;

public enum EquipmentCategory
{
    Router,
    Switch,
    Ups,
    Pdu,
    Server,
    PatchPanel,
    DesktopPc,
    Printer
}

[CreateAssetMenu(menuName = "TSS/Equipment Definition")]
public sealed class EquipmentDefinition : ScriptableObject
{
    public string equipmentId;
    public string displayName;
    public EquipmentCategory category;
    [Min(1)] public int rackUnits = 1;
    public GameObject rackPrefab;
    public GameObject carryPrefab;
    public bool useRearRackPlacement;
    public Vector3 rackLocalOffset;
    public Vector3 rackLocalEuler;
    public Vector3 rackLocalScale = Vector3.one;
    public Vector3 carryLocalOffset;
    public Vector3 carryLocalEuler;
    public Vector3 carryLocalScale = Vector3.one;
    public GameObject placementPrefab;
    public Vector3 placementLocalOffset;
    public Vector3 placementLocalEuler;
    public Vector3 placementLocalScale = Vector3.one;
    public EquipmentInterface[] interfaces;

    public GameObject GetPlacementPrefab()
    {
        if (placementPrefab)
            return placementPrefab;

        return carryPrefab ? carryPrefab : rackPrefab;
    }
}

[Serializable]
public sealed class EquipmentInterface
{
    public string name;
    public string connectorType;
    public string label;
    public TssCableType[] supportedCableTypes;
    public string portAnchorPath;

    public bool Supports(TssCableType cableType)
    {
        if (supportedCableTypes == null || supportedCableTypes.Length == 0)
            return true;

        foreach (var supported in supportedCableTypes)
        {
            if (supported == cableType)
                return true;
        }

        return false;
    }
}
