using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

public static class TssScenarioAssessmentCoordinator
{
    public static TssScenarioAssessment Assess(TssTrainingSession session)
    {
        var scenario = session ? session.Scenario : null;
        var workPackages = new List<TssAssessmentWorkPackage>
        {
            AssessPhysical(session),
            AssessNetwork(2, session),
            AssessNetwork(3, session),
            AssessNetwork(4, session)
        };

        return new TssScenarioAssessment(
            scenario ? scenario.scenarioId : string.Empty,
            scenario ? scenario.displayName : string.Empty,
            workPackages.All(x => x.IsComplete),
            workPackages);
    }

    private static TssAssessmentWorkPackage AssessPhysical(TssTrainingSession session)
    {
        var passed = new List<string>();
        var unmet = new List<string>();
        var result = new TssPhysicalWorkPackageEvaluator(session).Evaluate();
        foreach (var condition in result.Conditions)
        {
            if (condition.IsComplete) passed.Add(condition.Label);
            else unmet.Add(condition.Label);
        }
        return new TssAssessmentWorkPackage(1, unmet.Count == 0, passed, unmet);
    }

    private static TssAssessmentWorkPackage AssessNetwork(int workPackage, TssTrainingSession session)
    {
        var passed = new List<string>();
        var unmet = new List<string>();
        if (!session || session.NetworkState == null)
        {
            unmet.Add("Required network state is unavailable.");
            return new TssAssessmentWorkPackage(workPackage, false, passed, unmet);
        }

        var evaluator = new TssNetworkWorkPackageEvaluator(session.NetworkState);
        var result = workPackage switch
        {
            2 => evaluator.EvaluateLayer2(false),
            3 => evaluator.EvaluateLayer3(false),
            _ => evaluator.EvaluateLayer4(false)
        };

        foreach (var condition in result.Conditions)
        {
            if (condition.IsComplete) passed.Add(condition.Label);
            else unmet.Add(condition.Label);
        }

        if (passed.Count == 0 && unmet.Count == 0)
            unmet.Add($"WP{workPackage} has no verifiable current-state criteria.");

        return new TssAssessmentWorkPackage(workPackage, unmet.Count == 0, passed, unmet);
    }
}

public static class TssWorkPackageTitles
{
    public static string Get(int workPackage) => workPackage switch
    {
        1 => "WP1 — Physical Installation",
        2 => "WP1 — Layer 2 Data Link",
        3 => "WP3 — Layer 3",
        4 => "WP4 — Wireless",
        _ => $"WORK PACKAGE {workPackage}"
    };
}

public sealed class TssScenarioAssessment
{
    public readonly string ScenarioId;
    public readonly string ScenarioName;
    public readonly bool IsComplete;
    public readonly IReadOnlyList<TssAssessmentWorkPackage> WorkPackages;

    public TssScenarioAssessment(string scenarioId, string scenarioName, bool isComplete, IReadOnlyList<TssAssessmentWorkPackage> workPackages)
    {
        ScenarioId = scenarioId ?? string.Empty;
        ScenarioName = scenarioName ?? string.Empty;
        IsComplete = isComplete;
        WorkPackages = workPackages ?? Array.Empty<TssAssessmentWorkPackage>();
    }
}

public sealed class TssAssessmentWorkPackage
{
    public readonly int WorkPackage;
    public readonly bool IsComplete;
    public readonly IReadOnlyList<string> CompletedCriteria;
    public readonly IReadOnlyList<string> UnmetCriteria;

    public TssAssessmentWorkPackage(int workPackage, bool isComplete, IReadOnlyList<string> completedCriteria, IReadOnlyList<string> unmetCriteria)
    {
        WorkPackage = workPackage;
        IsComplete = isComplete;
        CompletedCriteria = completedCriteria ?? Array.Empty<string>();
        UnmetCriteria = unmetCriteria ?? Array.Empty<string>();
    }
}

public static class TssResultsHandoff
{
    public static TssResultsSnapshot Snapshot { get; private set; }

    public static TssResultsSnapshot Capture(TssTrainingSession session)
    {
        if (Snapshot != null)
            return Snapshot;

        Snapshot = TssResultsSnapshot.Create(session, TssScenarioAssessmentCoordinator.Assess(session));
        return Snapshot;
    }

