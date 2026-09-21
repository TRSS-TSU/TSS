using System;
using UnityEngine;

[CreateAssetMenu(menuName = "TSS/Scenario Definition")]
public sealed class TssScenarioDefinition : ScriptableObject
{
    public string scenarioId;
    public string displayName;
    public string sceneName = "SampleScene";
    public ScenarioInventoryItem[] inventory;
    public RackScenarioConfig[] racks;
    public SopSection[] sopSections;
}

[Serializable]
public sealed class ScenarioInventoryItem
{
    public EquipmentDefinition equipment;
    [Min(0)] public int quantity;
}

[Serializable]
public sealed class RackScenarioConfig
{
    public string rackId;
    public int lowestU = 1;
    public int highestU = 40;
    public int firstInstallableU = 3;
    public int lastInstallableU = 39;
    public int[] reservedUPositions = { 40 };
}

[Serializable]
public sealed class SopSection
{
    public string title;
    [TextArea(2, 8)] public string body;
}
