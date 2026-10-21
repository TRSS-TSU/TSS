using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

public sealed class TssExternalScenarioTests
{
    private TssScenarioCatalog catalog;
    private TssScenarioDefinition baseline;

    [SetUp]
    public void SetUp()
    {
        catalog = Resources.Load<TssScenarioCatalog>(TssScenarioCatalog.ResourcePath);
        baseline = AssetDatabase.LoadAssetAtPath<TssScenarioDefinition>("Assets/Data/TSS/Scenarios/Balanced_12_Piece_Lab.asset");
    }

    private TssExternalScenarioData BaselineData() => TssExternalScenarioLoader.Export(baseline);

    [TestCase("balanced-lab.json")]
    [TestCase("installation-practice.json")]
    [TestCase("network-verification.json")]
    [TestCase("wireless-readiness.json")]
    public void SampleJsonResolvesIntoIndependentRuntimeDefinition(string file)
    {
        var data = JsonUtility.FromJson<TssExternalScenarioData>(File.ReadAllText(Path.Combine(Application.streamingAssetsPath, "Scenarios", file)));
        Assert.IsTrue(TssExternalScenarioLoader.TryCreate(data, catalog, out var runtime, out var errors), string.Join("\n", errors));
        try
        {
            Assert.IsTrue(runtime.IsExternalRuntime);
            Assert.IsFalse(EditorUtility.IsPersistent(runtime));
            Assert.AreEqual(data.scenarioId, runtime.scenarioId);
            Assert.AreEqual(JsonUtility.ToJson(data.binderReference), JsonUtility.ToJson(runtime.binderReference));
            Assert.AreEqual(JsonUtility.ToJson(data.networkScenario), JsonUtility.ToJson(runtime.networkScenario));
            Assert.AreSame(catalog.FindEquipment(data.inventory[0].equipmentId), runtime.inventory[0].equipment);
            Assert.AreSame(catalog.FindCable(data.cableInventory[0].cableId), runtime.cableInventory[0].cable);
            Assert.AreEqual(data.permanentConnections.Length, runtime.permanentConnections.Length);
            Assert.AreSame(catalog.FindCable(data.permanentConnections[0].cableId), runtime.permanentConnections[0].cable);
            runtime.binderReference.wireless.rows[0].value = "changed";
            Assert.AreNotEqual("changed", data.binderReference.wireless.rows[0].value);
            Assert.AreNotEqual("changed", baseline.binderReference.wireless.rows[0].value);
        }
        finally { TssScenarioLaunchState.ReleaseRuntime(runtime); }
    }

    [Test]
    public void BaselineExportPreservesEveryAuthoredValue()
    {
        var before = JsonUtility.ToJson(baseline);
        var data = BaselineData();
        Assert.IsTrue(TssExternalScenarioLoader.TryCreate(data, catalog, out var runtime, out var errors), string.Join("\n", errors));
        try
        {
            Assert.AreEqual(JsonUtility.ToJson(data), JsonUtility.ToJson(TssExternalScenarioLoader.Export(runtime)));
            Assert.AreEqual(before, JsonUtility.ToJson(baseline));
            StringAssert.DoesNotContain("instanceID", JsonUtility.ToJson(data));
            StringAssert.DoesNotContain("guid", JsonUtility.ToJson(data));
        }
        finally { TssScenarioLaunchState.ReleaseRuntime(runtime); }
    }