    public static void Clear()
    {
        Snapshot = null;
    }
}

public sealed class TssResultsSnapshot
{
    public readonly TssScenarioAssessment Assessment;
    public readonly TssScenarioDataSnapshot Scenario;
    public readonly string StudentId;
    public readonly TssNetworkRunProfileSnapshot NetworkProfile;
    public readonly List<TssInventorySnapshot> Inventory = new();
    public readonly List<TssCableInventorySnapshot> CableInventory = new();
    public readonly List<TssInstalledEquipmentSnapshot> Installed = new();
    public readonly List<TssEndpointPlacementSnapshot> EndpointPlacements = new();
    public readonly List<TssPhysicalConnectionSnapshot> PhysicalConnections = new();
    public readonly List<TssTerminationSnapshot> Terminations = new();
    public readonly TssNetworkStateSnapshot NetworkState;
    public readonly List<TssDeviceConfigurationSnapshot> DeviceConfigurations = new();

    private TssResultsSnapshot(TssTrainingSession session, TssScenarioAssessment assessment)
    {
        Assessment = assessment;
        Scenario = TssScenarioDataSnapshot.Copy(session ? session.Scenario : null);
        StudentId = session ? session.StudentId ?? string.Empty : string.Empty;
        NetworkProfile = TssNetworkRunProfileSnapshot.Copy(session ? session.NetworkProfile : null);

        if (!session)
            return;

        foreach (var item in session.GetScenarioInventory())
            if (item != null)
                Inventory.Add(new TssInventorySnapshot(EquipmentId(item.equipment), item.equipment ? item.equipment.displayName : string.Empty, item.equipment ? item.equipment.category : default, item.quantity, session.GetRemaining(item.equipment)));
        foreach (var item in session.GetScenarioCableInventory())
            if (item != null)
                CableInventory.Add(new TssCableInventorySnapshot(CableId(item.cable), item.cable ? item.cable.displayName : string.Empty, item.cable ? item.cable.cableType : default, item.quantity, session.GetRemaining(item.cable)));
        foreach (var item in session.Installed)
            Installed.Add(new TssInstalledEquipmentSnapshot(item.RackId, EquipmentId(item.Equipment), item.Equipment ? item.Equipment.displayName : string.Empty, item.Equipment ? item.Equipment.category : default, item.StartingU, item.RackUnits, item.DeviceName));
        foreach (var item in session.EndpointPlacements)
            EndpointPlacements.Add(new TssEndpointPlacementSnapshot(item.PlacementId, item.StationId, EquipmentId(item.Equipment), item.Equipment ? item.Equipment.displayName : string.Empty, item.Equipment ? item.Equipment.category : default, item.DeviceName));
        foreach (var item in session.PhysicalConnections)
            PhysicalConnections.Add(new TssPhysicalConnectionSnapshot(item.ConnectionId, CableId(item.Cable), item.CableType, item.EndpointAId, item.EndpointBId, item.IsPermanent));
        foreach (var item in session.GetTerminationModuleStates())
            Terminations.Add(new TssTerminationSnapshot(item, session.IsTerminationCorrectForResults(item.connectionId, item.endpointId)));
        NetworkState = TssNetworkStateSnapshot.Copy(session.NetworkState);
        BuildDeviceConfigurations(session, assessment);
    }

