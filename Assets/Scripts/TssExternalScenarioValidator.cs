using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;

public static class TssExternalScenarioValidator
{
    public static bool IsLocalJsonFile(string path) => path != null && Regex.IsMatch(path, @"\A[A-Za-z0-9_-]+\.json\z");

    public static List<string> Validate(TssExternalScenarioData data, TssScenarioCatalog catalog)
    {
        var errors = catalog ? catalog.Validate() : new List<string> { "Scenario catalog is missing." };
        if (data == null) { errors.Add("Scenario JSON is empty."); return errors; }
        if (data.format != TssExternalScenarioData.Format || data.schemaVersion != TssExternalScenarioData.Version)
            errors.Add("Unsupported scenario format or schemaVersion.");
        Required(data.scenarioId, "scenarioId", errors);
        Required(data.displayName, "displayName", errors);
        if (!catalog || data.sceneName != catalog.sceneName) errors.Add("Only the catalog's TSS scene is supported.");
        if (!catalog || errors.Count > 0) return errors;

        CheckEnum(data.defaultPlayerCableType, "defaultPlayerCableType", errors);
        var inventoryIds = NewIds();
        var available = new List<EquipmentDefinition>();
        foreach (var row in Rows(data.inventory, "inventory", errors))
        {
            Unique(row.equipmentId, inventoryIds, "equipmentId", errors);
            var equipment = catalog.FindEquipment(row.equipmentId);
            if (!equipment) errors.Add("Unknown equipmentId: " + row.equipmentId);
            else if (row.quantity > 0) available.Add(equipment);
            if (row.quantity < 0) errors.Add("Equipment quantity must be non-negative.");
        }
        var cableIds = NewIds();
        foreach (var row in Rows(data.cableInventory, "cableInventory", errors))
        {
            Unique(row.cableId, cableIds, "cableId", errors);
            if (!catalog.FindCable(row.cableId)) errors.Add("Unknown cableId: " + row.cableId);
            if (row.quantity < 0) errors.Add("Cable quantity must be non-negative.");
        }

        var racks = new Dictionary<string, RackScenarioConfig>(StringComparer.Ordinal);
        foreach (var rack in Rows(data.racks, "racks", errors))
        {
            Known(rack.rackId, catalog.rackIds, "rackId", errors);
            if (!string.IsNullOrWhiteSpace(rack.rackId) && !racks.TryAdd(rack.rackId, rack)) errors.Add("Duplicate rackId: " + rack.rackId);
            if (rack.lowestU < 1 || rack.highestU > 40 || rack.lowestU > rack.highestU ||
                rack.firstInstallableU < rack.lowestU || rack.lastInstallableU > rack.highestU || rack.firstInstallableU > rack.lastInstallableU)
                errors.Add("Invalid U range for " + rack.rackId);
            if (rack.reservedUPositions == null || rack.reservedUPositions.Any(u => u < 1 || u > 40)) errors.Add("Invalid reserved U positions.");
        }
        var endpoints = new HashSet<string>(catalog.endpointIds, StringComparer.Ordinal);
        var owners = new Dictionary<string, HashSet<EquipmentCategory>>(StringComparer.Ordinal);
        var names = NewIds();
        foreach (var row in Rows(data.expectedRackPlacements, "expectedRackPlacements", errors))
        {
            Unique(row.requiredName, names, "placement name", errors);
            CheckEnum(row.category, "placement category", errors);
            Required(row.phaseLabel, "phaseLabel", errors);
            var end = row.endingU == 0 ? row.startingU : row.endingU;
            if (string.IsNullOrEmpty(row.rackId) || !racks.TryGetValue(row.rackId, out var rack) || row.startingU < rack.firstInstallableU || end > rack.lastInstallableU || end < row.startingU)
            { errors.Add("Invalid placement rack/U range: " + row.requiredName); continue; }
            var matches = available.Where(x => x.category == row.category).ToArray();
            if (matches.Length == 0) errors.Add("No inventory for placement: " + row.requiredName);
            for (var u = row.startingU; u <= end; u++)
            {
                var owner = TssTrainingSession.GetRackOwnerPrefix(row.rackId, u);
                if (!owners.TryGetValue(owner, out var categories)) owners[owner] = categories = new HashSet<EquipmentCategory>();
                categories.Add(row.category);
                foreach (var equipment in matches)
                foreach (var port in equipment.interfaces ?? Array.Empty<EquipmentInterface>())
                    if (port != null && !string.IsNullOrWhiteSpace(port.name)) endpoints.Add(owner + ":" + port.name.Trim());
            }
        }
        foreach (var row in Rows(data.expectedEndpointPlacements, "expectedEndpointPlacements", errors))
        {
            Known(row.stationId, catalog.stationIds, "stationId", errors);
            Unique(row.requiredName, names, "placement name", errors);
            Required(row.phaseLabel, "phaseLabel", errors);
            CheckEnum(row.category, "placement category", errors);
            if (!available.Any(x => x.category == row.category)) errors.Add("No inventory for placement: " + row.requiredName);
        }
        // Reject understocked scenarios rather than leaving an installation impossible to finish.
        foreach (var category in available.Select(x => x.category).Distinct())
        {
            var needed = (data.expectedRackPlacements ?? Array.Empty<ExpectedRackPlacement>()).Count(x => x != null && x.category == category)
                + (data.expectedEndpointPlacements ?? Array.Empty<ExpectedEndpointPlacement>()).Count(x => x != null && x.category == category);
            var supplied = (data.inventory ?? Array.Empty<TssExternalEquipmentItem>()).Where(x => x != null && catalog.FindEquipment(x.equipmentId)?.category == category).Sum(x => (long)x.quantity);
            if (supplied < needed) errors.Add("Insufficient inventory for " + category);
        }

        var requirements = NewIds();
        foreach (var row in Rows(data.cableTypeRequirements, "cableTypeRequirements", errors))
        {
            Unique(row.requirementId, requirements, "requirementId", errors);
            Known(row.endpointAId, endpoints, "endpointAId", errors);
            Known(row.endpointBId, endpoints, "endpointBId", errors);
            CheckEnum(row.requiredCableType, "requiredCableType", errors);
        }
        var connections = NewIds();
        var modules = NewIds();
        foreach (var row in Rows(data.permanentConnections, "permanentConnections", errors))
        {
            Unique(row.connectionId, connections, "connectionId", errors);
            var cable = catalog.FindCable(row.cableId);
            if (!cable) errors.Add("Unknown permanent cableId: " + row.cableId);
            else if (cable.cableType != row.cableType) errors.Add("Permanent cable type does not match " + row.cableId);
            CheckEnum(row.cableType, "cableType", errors);
            Known(row.patchPanelEndpointId, catalog.endpointIds, "permanent endpoint", errors);
            Known(row.wallportEndpointId, catalog.endpointIds, "permanent endpoint", errors);
            if (row.patchPanelEndpointId == row.wallportEndpointId) errors.Add("A connection cannot connect an endpoint to itself.");
            if (row.requiresPunchDownTermination)
            {
                Unique(row.endpointATerminationModuleId, modules, "termination module", errors);
                Unique(row.endpointBTerminationModuleId, modules, "termination module", errors);
            }
        }
        foreach (var row in Rows(data.cableSchedule, "cableSchedule", errors))
        {
            Required(row.fromEndpoint, "cableSchedule.fromEndpoint", errors);
            Required(row.toEndpoint, "cableSchedule.toEndpoint", errors);
            Required(row.fromPort, "cableSchedule.fromPort", errors);
            Required(row.toPort, "cableSchedule.toPort", errors);
            Required(row.cableType, "cableSchedule.cableType", errors);
        }
        foreach (var row in Rows(data.namingRows, "namingRows", errors))
        {
            Known(row.rackId, catalog.rackIds, "naming rackId", errors);
            Required(row.slot, "naming slot", errors);
            Required(row.device, "naming device", errors);
            Required(row.assignedName, "assignedName", errors);
        }
        ValidateNetwork(data.networkScenario, owners, names, available, errors);
        ValidateBinder(data.binderReference, errors);
        return errors;
    }

