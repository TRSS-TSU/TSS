using System;

// External scenarios use reference-free authored rows; Unity assets are represented by stable IDs.
[Serializable]
public sealed class TssExternalScenarioData
{
    public const string Format = "tss-scenario";
    public const int LegacyVersion = 1;
    public const int Version = 2;
    public const string DefaultFacilityId = "TSS_MAIN";
    public const string DefaultCatalogId = "TSS_MAIN_CATALOG";
    public string format;
    public int schemaVersion;
    public string scenarioId;
    public string displayName;
    public string facilityId;
    public string catalogId;
    public string sceneName;
    public TssExternalEquipmentItem[] inventory;
    public TssExternalCableItem[] cableInventory;
    public TssCableType defaultPlayerCableType;
    public ScenarioCableTypeRequirement[] cableTypeRequirements;
    public TssExternalPermanentConnection[] permanentConnections;
    public ScenarioCableScheduleEntry[] cableSchedule;
    public ScenarioNamingEntry[] namingRows;
    public RackScenarioConfig[] racks;
    public ExpectedRackPlacement[] expectedRackPlacements;
    public ExpectedEndpointPlacement[] expectedEndpointPlacements;
    public NetworkScenarioConfig networkScenario;
    public TssBinderReferenceContent binderReference;
}

[Serializable]
public sealed class TssExternalEquipmentItem
{
    public string equipmentId;
    public int quantity;
}

[Serializable]
public sealed class TssExternalCableItem
{
    public string cableId;
    public int quantity;
}

[Serializable]
public sealed class TssExternalPermanentConnection
{
    public string connectionId;
    public string cableId;
    public TssCableType cableType;
    public string patchPanelEndpointId;
    public string wallportEndpointId;
    public bool requiresPunchDownTermination;
    public string endpointATerminationModuleId;
    public string endpointBTerminationModuleId;
}

[Serializable]
public sealed class TssScenarioManifest
{
    public const string Format = "tss-scenario-manifest";
    public string format;
    public int schemaVersion;
    public TssScenarioManifestEntry[] scenarios;
}

[Serializable]
public sealed class TssScenarioManifestEntry
{
    public string scenarioId;
    public string displayName;
    public string facilityId;
    public string catalogId;
    public string jsonFile;
    public string sceneName;
    public string description;
}