    private void BuildDeviceConfigurations(TssTrainingSession session, TssScenarioAssessment assessment)
    {
        var byKey = new Dictionary<string, TssDeviceConfigurationSnapshot>(StringComparer.OrdinalIgnoreCase);
        var bindings = session.Scenario?.networkScenario?.roleBindings ?? Array.Empty<NetworkRoleBinding>();
        foreach (var binding in bindings)
        {
            if (binding == null || !IsConfigurable(binding.category)) continue;
            var key = binding.deviceKey ?? string.Empty;
            var installed = session.Installed.FirstOrDefault(x => x.Equipment && x.Equipment.category == binding.category &&
                (TssTrainingSession.GetRackOwnerPrefix(x.RackId, x.StartingU).Equals(key, StringComparison.OrdinalIgnoreCase) || x.DeviceName.Equals(binding.role, StringComparison.OrdinalIgnoreCase)));
            var actualKey = installed.Equipment ? TssTrainingSession.GetRackOwnerPrefix(installed.RackId, installed.StartingU) : key;
            var device = session.NetworkState?.Devices.FirstOrDefault(x => x.Key.Equals(actualKey, StringComparison.OrdinalIgnoreCase) || string.Equals(x.ExpectedRole, binding.role, StringComparison.OrdinalIgnoreCase));
            AddDevice(byKey, actualKey, binding.category, binding.role, device?.Hostname ?? installed.DeviceName ?? binding.displayName ?? binding.role,
                installed.Equipment ? installed.RackId + " U" + installed.StartingU : string.Empty, installed.Equipment, device);
            if (byKey.TryGetValue(actualKey, out var expected)) expected.ExpectedOrder = Math.Min(expected.ExpectedOrder, Array.IndexOf(bindings, binding));
        }
        foreach (var installed in session.Installed)
        {
            if (!installed.Equipment || !IsConfigurable(installed.Equipment.category)) continue;
            var key = TssTrainingSession.GetRackOwnerPrefix(installed.RackId, installed.StartingU);
            var device = session.NetworkState?.Devices.FirstOrDefault(x => x.Key.Equals(key, StringComparison.OrdinalIgnoreCase));
            AddDevice(byKey, key, installed.Equipment.category, ExpectedRoleFor(bindings, key, device), device?.Hostname ?? installed.DeviceName,
                installed.RackId + " U" + installed.StartingU, true, device);
        }
        foreach (var device in session.NetworkState?.Devices ?? Array.Empty<NetworkDeviceState>())
        {
            if (!IsConfigurable(device.Category)) continue;
            AddDevice(byKey, device.Key, device.Category, ExpectedRoleFor(bindings, device.Key, device), device.Hostname, string.Empty, false, device);
        }
        DeviceConfigurations.AddRange(byKey.Values.OrderBy(x => x.ExpectedOrder).ThenBy(x => CategoryOrder(x.Category)).ThenBy(x => x.StableKey, StringComparer.OrdinalIgnoreCase));
        foreach (var device in DeviceConfigurations)
        {
            if (device.IsInstalled && !device.HasTerminalState)
            {
                device.Configuration = CreateStartingConfiguration(device, session.NetworkState?.Profile);
                device.HasTerminalState = device.Configuration != null;
                device.IsStartingConfiguration = device.HasTerminalState;
            }
            AddConfigurationWarnings(device, assessment);
        }
    }

    private static NetworkDeviceState CreateStartingConfiguration(TssDeviceConfigurationSnapshot snapshot, TssNetworkRunProfile profile)
    {
        if (snapshot == null || profile == null) return null;
        var device = new NetworkDeviceState(snapshot.StableKey, snapshot.Category, snapshot.Hostname, profile) { ExpectedRole = snapshot.ExpectedRole };
        if (snapshot.Category == EquipmentCategory.Switch)
        {
            for (var port = 1; port <= 24; port++)
                device.Interfaces["Fa0/" + port] = new NetworkInterfaceState { Name = "Fa0/" + port, AdminUp = true, CarrierUp = false, AccessVlan = 1 };
        }
        else if (snapshot.Category == EquipmentCategory.Router)
        {
            device.Interfaces["Fa0/0"] = new NetworkInterfaceState { Name = "Fa0/0", AdminUp = false, CarrierUp = false };
            device.Interfaces["Fa0/1"] = new NetworkInterfaceState { Name = "Fa0/1", AdminUp = false, CarrierUp = false };
        }
        return device;
    }

    private static void AddConfigurationWarnings(TssDeviceConfigurationSnapshot device, TssScenarioAssessment assessment)
    {
        if (device == null || assessment == null || !device.HasTerminalState) return;
        if (device.IsInstalled && string.IsNullOrWhiteSpace(device.ExpectedRole))
            device.PossibleIncorrectConfiguration.Add("Extra device is not part of the expected scenario roster.");
        foreach (var workPackage in assessment.WorkPackages)
        {
            if (workPackage.WorkPackage == 2 && device.Category != EquipmentCategory.Switch) continue;
            if (workPackage.WorkPackage == 3 && device.Category != EquipmentCategory.Router) continue;
            if (workPackage.WorkPackage != 2 && workPackage.WorkPackage != 3) continue;
            foreach (var criterion in workPackage.UnmetCriteria ?? Array.Empty<string>())
            {
                if (workPackage.WorkPackage == 2 && !MentionsDevice(criterion, device)) continue;
                device.PossibleIncorrectConfiguration.Add(criterion);
            }
        }
    }

