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
    Printer,
    AccessPoint,
    Laptop
}

public enum EquipmentInterfaceFace
{
    Front,
    Rear
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
    [Min(0f), Tooltip("Distance beyond each side of the carried item's bounds; edits apply live while carrying in Play Mode.")]
    public float carryHandSurfaceOffset = 0.04f;
    [Tooltip("Local XYZ adjustment applied to both hand attach points; edits apply live while carrying in Play Mode.")]
    public Vector3 carryHandPositionOffset;
    [Tooltip("Euler adjustments from the carried item's rotation for each hand.")]
    public Vector3 carryLeftHandRotationOffset;
    public Vector3 carryRightHandRotationOffset;
    public bool swapCarryHandSides;
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
    public EquipmentInterfaceFace interfaceFace;

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