    private static void ValidateNetwork(NetworkScenarioConfig network, Dictionary<string, HashSet<EquipmentCategory>> owners,
        HashSet<string> names, List<EquipmentDefinition> equipment, List<string> errors)
    {
        if (network == null) { errors.Add("networkScenario is required."); return; }
        // The current generator uses tens from 10..390 and needs four different values.
        if (network.vlanMinimum < 10 || network.vlanMaximum > 390 || network.vlanMinimum % 10 != 0 || network.vlanMaximum % 10 != 0 || network.vlanMaximum - network.vlanMinimum < 30)
            errors.Add("VLAN bounds must be multiples of 10 within 10..390 with at least four values.");
        var networkNames = NewIds();
        foreach (var name in new[] { network.managementVlanName, network.receptionVlanName, network.dataVlanName, network.nativeVlanName, network.deadVlanName })
            Unique(name, networkNames, "VLAN name", errors);
        foreach (var name in network.dhcpNetworkNames ?? Array.Empty<string>())
            Known(name, new[] { network.managementVlanName, network.receptionVlanName, network.dataVlanName }, "DHCP network", errors);
        foreach (var alias in network.endpointAliases ?? Array.Empty<string>()) Known(alias, names, "endpoint alias", errors);
        var roles = new Dictionary<string, NetworkRoleBinding>(StringComparer.OrdinalIgnoreCase);
        var keys = NewIds();
        foreach (var row in Rows(network.roleBindings, "roleBindings", errors))
        {
            Required(row.role, "role", errors);
            Required(row.displayName, "role displayName", errors);
            Unique(row.deviceKey, keys, "role deviceKey", errors);
            if (!string.IsNullOrWhiteSpace(row.role) && !roles.TryAdd(row.role, row)) errors.Add("Duplicate role: " + row.role);
            if (string.IsNullOrEmpty(row.deviceKey) || !owners.TryGetValue(row.deviceKey, out var categories) || !categories.Contains(row.category))
                errors.Add("Unknown role device/category: " + row.deviceKey);
        }
        if (network.enableLayer23Simulation)
        {
            foreach (var role in new[] { "AN-1", "AN-2", "CN-1" })
                if (!roles.TryGetValue(role, out var binding) || binding.category != (role == "CN-1" ? EquipmentCategory.Router : EquipmentCategory.Switch))
                    errors.Add("Missing required network role/category: " + role);
        }
        var ports = NewIds();
        foreach (var row in Rows(network.requiredPorts, "requiredPorts", errors))
        {
            Unique(row.deviceRole + ":" + row.interfaceName, ports, "required port", errors);
            if ((row.deviceRole != "AN-1" && row.deviceRole != "AN-2") || !roles.TryGetValue(row.deviceRole, out var binding))
                errors.Add("Unsupported required-port role: " + row.deviceRole);
            else if (string.IsNullOrEmpty(row.interfaceName) || !Regex.IsMatch(row.interfaceName, @"\A(?:Fa|Gi)0/[1-9][0-9]*\z") || !equipment.Where(x => x.category == binding.category)
                .SelectMany(x => x.interfaces ?? Array.Empty<EquipmentInterface>()).Any(p => p != null && p.name == row.interfaceName.Replace("/", "")))
                errors.Add("Unknown interface: " + row.interfaceName);
            Known(row.endpointId, names, "required-port endpoint", errors);
        }
        if (network.enableLayer23Simulation && (network.requiredPorts == null || network.requiredPorts.Length == 0)) errors.Add("Enabled network requires ports.");
        var evidence = NewIds();
        foreach (var row in Rows(network.evidenceRequirements, "evidenceRequirements", errors))
        {
            Unique(row.evidenceId, evidence, "evidenceId", errors);
            Required(row.command, "evidence command", errors);
            Required(row.description, "evidence description", errors);
            // Only diagnostics reach the current required-evidence recording path.
            if (row.command == null || !Regex.IsMatch(row.command, @"\A(?:ping|tracert) provider\z", RegexOptions.IgnoreCase))
                errors.Add("Evidence command must be ping provider or tracert provider; student addresses are generated at runtime.");
        }
    }

