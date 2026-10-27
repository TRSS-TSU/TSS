using NUnit.Framework;
using System.Linq;
using UnityEngine;
using Object = UnityEngine.Object;

public sealed class TssScenarioResultsTests
{
    [Test]
    public void ExitRecheckIgnoresHistoricalNetworkCompletionSnapshots()
    {
        var state = new TssNetworkSessionState(TssNetworkRunProfile.Create(42, new NetworkScenarioConfig()));
        state.SaveCompletionSnapshot("WP2");

        var result = new TssNetworkWorkPackageEvaluator(state).EvaluateLayer2(false);

        Assert.IsFalse(result.Complete);
        Assert.IsNull(result.CompletionSnapshot);
        Assert.IsNotEmpty(result.UnmetCategories);
    }

    [Test]
    public void MissingNetworkStateMakesAssessmentIncomplete()
    {
        var scenario = ScriptableObject.CreateInstance<TssScenarioDefinition>();
        var sessionObject = new GameObject("ResultsSession");
        var session = sessionObject.AddComponent<TssTrainingSession>();
        session.BeginScenario(scenario, "STUDENT-1");

        try
        {
            var assessment = TssScenarioAssessmentCoordinator.Assess(session);

            Assert.IsFalse(assessment.IsComplete);
            Assert.IsFalse(assessment.WorkPackages[1].IsComplete);
            Assert.IsFalse(assessment.WorkPackages[2].IsComplete);
            Assert.IsFalse(assessment.WorkPackages[3].IsComplete);
            StringAssert.Contains("network state", assessment.WorkPackages[1].UnmetCriteria[0]);
        }
        finally
        {
            Object.DestroyImmediate(sessionObject);
            Object.DestroyImmediate(scenario);
        }
    }

    [Test]
    public void PhysicalFailuresStayInWp1WithoutNetworkState()
    {
        var scenario = ScriptableObject.CreateInstance<TssScenarioDefinition>();
        scenario.cableTypeRequirements = new[]
        {
            new ScenarioCableTypeRequirement { requirementId = "RequiredCable", endpointAId = "A", endpointBId = "B", requiredCableType = TssCableType.Crossover }
        };
        scenario.permanentConnections = new[]
        {
            new ScenarioPermanentCableConnection { connectionId = "Infrastructure", patchPanelEndpointId = "Patch", wallportEndpointId = "Wall", requiresPunchDownTermination = true }
        };
        var sessionObject = new GameObject("PhysicalResultsSession");
        var session = sessionObject.AddComponent<TssTrainingSession>();
        session.BeginScenario(scenario, "STUDENT-1");

        try
        {
            var physical = new TssPhysicalWorkPackageEvaluator(session).Evaluate();
            var assessment = TssScenarioAssessmentCoordinator.Assess(session);

            Assert.IsNull(session.NetworkState);
            Assert.IsTrue(physical.UnmetCategories.Exists(x => x.Id == "WP1.CableTypes.RequiredCable"));
            Assert.IsTrue(physical.UnmetCategories.Exists(x => x.Id == "WP1.PermanentConnections.Infrastructure"));
            Assert.IsTrue(physical.UnmetCategories.Exists(x => x.Id == "WP1.Terminations"));
            Assert.IsFalse(assessment.WorkPackages[0].IsComplete);
            Assert.IsFalse(assessment.WorkPackages[1].UnmetCriteria.Any(x => x.Contains("cable", System.StringComparison.OrdinalIgnoreCase)));
        }
        finally
        {
            Object.DestroyImmediate(sessionObject);
            Object.DestroyImmediate(scenario);
        }
    }

    [Test]
    public void Layer2DoesNotProducePhysicalCriteria()
    {
        var state = new TssNetworkSessionState(TssNetworkRunProfile.Create(9, new NetworkScenarioConfig()));
        var result = new TssNetworkWorkPackageEvaluator(state).EvaluateLayer2(false);

        Assert.IsFalse(result.Conditions.Exists(x => x.Id.StartsWith("WP2.CableTypes.") || x.Id == "WP2.Terminations"));
    }