    private static bool MentionsDevice(string criterion, TssDeviceConfigurationSnapshot device)
    {
        return (!string.IsNullOrWhiteSpace(device.ExpectedRole) && criterion.IndexOf(device.ExpectedRole, StringComparison.OrdinalIgnoreCase) >= 0)
            || (!string.IsNullOrWhiteSpace(device.Hostname) && criterion.IndexOf(device.Hostname, StringComparison.OrdinalIgnoreCase) >= 0)
            || (!string.IsNullOrWhiteSpace(device.StableKey) && criterion.IndexOf(device.StableKey, StringComparison.OrdinalIgnoreCase) >= 0);
    }

    private static void AddDevice(Dictionary<string, TssDeviceConfigurationSnapshot> byKey, string key, EquipmentCategory category, string role, string hostname, string location, bool installed, NetworkDeviceState device)
    {
        if (string.IsNullOrWhiteSpace(key)) return;
        if (!byKey.TryGetValue(key, out var item)) byKey[key] = item = new TssDeviceConfigurationSnapshot(key, category, role, hostname, location, installed, device != null, device?.Clone());
        else { item.IsInstalled |= installed; if (device != null) { item.HasTerminalState = true; item.Configuration = device.Clone(); item.Hostname = device.Hostname; } }
    }

    private static bool IsConfigurable(EquipmentCategory category) => category is EquipmentCategory.Switch or EquipmentCategory.Router;
    private static int CategoryOrder(EquipmentCategory category) => category == EquipmentCategory.Switch ? 0 : 1;
    private static string ExpectedRoleFor(NetworkRoleBinding[] bindings, string key, NetworkDeviceState device)
    {
        foreach (var binding in bindings)
            if (binding != null && (string.Equals(binding.deviceKey, key, StringComparison.OrdinalIgnoreCase) || string.Equals(binding.role, device?.ExpectedRole, StringComparison.OrdinalIgnoreCase)))
                return binding.role ?? string.Empty;
        return string.Empty;
    }

    public static TssResultsSnapshot Create(TssTrainingSession session, TssScenarioAssessment assessment) => new(session, assessment);

    private static string EquipmentId(EquipmentDefinition definition) => definition ? (!string.IsNullOrWhiteSpace(definition.equipmentId) ? definition.equipmentId : definition.name) : string.Empty;
    private static string CableId(TssCableDefinition definition) => definition ? (!string.IsNullOrWhiteSpace(definition.cableId) ? definition.cableId : definition.name) : string.Empty;
}

public sealed class TssScenarioDataSnapshot
{
    public string ScenarioId;
    public string DisplayName;
    public string FacilityId;
    public string CatalogId;
    public string SceneName;
    public List<TssExpectedRackSnapshot> ExpectedRacks = new();
    public List<TssExpectedEndpointSnapshot> ExpectedEndpoints = new();
    public TssNetworkScenarioSnapshot NetworkScenario;

    public static TssScenarioDataSnapshot Copy(TssScenarioDefinition scenario)
    {
        if (!scenario) return null;
        var copy = new TssScenarioDataSnapshot
        {
            ScenarioId = scenario.scenarioId,
            DisplayName = scenario.displayName,
            FacilityId = scenario.facilityId,
            CatalogId = scenario.catalogId,
            SceneName = scenario.sceneName,
            NetworkScenario = TssNetworkScenarioSnapshot.Copy(scenario.networkScenario)
        };
        foreach (var item in scenario.expectedRackPlacements ?? Array.Empty<ExpectedRackPlacement>())
            if (item != null) copy.ExpectedRacks.Add(new TssExpectedRackSnapshot(item));
        foreach (var item in scenario.expectedEndpointPlacements ?? Array.Empty<ExpectedEndpointPlacement>())
            if (item != null) copy.ExpectedEndpoints.Add(new TssExpectedEndpointSnapshot(item));
        return copy;
    }
}