    private static void ValidateBinder(TssBinderReferenceContent binder, List<string> errors)
    {
        if (binder == null) { errors.Add("binderReference is required."); return; }
        Required(binder.namingAndRacks?.sectionTitle, "namingAndRacks.sectionTitle", errors);
        Required(binder.namingAndRacks?.scenarioHeading, "namingAndRacks.scenarioHeading", errors);
        Required(binder.vlans?.sectionTitle, "vlans.sectionTitle", errors);
        Required(binder.vlans?.scenarioHeading, "vlans.scenarioHeading", errors);
        Required(binder.ipPlan?.sectionTitle, "ipPlan.sectionTitle", errors);
        Required(binder.ipPlan?.scenarioHeading, "ipPlan.scenarioHeading", errors);
        Required(binder.cableSchedule?.sectionTitle, "cableSchedule.sectionTitle", errors);
        Required(binder.cableSchedule?.scenarioHeading, "cableSchedule.scenarioHeading", errors);
        foreach (var tab in new[] { binder.circuitLetter, binder.wireless, binder.credentials })
        {
            if (tab == null) { errors.Add("All seven binder sections are required."); continue; }
            Required(tab.sectionTitle, "binder sectionTitle", errors);
            Required(tab.scenarioHeading, "binder scenarioHeading", errors);
            foreach (var row in Rows(tab.rows, "binder rows", errors))
            {
                Required(row.label, "binder row label", errors);
                Required(row.value, "binder row value", errors);
            }
        }
    }

    private static IEnumerable<T> Rows<T>(T[] rows, string field, List<string> errors) where T : class
    {
        if (rows == null) { errors.Add(field + " array is required (use [] when empty)."); yield break; }
        foreach (var row in rows)
            if (row == null) errors.Add(field + " contains a null row."); else yield return row;
    }
    private static HashSet<string> NewIds() => new(StringComparer.OrdinalIgnoreCase);
    private static void Required(string value, string field, List<string> errors)
    {
        if (string.IsNullOrWhiteSpace(value)) errors.Add(field + " is required.");
    }
    private static void Unique(string value, HashSet<string> ids, string field, List<string> errors)
    {
        if (string.IsNullOrWhiteSpace(value) || value != value.Trim() || !ids.Add(value)) errors.Add("Missing, padded or duplicate " + field + ": " + value);
    }
    private static void Known(string value, IEnumerable<string> ids, string field, List<string> errors)
    {
        if (string.IsNullOrWhiteSpace(value) || !ids.Contains(value)) errors.Add("Unknown " + field + ": " + value);
    }
    private static void CheckEnum<T>(T value, string field, List<string> errors) where T : Enum
    {
        if (!Enum.IsDefined(typeof(T), value)) errors.Add("Invalid " + field);
    }
}
