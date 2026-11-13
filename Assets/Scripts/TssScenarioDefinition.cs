using System;
using UnityEngine;

[CreateAssetMenu(menuName = "TSS/Scenario Definition")]
public sealed class TssScenarioDefinition : ScriptableObject
{
    public bool IsExternalRuntime { get; internal set; }
    public string scenarioId;
    public string displayName;
    public string facilityId = TssExternalScenarioData.DefaultFacilityId;
    public string catalogId = TssExternalScenarioData.DefaultCatalogId;
    public string sceneName = "SampleScene";
    public ScenarioInventoryItem[] inventory;
    public ScenarioCableInventoryItem[] cableInventory;
    public TssCableType defaultPlayerCableType = TssCableType.StraightThrough;
    public ScenarioCableTypeRequirement[] cableTypeRequirements;
    public ScenarioPermanentCableConnection[] permanentConnections;
    public ScenarioEffectivePathRequirement[] expectedEffectivePaths;
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
    public string crossoverGroupId;
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
public sealed class ScenarioEffectivePathRequirement
{
    public string requirementId;
    public string endpointAId;
    public string endpointBId;
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
    [Header("Instructor-authored run values")]
    public string circuitId = "CIR-TRAINING-001";
    public string ssid = "TSS-GUEST";
    public string wifiPassphrase = "TrainingWifi!";
    public int wirelessChannel = 6;
    public string apAdminPassword = "TrainingAp!";
    public string enableSecret = "TrainingEnable";
    public string consolePassword = "TrainingConsole";
    public string vtyPassword = "TrainingVty";
    public string localUsername;
    public string localPassword;
    [TextArea] public string motd = "AUTHORIZED TRAINING ACCESS ONLY";
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
    public NetworkTrunkRequirement[] trunkRequirements;
    public NetworkUnusedPortRequirement[] unusedPortRequirements;
    public NetworkRouterSubinterfaceRequirement[] routerSubinterfaceRequirements;
    public NetworkEvidenceRequirement[] evidenceRequirements;
    public string routerRole = "CN-1";
    public string routerParentInterface = "Fa0/0";
    public string routerWanInterface = "Fa0/1";
    public string accessPointSwitchRole = "AN-1";
    public string accessPointSwitchPort = "Fa0/7";
    public string testLaptopRole = "TestLaptop";
    public string routeEvidenceCommand = "show ip route";
    public string[] testLaptopDiagnosticEvidenceIds = { "ipconfig", "TestLaptop-provider" };
    public string redWolfEvidenceId = "redwolf";
    public bool requirePasswordEncryption = true;
    public bool requireDomainLookupDisabled = true;
    public bool requireConsoleLogin = true;
    public bool requireVtyLogin = true;
    public bool requireNoEnablePassword = true;
    public bool requireRouteEvidence = true;
    public bool requireDefaultRoute = true;
    public bool requireSave = true;
    public bool requireAccessPointUplink = true;
    public bool requireAccessPointSwitchPort = true;
    public bool requireAccessPointConfiguration = true;
    public bool requireLaptopAssociation = true;
    public bool requireDhcpLease = true;
    public bool requireLaptopDiagnostics = true;
    public bool requireRedWolfEvidence = true;
    public NetworkWorkPackageObjective[] workPackageObjectives;
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
    // Empty preserves legacy endpoint-name inference; new scenarios should author this explicitly.
    public string vlanName;
}

[Serializable]
public sealed class NetworkTrunkRequirement
{
    public string deviceRole;
    public string interfaceName;
    public string nativeVlanName;
    public string[] allowedVlanNames;
}

[Serializable]
public sealed class NetworkUnusedPortRequirement
{
    public string deviceRole;
    public string interfacePrefix = "Fa0/";
    public int firstPort;
    public int lastPort;
    public string deadVlanName;
}

[Serializable]
public sealed class NetworkRouterSubinterfaceRequirement
{
    public string vlanName;
    public string networkName;
    public int addressSuffix = 1;
    public string mask = "255.255.255.0";
}

[Serializable]
public sealed class NetworkEvidenceRequirement
{
    public string evidenceId;
    public string command;
    public string description;
}

[Serializable]
public sealed class NetworkWorkPackageObjective
{
    public int workPackage;
    public NetworkWorkPackageObjectiveGroup[] groups;
}

[Serializable]
public sealed class NetworkWorkPackageObjectiveGroup
{
    public string id;
    public string heading;
    // ponytail: prefixes keep scenario grouping compact; use explicit IDs if selectors become ambiguous.
    public string[] conditionIdPrefixes;
}