public sealed class TssExpectedRackSnapshot
{
    public string PhaseLabel, RackId, RequiredName;
    public int StartingU, EndingU;
    public EquipmentCategory Category;
    public TssExpectedRackSnapshot(ExpectedRackPlacement source) { PhaseLabel = source.phaseLabel; RackId = source.rackId; RequiredName = source.requiredName; StartingU = source.startingU; EndingU = source.endingU; Category = source.category; }
}

public sealed class TssExpectedEndpointSnapshot
{
    public string PhaseLabel, StationId, RequiredName;
    public EquipmentCategory Category;
    public TssExpectedEndpointSnapshot(ExpectedEndpointPlacement source) { PhaseLabel = source.phaseLabel; StationId = source.stationId; RequiredName = source.requiredName; Category = source.category; }
}

public sealed class TssNetworkScenarioSnapshot
{
    public bool Enabled;
    public int ProfileSeed;
    public string CircuitId, Ssid, WifiPassphrase, ApAdminPassword, EnableSecret, ConsolePassword, VtyPassword, Motd;
    public int WirelessChannel;
    public string ManagementVlanName, ReceptionVlanName, DataVlanName, NativeVlanName, DeadVlanName;
    public string[] DhcpNetworkNames = Array.Empty<string>();

    public static TssNetworkScenarioSnapshot Copy(NetworkScenarioConfig source)
    {
        if (source == null) return null;
        return new TssNetworkScenarioSnapshot
        {
            Enabled = source.enableLayer23Simulation,
            ProfileSeed = source.profileSeed,
            CircuitId = source.circuitId,
            Ssid = source.ssid,
            WifiPassphrase = source.wifiPassphrase,
            ApAdminPassword = source.apAdminPassword,
            EnableSecret = source.enableSecret,
            ConsolePassword = source.consolePassword,
            VtyPassword = source.vtyPassword,
            Motd = source.motd,
            WirelessChannel = source.wirelessChannel,
            ManagementVlanName = source.managementVlanName,
            ReceptionVlanName = source.receptionVlanName,
            DataVlanName = source.dataVlanName,
            NativeVlanName = source.nativeVlanName,
            DeadVlanName = source.deadVlanName,
            DhcpNetworkNames = (source.dhcpNetworkNames ?? Array.Empty<string>()).ToArray()
        };
    }
}

public sealed class TssNetworkRunProfileSnapshot
{
    public int Seed, ManagementVlan, ReceptionVlan, DataVlan, NativeVlan, DeadVlan, WirelessChannel;
    public string StudentId, ManagementNetwork, ReceptionNetwork, DataNetwork, WanCustomerAddress, WanProviderAddress, WanNetwork, CircuitId, EnableSecret, ConsolePassword, VtyPassword, Motd, Ssid, WifiPassphrase, ApAdminPassword, ApManagementAddress, TestLaptopAddress;
    public readonly Dictionary<string, string> EndpointAddresses = new(StringComparer.OrdinalIgnoreCase);
    public readonly Dictionary<string, string> EndpointAliases = new(StringComparer.OrdinalIgnoreCase);
    public readonly HashSet<string> DhcpNetworkNames = new(StringComparer.OrdinalIgnoreCase);

    public static TssNetworkRunProfileSnapshot Copy(TssNetworkRunProfile source)
    {
        if (source == null) return null;
        var copy = new TssNetworkRunProfileSnapshot
        {
            Seed = source.Seed, StudentId = source.StudentId, ManagementVlan = source.ManagementVlan, ReceptionVlan = source.ReceptionVlan, DataVlan = source.DataVlan, NativeVlan = source.NativeVlan, DeadVlan = source.DeadVlan,
            ManagementNetwork = source.ManagementNetwork, ReceptionNetwork = source.ReceptionNetwork, DataNetwork = source.DataNetwork, WanCustomerAddress = source.WanCustomerAddress, WanProviderAddress = source.WanProviderAddress, WanNetwork = source.WanNetwork,
            CircuitId = source.CircuitId, EnableSecret = source.EnableSecret, ConsolePassword = source.ConsolePassword, VtyPassword = source.VtyPassword, Motd = source.Motd, Ssid = source.Ssid, WifiPassphrase = source.WifiPassphrase, ApAdminPassword = source.ApAdminPassword,
            WirelessChannel = source.WirelessChannel, ApManagementAddress = source.ApManagementAddress, TestLaptopAddress = source.TestLaptopAddress
        };
        foreach (var item in source.EndpointAddresses) copy.EndpointAddresses[item.Key] = item.Value;
        foreach (var item in source.EndpointAliases) copy.EndpointAliases[item.Key] = item.Value;
        foreach (var item in source.DhcpNetworkNames) copy.DhcpNetworkNames.Add(item);
        return copy;
    }
}