    [Test]
    public void Layer2ConfigurationDoesNotRequireCarrierUp()
    {
        var config = new NetworkScenarioConfig
        {
            requiredPorts = new[] { new NetworkRequiredPort { deviceRole = "AN-1", interfaceName = "Fa0/2", endpointId = "IT1" } }
        };
        var profile = TssNetworkRunProfile.Create(10, config);
        var state = new TssNetworkSessionState(profile);
        var an1 = state.GetOrCreateDevice("AN1", EquipmentCategory.Switch, "AN-1");
        var an2 = state.GetOrCreateDevice("AN2", EquipmentCategory.Switch, "AN-2");
        an1.Interfaces["Fa0/2"] = new NetworkInterfaceState { Name = "Fa0/2", AdminUp = true, CarrierUp = false, AccessVlan = profile.ManagementVlan };
        an1.Interfaces["Fa0/1"] = Trunk("Fa0/1", profile);
        an1.Interfaces["Fa0/6"] = Trunk("Fa0/6", profile);
        an2.Interfaces["Fa0/1"] = Trunk("Fa0/1", profile);

        var result = new TssNetworkWorkPackageEvaluator(state).EvaluateLayer2(false);

        Assert.IsFalse(result.UnmetCategories.Exists(x => x.Id == "WP2.Access:AN-1:Fa0/2"));
        Assert.IsFalse(result.UnmetCategories.Exists(x => x.Id.StartsWith("WP2.Trunk:", System.StringComparison.Ordinal)));
    }

    [Test]
    public void Layer2RequiresLineLoginAndExactVlanDatabase()
    {
        var state = CompleteLayer2State(out var an1, out _);
        an1.Hostname = "WRONG-NAME";
        Assert.IsTrue(new TssNetworkWorkPackageEvaluator(state).EvaluateLayer2(false).UnmetCategories.Exists(x => x.Id == "WP2.AN-1.Hardening"));

        an1.Hostname = "AN-1";
        an1.EnablePassword = "legacy-password";
        Assert.IsTrue(new TssNetworkWorkPackageEvaluator(state).EvaluateLayer2(false).UnmetCategories.Exists(x => x.Id == "WP2.AN-1.Hardening"));

        an1.EnablePassword = string.Empty;
        an1.ConsoleLoginRequired = false;
        Assert.IsTrue(new TssNetworkWorkPackageEvaluator(state).EvaluateLayer2(false).UnmetCategories.Exists(x => x.Id == "WP2.AN-1.Hardening"));

        an1.ConsoleLoginRequired = true;
        an1.VtyLoginRequired = false;
        Assert.IsTrue(new TssNetworkWorkPackageEvaluator(state).EvaluateLayer2(false).UnmetCategories.Exists(x => x.Id == "WP2.AN-1.Hardening"));

        an1.VtyLoginRequired = true;
        an1.Vlans[999] = "Unexpected";
        Assert.IsTrue(new TssNetworkWorkPackageEvaluator(state).EvaluateLayer2(false).UnmetCategories.Exists(x => x.Id == "WP2.AN-1.Vlans"));

        an1.Vlans.Remove(999);
        an1.Vlans[state.Profile.ManagementVlan] += " (suspended)";
        Assert.IsTrue(new TssNetworkWorkPackageEvaluator(state).EvaluateLayer2(false).UnmetCategories.Exists(x => x.Id == "WP2.AN-1.Vlans"));
    }

