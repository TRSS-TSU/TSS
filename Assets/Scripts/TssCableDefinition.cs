using UnityEngine;

public enum TssCableType
{
    StraightThrough,
    Crossover,
    Rollover
}

[CreateAssetMenu(menuName = "TSS/Cable Definition")]
public sealed class TssCableDefinition : ScriptableObject
{
    public string cableId;
    public string displayName;
    public TssCableType cableType;
    public GameObject carryPrefab;
    public Material visualMaterial;
    [Min(0.005f)] public float radiusMeters = 0.012f;
}