public sealed class TssInventorySnapshot
{
    public string EquipmentId, DisplayName;
    public EquipmentCategory Category;
    public int Quantity, Remaining;
    public TssInventorySnapshot(string id, string displayName, EquipmentCategory category, int quantity, int remaining) { EquipmentId = id; DisplayName = displayName; Category = category; Quantity = quantity; Remaining = remaining; }
}

public sealed class TssDeviceConfigurationSnapshot
{
    public readonly string StableKey;
    public readonly EquipmentCategory Category;
    public readonly string ExpectedRole;
    public string Hostname;
    public readonly string Location;
    public bool IsInstalled;
    public bool HasTerminalState;
    public bool IsStartingConfiguration;
    public NetworkDeviceState Configuration;
    public readonly List<string> PossibleIncorrectConfiguration = new();
    public int ExpectedOrder = int.MaxValue;
    public TssDeviceConfigurationSnapshot(string key, EquipmentCategory category, string role, string hostname, string location, bool installed, bool recorded, NetworkDeviceState configuration)
    { StableKey = key; Category = category; ExpectedRole = role ?? string.Empty; Hostname = hostname ?? string.Empty; Location = location ?? string.Empty; IsInstalled = installed; HasTerminalState = recorded; Configuration = configuration; }
    public string Label => (ExpectedRole.Length > 0 ? (IsInstalled && !string.Equals(ExpectedRole, Hostname, StringComparison.OrdinalIgnoreCase) ? ExpectedRole + " / " + Hostname : ExpectedRole) : Hostname);
    public string StateMessage => !IsInstalled ? "This scenario device is not racked/installed or configured." : !HasTerminalState ? "No terminal configuration was recorded for this device." : string.Empty;
}

public sealed class TssCableInventorySnapshot
{
    public string CableId, DisplayName;
    public TssCableType CableType;
    public int Quantity, Remaining;
    public TssCableInventorySnapshot(string id, string displayName, TssCableType cableType, int quantity, int remaining) { CableId = id; DisplayName = displayName; CableType = cableType; Quantity = quantity; Remaining = remaining; }
}

public sealed class TssInstalledEquipmentSnapshot
{
    public string RackId, EquipmentId, DisplayName, DeviceName;
    public EquipmentCategory Category;
    public int StartingU, RackUnits;
    public TssInstalledEquipmentSnapshot(string rackId, string equipmentId, string displayName, EquipmentCategory category, int startingU, int rackUnits, string deviceName) { RackId = rackId; EquipmentId = equipmentId; DisplayName = displayName; Category = category; StartingU = startingU; RackUnits = rackUnits; DeviceName = deviceName; }
}

public sealed class TssEndpointPlacementSnapshot
{
    public string PlacementId, StationId, EquipmentId, DisplayName, DeviceName;
    public EquipmentCategory Category;
    public TssEndpointPlacementSnapshot(string placementId, string stationId, string equipmentId, string displayName, EquipmentCategory category, string deviceName) { PlacementId = placementId; StationId = stationId; EquipmentId = equipmentId; DisplayName = displayName; Category = category; DeviceName = deviceName; }
}

public sealed class TssPhysicalConnectionSnapshot
{
    public string ConnectionId, CableId, EndpointAId, EndpointBId;
    public TssCableType CableType;
    public bool IsPermanent;
    public TssPhysicalConnectionSnapshot(string connectionId, string cableId, TssCableType cableType, string endpointAId, string endpointBId, bool isPermanent) { ConnectionId = connectionId; CableId = cableId; CableType = cableType; EndpointAId = endpointAId; EndpointBId = endpointBId; IsPermanent = isPermanent; }
}

