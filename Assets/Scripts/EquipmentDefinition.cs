using System;
using UnityEngine;

public enum EquipmentCategory
{
    Router,
    Switch,
    Ups,
    Pdu,
    Server
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
    public EquipmentInterface[] interfaces;
}

[Serializable]
public sealed class EquipmentInterface
{
    public string name;
    public string connectorType;
    public string label;
}