    [TestCase("equipment")]
    [TestCase("cable")]
    [TestCase("permanentCable")]
    [TestCase("rack")]
    [TestCase("station")]
    [TestCase("endpoint")]
    [TestCase("connectionDuplicate")]
    [TestCase("evidenceDuplicate")]
    [TestCase("vlan")]
    [TestCase("role")]
    [TestCase("port")]
    [TestCase("binder")]
    [TestCase("schema")]
    [TestCase("identity")]
    [TestCase("quantity")]
    [TestCase("nullRow")]
    [TestCase("enum")]
    public void InvalidDataDoesNotCreateAnyRuntimeScenario(string fault)
    {
        var data = BaselineData();
        switch (fault)
        {
            case "equipment": data.inventory[0].equipmentId = "unknown"; break;
            case "cable": data.cableInventory[0].cableId = "unknown"; break;
            case "permanentCable": data.permanentConnections[0].cableId = "unknown"; break;
            case "rack": data.expectedRackPlacements[0].rackId = "unknown"; break;
            case "station": data.expectedEndpointPlacements[0].stationId = "unknown"; break;
            case "endpoint": data.permanentConnections[0].wallportEndpointId = "unknown"; break;
            case "connectionDuplicate": data.permanentConnections[1].connectionId = data.permanentConnections[0].connectionId; break;
            case "evidenceDuplicate": data.networkScenario.evidenceRequirements = new[] { data.networkScenario.evidenceRequirements[0], data.networkScenario.evidenceRequirements[0] }; break;
            case "vlan": data.networkScenario.vlanMaximum = 20; break;
            case "role": data.networkScenario.roleBindings[0].role = "unknown"; break;
            case "port": data.networkScenario.requiredPorts[0].interfaceName = "Fa0/99"; break;
            case "binder": data.binderReference.credentials = null; break;
            case "schema": data.schemaVersion = 999; break;
            case "identity": data.scenarioId = " "; break;
            case "quantity": data.inventory[0].quantity = -1; break;
            case "nullRow": data.cableSchedule[0] = null; break;
            case "enum": data.defaultPlayerCableType = (TssCableType)999; break;
        }
        Assert.IsFalse(TssExternalScenarioLoader.TryCreate(data, catalog, out var runtime, out var errors));
        Assert.IsNull(runtime);
        Assert.IsNotEmpty(errors);
    }

    [Test]
    public void CatalogRejectsDuplicateAndMissingAssetIds()
    {
        var copy = UnityEngine.Object.Instantiate(catalog);
        try
        {
            copy.equipment = new[] { catalog.equipment[0], catalog.equipment[0], null };
            copy.cables = new[] { catalog.cables[0], catalog.cables[0], null };
            Assert.AreEqual(4, copy.Validate().Count);
        }
        finally { UnityEngine.Object.DestroyImmediate(copy); }
    }

    [Test]
    public void DuplicateScenarioIdsExcludeBothEntries()
    {
        var manifest = JsonUtility.FromJson<TssScenarioManifest>(File.ReadAllText(Path.Combine(Application.streamingAssetsPath, "Scenarios/manifest.json")));
        manifest.scenarios[1].scenarioId = manifest.scenarios[0].scenarioId.ToLowerInvariant();
        var warnings = new List<string>();
        var filtered = TssExternalScenarioLoader.ParseManifest(JsonUtility.ToJson(manifest), warnings);
        Assert.AreEqual(2, filtered.scenarios.Length);
        Assert.AreEqual(2, warnings.Count);
    }

    [TestCase("../outside.json")]
    [TestCase("https://example.com/scenario.json")]
    [TestCase("C:\\outside.json")]
    [TestCase("%2e%2e.json")]
    [TestCase("scenario.json?query")]
    public void ManifestPathsStayInsideScenarioFolder(string path) => Assert.IsFalse(TssExternalScenarioValidator.IsLocalJsonFile(path));

    [TestCase("{}")]
    [TestCase("{\"format\":\"tss-scenario-manifest\",\"schemaVersion\":1,\"scenarios\":[]}")]
    public void MissingOrWrongLengthManifestIsRejected(string json) => Assert.Throws<InvalidDataException>(() => TssExternalScenarioLoader.ParseManifest(json, new List<string>()));

    [Test]
    public void NetworkProfilesRemainDeterministicAndScenarioSpecific()
    {
        Assert.IsTrue(TssScenarioLaunchState.TryCreateStudentId("  Smith ", "017", out var student));
        Assert.AreEqual("SMITH-17", student);
        var a = TssNetworkRunProfile.Create(TssNetworkRunProfile.DeriveSeed(student, "BALANCED_12_PIECE_LAB"), baseline.networkScenario, student);
        var b = TssNetworkRunProfile.Create(TssNetworkRunProfile.DeriveSeed(student, "BALANCED_12_PIECE_LAB"), baseline.networkScenario, student);
        var c = TssNetworkRunProfile.Create(TssNetworkRunProfile.DeriveSeed(student, "NETWORK_VERIFICATION"), baseline.networkScenario, student);
        Assert.AreEqual(a.Seed, b.Seed);
        Assert.AreEqual(a.ManagementVlan, b.ManagementVlan);
        Assert.AreEqual(a.WanProviderAddress, b.WanProviderAddress);
        Assert.AreEqual(a.EnableSecret, b.EnableSecret);
        CollectionAssert.AreEquivalent(a.EndpointAddresses, b.EndpointAddresses);
        Assert.AreNotEqual(a.Seed, c.Seed);
        Assert.AreNotEqual(a.WanProviderAddress + a.EnableSecret, c.WanProviderAddress + c.EnableSecret);
    }