public sealed class TssTerminationSnapshot
{
    public string ConnectionId, EndpointId, ModuleId, EndpointRole;
    public bool IsCorrect;
    public readonly List<TssTerminationTerminalSnapshot> Terminals = new();
    public TssTerminationSnapshot(TssTerminationModuleState source, bool isCorrect)
    {
        ConnectionId = source?.connectionId; EndpointId = source?.endpointId; ModuleId = source?.moduleId; EndpointRole = source?.endpointRole; IsCorrect = isCorrect;
        foreach (var terminal in source?.terminals ?? Array.Empty<TssTerminationTerminalState>())
            if (terminal != null) Terminals.Add(new TssTerminationTerminalSnapshot(terminal));
    }
}

public sealed class TssTerminationTerminalSnapshot
{
    public int Pin, OperationOrder, RotationSteps;
    public TssConductorColor Conductor;
    public bool HasConductor, IsPunched, Flipped;
    public TssTerminationTerminalSnapshot(TssTerminationTerminalState source) { Pin = source.pin; OperationOrder = source.operationOrder; RotationSteps = source.rotationSteps; Conductor = source.conductor; HasConductor = source.hasConductor; IsPunched = source.isPunched; Flipped = source.flipped; }
}

public sealed class TssNetworkStateSnapshot
{
    public TssNetworkRunProfileSnapshot Profile;
    public readonly List<NetworkDeviceState> Devices = new();
    public readonly List<EndpointNetworkState> Endpoints = new();
    public readonly List<string> Evidence = new();
    public readonly List<TssCompletionHistorySnapshot> CompletionHistory = new();
    public bool ProviderDemarcConnected;
    public bool AccessPointUplinkConnected, AccessPointConfigured;
    public string AccessPointSsid, AssociatedEndpointKey, AssociatedSsid;

    public static TssNetworkStateSnapshot Copy(TssNetworkSessionState source)
    {
        if (source == null) return null;
        var copy = new TssNetworkStateSnapshot { Profile = TssNetworkRunProfileSnapshot.Copy(source.Profile), ProviderDemarcConnected = source.ProviderDemarcConnected, AccessPointUplinkConnected = source.AccessPoint.UplinkConnected, AccessPointConfigured = source.AccessPoint.Configured, AccessPointSsid = source.AccessPoint.Ssid, AssociatedEndpointKey = source.AccessPoint.AssociatedEndpointKey, AssociatedSsid = source.AccessPoint.AssociatedSsid };
        foreach (var device in source.Devices) copy.Devices.Add(device.Clone());
        foreach (var endpoint in source.Endpoints) copy.Endpoints.Add(endpoint.Clone());
        copy.Evidence.AddRange(source.Evidence);
        foreach (var item in source.CompletionSnapshots.Values)
            copy.CompletionHistory.Add(new TssCompletionHistorySnapshot(item));
        return copy;
    }
}

public sealed class TssCompletionHistorySnapshot
{
    public string WorkPackage;
    public int ProfileSeed, CompletionOrder;
    public bool ProviderDemarcConnected, AccessPointUplinkConnected, AccessPointConfigured;
    public string AccessPointSsid, AssociatedEndpointKey, AssociatedSsid;
    public readonly List<NetworkDeviceState> Devices = new();
    public readonly List<EndpointNetworkState> Endpoints = new();
    public readonly List<string> Evidence = new();
    public TssCompletionHistorySnapshot(TssNetworkCompletionSnapshot source)
    {
        WorkPackage = source.WorkPackage; ProfileSeed = source.ProfileSeed; CompletionOrder = source.CompletionOrder;
        ProviderDemarcConnected = source.State.ProviderDemarcConnected;
        AccessPointUplinkConnected = source.State.AccessPoint.UplinkConnected;
        AccessPointConfigured = source.State.AccessPoint.Configured;
        AccessPointSsid = source.State.AccessPoint.Ssid;
        AssociatedEndpointKey = source.State.AccessPoint.AssociatedEndpointKey;
        AssociatedSsid = source.State.AccessPoint.AssociatedSsid;
        foreach (var device in source.State.Devices.Values) Devices.Add(device.Clone());
        foreach (var endpoint in source.State.Endpoints.Values) Endpoints.Add(endpoint.Clone());
        Evidence.AddRange(source.State.Evidence);
    }
}
