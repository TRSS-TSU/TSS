using System;
using UnityEngine;

[CreateAssetMenu(menuName = "TSS/Scenario Definition")]
public sealed class TssScenarioDefinition : ScriptableObject
{
    public string scenarioId;
    public string displayName;
    public string sceneName = "SampleScene";
    public ScenarioInventoryItem[] inventory;
    public ScenarioCableInventoryItem[] cableInventory;
    public TssCableType defaultPlayerCableType = TssCableType.StraightThrough;
    public ScenarioCableTypeRequirement[] cableTypeRequirements;
    public ScenarioPermanentCableConnection[] permanentConnections;
    public ScenarioCableScheduleEntry[] cableSchedule;
    public ScenarioNamingEntry[] namingRows;
    public RackScenarioConfig[] racks;
    public ExpectedRackPlacement[] expectedRackPlacements;
    public ExpectedEndpointPlacement[] expectedEndpointPlacements;
    public NetworkScenarioConfig networkScenario;
    public TssBinderReferenceContent binderReference;
}

[Serializable]
public sealed class TssBinderReferenceContent
{
    public TssBinderNamingContent namingAndRacks;
    public TssBinderVlanContent vlans;
    public TssBinderIpPlanContent ipPlan;
    public TssBinderCableScheduleContent cableSchedule;
    public TssBinderTabContent circuitLetter;
    public TssBinderTabContent wireless;
    public TssBinderTabContent credentials;
}

[Serializable]
public sealed class TssBinderNamingContent
{
    public string sectionTitle;
    public string scenarioHeading;
    [TextArea(2, 8)] public string intro;
}

[Serializable]
public sealed class TssBinderVlanContent
{
    public string sectionTitle;
    public string scenarioHeading;
    [TextArea(2, 8)] public string intro;
    [TextArea(2, 8)] public string endingParagraph;
}

[Serializable]
public sealed class TssBinderIpPlanContent
{
    public string sectionTitle;
    public string scenarioHeading;
    public string hostAddressesHeading;
    [TextArea(2, 8)] public string paragraph;
}

[Serializable]
public sealed class TssBinderCableScheduleContent
{
    public string sectionTitle;
    public string scenarioHeading;
    [TextArea(2, 8)] public string introParagraph;
}

[Serializable]
public sealed class TssBinderTabContent
{
    public string sectionTitle;
    public string scenarioHeading;
    [TextArea(2, 8)] public string intro;
    [TextArea(2, 8)] public string note;
    public TssBinderReferenceRow[] rows;
}

[Serializable]
public sealed class TssBinderReferenceRow
{
    public string label;
    public string value;
}

[Serializable]
public sealed class ScenarioInventoryItem
{
    public EquipmentDefinition equipment;
    [Min(0)] public int quantity;
}

[Serializable]
public sealed class ScenarioCableInventoryItem
{
    public TssCableDefinition cable;
    [Min(0)] public int quantity;
}

[Serializable]
public sealed class ScenarioCableTypeRequirement
{
    public string requirementId;
    public string endpointAId;
    public string endpointBId;
    public TssCableType requiredCableType = TssCableType.StraightThrough;
}

[Serializable]
public sealed class ScenarioPermanentCableConnection
{
    public string connectionId;
    public TssCableDefinition cable;
    public TssCableType cableType = TssCableType.StraightThrough;
    public string patchPanelEndpointId;
    public string wallportEndpointId;
    public bool requiresPunchDownTermination;
    public string endpointATerminationModuleId;
    public string endpointBTerminationModuleId;
}

[Serializable]
public sealed class ScenarioCableScheduleEntry
{
    public string fromEndpoint;
    public string fromPort;
    public string toEndpoint;
    public string toPort;
    public string cableType;
}

[Serializable]
public sealed class ScenarioNamingEntry
{
    public string rackId;
    public string slot;
    public string device;
    public string assignedName;
    public string rationale;
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
public sealed class ExpectedRackPlacement
{
    public string phaseLabel;
    public string rackId;
    public int startingU;
    // ponytail: zero preserves existing exact-U scenarios; set endingU for an inclusive range.
    public int endingU;
    public EquipmentCategory category;
    public string requiredName;
}

[Serializable]
public sealed class ExpectedEndpointPlacement
{
    public string phaseLabel;
    public string stationId;
    public EquipmentCategory category;
    public string requiredName;
}

[Serializable]
public sealed class SopSection
{
    public string title;
    [TextArea(2, 8)] public string body;
}

[Serializable]
public sealed class NetworkScenarioConfig
{
    public bool enableLayer23Simulation;
    [Min(1)] public int profileSeed = 20261008;
    public string managementVlanName = "Management";
    public string receptionVlanName = "Reception";
    public string dataVlanName = "Data";
    public string nativeVlanName = "Native";
    public string deadVlanName = "Dead_Vlan";
    public string[] dhcpNetworkNames = { "Reception" };
    public int vlanMinimum = 10;
    public int vlanMaximum = 399;
    public string[] endpointAliases;
    public NetworkRoleBinding[] roleBindings;
    public NetworkRequiredPort[] requiredPorts;
    public NetworkEvidenceRequirement[] evidenceRequirements;
}

[Serializable]
public sealed class NetworkRoleBinding
{
    public string role;
    public string deviceKey;
    public EquipmentCategory category;
    public string displayName;
}

[Serializable]
public sealed class NetworkRequiredPort
{
    public string deviceRole;
    public string interfaceName;
    public string endpointId;
}

[Serializable]
public sealed class NetworkEvidenceRequirement
{
    public string evidenceId;
    public string command;
    public string description;
}