    [Test]
    public void Layer2RequiresExactTrunkListsAndUnusedPortShutdown()
    {
        var state = CompleteLayer2State(out var an1, out _);
        var trunk = an1.Interfaces["Fa0/1"];
        trunk.AllowedVlans.Add(state.Profile.NativeVlan);
        Assert.IsTrue(new TssNetworkWorkPackageEvaluator(state).EvaluateLayer2(false).UnmetCategories.Exists(x => x.Id == "WP2.Trunk:AN-1:Fa0/1"));

        trunk.AllowedVlans.Remove(state.Profile.NativeVlan);
        trunk.AllowedVlans.Add(state.Profile.DeadVlan);
        Assert.IsTrue(new TssNetworkWorkPackageEvaluator(state).EvaluateLayer2(false).UnmetCategories.Exists(x => x.Id == "WP2.Trunk:AN-1:Fa0/1"));

        trunk.AllowedVlans.Remove(state.Profile.DeadVlan);
        an1.Interfaces["Fa0/7"].AdminUp = true;
        Assert.IsTrue(new TssNetworkWorkPackageEvaluator(state).EvaluateLayer2(false).UnmetCategories.Exists(x => x.Id == "WP2.UnusedPorts:AN-1"));
    }

    [Test]
    public void Layer2UsesGranularCriteriaWithoutCarrierOrPhysicalCable()
    {
        var state = CompleteLayer2State(out _, out _);
        var evaluator = new TssNetworkWorkPackageEvaluator(state);
        var result = evaluator.EvaluateLayer2(false);

        Assert.IsTrue(result.Complete, string.Join("\n", result.UnmetConditions));
        Assert.IsTrue(result.Conditions.Any(x => x.Id == "WP2.Access:AN-1:Fa0/2"));
        Assert.IsTrue(result.Conditions.Any(x => x.Id == "WP2.Access:AN-2:Fa0/2"));
        Assert.IsTrue(result.Conditions.Any(x => x.Id == "WP2.Trunk:AN-1:Fa0/1"));
        Assert.IsTrue(result.Conditions.Any(x => x.Id == "WP2.Trunk:AN-1:Fa0/6"));
        Assert.IsTrue(result.Conditions.Any(x => x.Id == "WP2.Trunk:AN-2:Fa0/1"));
        Assert.IsTrue(result.Conditions.Any(x => x.Id == "WP2.UnusedPorts:AN-1"));
        Assert.IsTrue(result.Conditions.Any(x => x.Id == "WP2.UnusedPorts:AN-2"));
        Assert.IsFalse(result.Conditions.Any(x => x.Id.StartsWith("WP2.CableTypes.") || x.Id == "WP2.Terminations"));

        state.Devices.SelectMany(device => device.Interfaces.Values).ToList().ForEach(port => port.CarrierUp = true);
        Assert.IsTrue(evaluator.EvaluateLayer2(false).Complete, "Live carrier changes must not invalidate the saved Layer 2 configuration.");

        var completion = evaluator.EvaluateLayer2();
        Assert.IsNotNull(completion.CompletionSnapshot);
        CollectionAssert.AreEquivalent(new[] { "WP2" }, state.CompletionSnapshots.Keys);
    }

    private static TssNetworkSessionState CompleteLayer2State(out NetworkDeviceState an1, out NetworkDeviceState an2)
    {
        var config = new NetworkScenarioConfig
        {
            enableSecret = "EnableSecret",
            consolePassword = "ConsolePassword",
            vtyPassword = "VtyPassword",
            motd = "Authorized users only",
            requiredPorts = new[]
            {
                new NetworkRequiredPort { deviceRole = "AN-1", interfaceName = "Fa0/2", endpointId = "IT1" },
                new NetworkRequiredPort { deviceRole = "AN-2", interfaceName = "Fa0/2", endpointId = "Data1" }
            }
        };
        var profile = TssNetworkRunProfile.Create(100, config);
        var state = new TssNetworkSessionState(profile);
        an1 = state.GetOrCreateDevice("AN1", EquipmentCategory.Switch, "AN-1");
        an2 = state.GetOrCreateDevice("AN2", EquipmentCategory.Switch, "AN-2");
        ConfigureSwitch(an1, profile);
        ConfigureSwitch(an2, profile);

        an1.Interfaces["Fa0/2"] = AccessPort("Fa0/2", profile.ManagementVlan);
        an2.Interfaces["Fa0/2"] = AccessPort("Fa0/2", profile.DataVlan);
        an1.Interfaces["Fa0/1"] = Trunk("Fa0/1", profile);
        an1.Interfaces["Fa0/6"] = Trunk("Fa0/6", profile);
        an2.Interfaces["Fa0/1"] = Trunk("Fa0/1", profile);
        ParkUnusedPorts(an1, 7, profile);
        ParkUnusedPorts(an2, 5, profile);
        an1.StartupSnapshot = an1.Clone();
        an2.StartupSnapshot = an2.Clone();
        return state;
    }