    [Test]
    public void FallbackSelectionAndHandoffPreserveAuthoredAsset()
    {
        var choices = TssExternalScenarioLoader.GetFallbackScenarios(new[] { null, baseline, baseline }, null);
        Assert.AreEqual(1, choices.Length);
        Assert.AreSame(baseline, choices[0]);
        Assert.AreSame(baseline, TssExternalScenarioLoader.GetFallbackScenarios(null, baseline).Single());
        TssScenarioLaunchState.Select(baseline, "SMITH-17");
        Assert.AreSame(baseline, TssScenarioLaunchState.ConsumeSelectedScenario(out var student));
        Assert.AreEqual("SMITH-17", student);
        Assert.IsNull(TssScenarioLaunchState.ConsumeSelectedScenario(out _));
        TssScenarioLaunchState.ReleaseRuntime(baseline);
        Assert.IsTrue(baseline);
    }

    [TestCase(32, "CN-1", "Rack01", EquipmentCategory.Router, true)]
    [TestCase(34, "CN-1", "Rack01", EquipmentCategory.Router, true)]
    [TestCase(35, "CN-1", "Rack01", EquipmentCategory.Router, false)]
    [TestCase(32, "WrongName", "Rack01", EquipmentCategory.Router, false)]
    [TestCase(32, "CN-1", "Rack02", EquipmentCategory.Router, false)]
    [TestCase(32, "CN-1", "Rack01", EquipmentCategory.Switch, false)]
    public void CableRequirementsFollowOnlyTheCorrectDeviceWithinItsAuthoredRange(int u, string name, string rack, EquipmentCategory category, bool resolves)
    {
        var data = BaselineData();
        var requirement = data.cableTypeRequirements.Single(x => x.requirementId == "DemarcWan");
        requirement.requiredCableType = TssCableType.Crossover;
        Assert.IsTrue(TssExternalScenarioLoader.TryCreate(data, catalog, out var runtime, out var errors), string.Join("\n", errors));
        var go = new GameObject("Scenario requirement test");
        try
        {
            var session = go.AddComponent<TssTrainingSession>();
            session.BeginScenario(runtime, "SMITH-17");
            var equipment = catalog.equipment.First(x => x.category == category);
            ((List<InstalledEquipmentRecord>)session.Installed).Add(new InstalledEquipmentRecord(rack, equipment, u, equipment.rackUnits, name));
            var status = session.GetCableTypeStatuses().Single(x => x.ConnectionId == "DemarcWan");
            var actualPort = TssTrainingSession.GetRackOwnerPrefix(rack, u) + ":Fa01";
            Assert.AreEqual(resolves ? actualPort : requirement.endpointAId, status.EndpointAId);
            var cable = catalog.cables.First(x => x.cableType == TssCableType.Crossover);
            Assert.AreEqual(resolves, session.IsCableTypeCorrect(new TssPhysicalConnectionRecord("test", cable, cable.cableType, actualPort, requirement.endpointBId, null)));
            Assert.AreEqual(requirement.endpointAId, runtime.cableTypeRequirements.Single(x => x.requirementId == "DemarcWan").endpointAId);
        }
        finally
        {
            UnityEngine.Object.DestroyImmediate(go);
            TssScenarioLaunchState.ReleaseRuntime(runtime);
        }
    }

    [Test]
    public void ReplacedRuntimeSelectionIsReleasedButConsumedSelectionSurvives()
    {
        Assert.IsTrue(TssExternalScenarioLoader.TryCreate(BaselineData(), catalog, out var first, out _));
        Assert.IsTrue(TssExternalScenarioLoader.TryCreate(BaselineData(), catalog, out var second, out _));
        TssScenarioLaunchState.Select(first);
        TssScenarioLaunchState.Select(second, "SMITH-17");
        Assert.IsFalse(first);
        Assert.AreSame(second, TssScenarioLaunchState.ConsumeSelectedScenario(out _));
        Assert.IsTrue(second);
        TssScenarioLaunchState.ReleaseRuntime(second);
    }
}