    private static void ConfigureSwitch(NetworkDeviceState device, TssNetworkRunProfile profile)
    {
        device.EnableSecret = profile.EnableSecret;
        device.ConsolePassword = profile.ConsolePassword;
        device.VtyPassword = profile.VtyPassword;
        device.Motd = profile.Motd;
        device.PasswordEncryption = true;
        device.DomainLookupDisabled = true;
        device.ConsoleLoginRequired = true;
        device.VtyLoginRequired = true;
        device.Vlans[profile.ManagementVlan] = profile.ManagementVlanName;
        device.Vlans[profile.ReceptionVlan] = profile.ReceptionVlanName;
        device.Vlans[profile.DataVlan] = profile.DataVlanName;
        device.Vlans[profile.NativeVlan] = profile.NativeVlanName;
        device.Vlans[profile.DeadVlan] = profile.DeadVlanName + " (suspended)";
        device.Evidence.Add("show vlan brief");
        device.Evidence.Add("show interfaces trunk");
        device.Evidence.Add("save");
    }

    private static NetworkInterfaceState AccessPort(string name, int vlan) => new() { Name = name, AdminUp = true, CarrierUp = false, AccessVlan = vlan };

    private static void ParkUnusedPorts(NetworkDeviceState device, int firstPort, TssNetworkRunProfile profile)
    {
        for (var port = firstPort; port <= 24; port++)
            device.Interfaces["Fa0/" + port] = new NetworkInterfaceState { Name = "Fa0/" + port, AccessVlan = profile.DeadVlan, AdminUp = false, CarrierUp = false };
    }

    private static NetworkInterfaceState Trunk(string name, TssNetworkRunProfile profile)
    {
        return new NetworkInterfaceState
        {
            Name = name,
            AdminUp = true,
            CarrierUp = false,
            IsTrunk = true,
            NativeVlan = profile.NativeVlan,
            AllowedVlans = new System.Collections.Generic.HashSet<int>
            {
                profile.ManagementVlan, profile.ReceptionVlan, profile.DataVlan
            }
        };
    }

    [Test]
    public void ResultsSnapshotRemainsAvailableAfterSourceSessionIsDestroyed()
    {
        TssResultsHandoff.Clear();
        var scenario = ScriptableObject.CreateInstance<TssScenarioDefinition>();
        scenario.scenarioId = "SNAPSHOT-1";
        scenario.displayName = "Snapshot scenario";
        var sessionObject = new GameObject("ResultsSession");
        var session = sessionObject.AddComponent<TssTrainingSession>();
        session.BeginScenario(scenario, "STUDENT-2");

        try
        {
            var snapshot = TssResultsHandoff.Capture(session);
            scenario.displayName = "Changed after exit";
            Object.DestroyImmediate(sessionObject);

            Assert.AreSame(snapshot, TssResultsHandoff.Snapshot);
            Assert.AreEqual("SNAPSHOT-1", snapshot.Scenario.ScenarioId);
            Assert.AreEqual("Snapshot scenario", snapshot.Scenario.DisplayName);
            Assert.AreEqual("STUDENT-2", snapshot.StudentId);
            Assert.IsNotNull(snapshot.Assessment);
        }
        finally
        {
            TssResultsHandoff.Clear();
            Object.DestroyImmediate(scenario);
        }
    }

    [Test]
    public void ResultsHandoffIsOneShotUntilCleared()
    {
        TssResultsHandoff.Clear();
        var first = TssResultsHandoff.Capture(null);
        var second = TssResultsHandoff.Capture(null);

        try
        {
            Assert.AreSame(first, second);
        }
        finally
        {
            TssResultsHandoff.Clear();
        }
    }
}
