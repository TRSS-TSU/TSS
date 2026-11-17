using System.Collections;
using System.IO;
using System.Linq;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

public sealed class TssScenarioAutomationPlayModeTests
{
    [UnityTest]
    public IEnumerator CapstoneAutomationPlacesRouterConnectsAndTerminates()
    {
        SceneManager.LoadScene("TSS");
        yield return new WaitUntil(() => SceneManager.GetActiveScene().name == "TSS");
        yield return new WaitUntil(() => TssTrainingSession.Instance);

        var data = JsonUtility.FromJson<TssExternalScenarioData>(File.ReadAllText(Path.Combine(Application.streamingAssetsPath, "Scenarios", "capstone.json")));
        Assert.IsTrue(TssExternalScenarioLoader.TryCreate(
            data,
            Resources.Load<TssScenarioCatalog>(TssScenarioCatalog.ResourcePath),
            out var scenario,
            out var errors),
            string.Join("\n", errors));

        var session = TssTrainingSession.Instance;
        session.BeginScenario(scenario, "AUTOMATION-1");
        var setup = new TssCapstoneSetup(session);
        var preview = setup.Preview(out var canApply);
        Assert.IsTrue(canApply, preview);
        Assert.AreEqual(0, session.Installed.Count, "Preview must not change the session.");
        var partial = new TssScenarioAutomation(session).PlaceAllEquipment();
        Assert.AreEqual(0, partial.failedPlacements, partial.ToString());
        preview = setup.Preview(out canApply);
        Assert.IsTrue(canApply, preview);
        var result = setup.Apply();
        Assert.IsFalse(result.StartsWith("Stopped"), result);
        Assert.IsTrue(HasInstalled(session, "CN-1", EquipmentCategory.Router), result);
        Assert.AreEqual(6, session.Installed.Count);
        Assert.AreEqual(8, session.EndpointPlacements.Count);
        Assert.IsTrue(result.Contains("Placed 0") && result.Contains("connected 18"), result);
        foreach (var status in session.GetCableTypeStatuses())
            Assert.IsTrue(status.IsConnected && status.IsCorrect, $"Incorrect cable: {status.ConnectionId}");
        Assert.AreEqual(session.Scenario.permanentConnections.Count(x => x.requiresPunchDownTermination) * 2,
            session.GetTerminationModuleStates().Count);
        Assert.AreEqual(42, CountPermanent(session));
        Assert.AreEqual(60, session.PhysicalConnections.Count);
        Assert.AreNotEqual(RoleKey(session, "PDU1"), RoleKey(session, "AN-1"));
        Assert.AreNotEqual(RoleKey(session, "PDU2"), RoleKey(session, "AN-2"));
        Assert.IsTrue(session.PhysicalConnections.Any(x => !x.IsPermanent
            && (x.EndpointAId.Contains(":Fa07") || x.EndpointBId.Contains(":Fa07"))));
        Assert.IsFalse(session.PhysicalConnections.Any(x => !x.IsPermanent
            && (x.EndpointAId.StartsWith("Endpoint:LobbyTable01:") && x.EndpointBId.EndsWith(":Fa07")
                || x.EndpointBId.StartsWith("Endpoint:LobbyTable01:") && x.EndpointAId.EndsWith(":Fa07"))));
        foreach (var required in session.Scenario.expectedEffectivePaths)
        {
            var a = ResolveRequired(session, required.endpointAId);
            var b = ResolveRequired(session, required.endpointBId);
            Assert.IsTrue(session.GetEffectivePhysicalPaths().Any(x => x.EndpointAId == a && x.EndpointBId == b
                || x.EndpointAId == b && x.EndpointBId == a), required.requirementId + ": " + a + " <> " + b);
        }
        Assert.AreEqual(0, System.Linq.Enumerable.Count(session.NetworkState.Devices), "Tool must not configure router or switch CLI.");
        foreach (var role in new[] { "IT1", "IT2", "Reception1", "Reception2", "Data1", "Data2", "Data3", "AP-1" })
        {
            var key = RoleKey(session, role);
            Assert.IsTrue(session.NetworkState.TryGetEndpoint(key, out var endpoint), role);
            Assert.AreEqual(session.NetworkProfile.EndpointAddresses[role], endpoint.Address, role);
            Assert.AreEqual(session.NetworkProfile.WanProviderAddress, endpoint.Dns, role);
        }
        Assert.IsFalse(session.NetworkState.AccessPoint.Configured, "AP settings must wait for normal reachability.");
        var it1 = RoleKey(session, "IT1");
        Assert.IsFalse(new TssNetworkReachabilityEvaluator(session.NetworkState).Evaluate(it1, session.NetworkProfile.ManagementNetwork + ".1").Success);
        var rerun = setup.Apply();
        Assert.IsTrue(rerun.Contains("connected 0"), rerun);
        Assert.AreEqual(6, session.Installed.Count);
        Assert.AreEqual(8, session.EndpointPlacements.Count);
        Assert.AreEqual(60, session.PhysicalConnections.Count);
        session.NetworkState.TryGetEndpoint(it1, out var conflictingEndpoint);
        conflictingEndpoint.Dns = "1.1.1.1";
        preview = setup.Preview(out canApply);
        Assert.IsFalse(canApply);
        StringAssert.Contains("BLOCKED address conflict: IT1", preview);
    }

    private static bool HasInstalled(TssTrainingSession session, string name, EquipmentCategory category)
    {
        foreach (var item in session.Installed)
            if (item.DeviceName == name && item.Equipment && item.Equipment.category == category)
                return true;
        return false;
    }

    private static int CountPermanent(TssTrainingSession session)
    {
        var count = 0;
        foreach (var connection in session.PhysicalConnections)
            if (connection.IsPermanent) count++;
        return count;
    }

    private static string RoleKey(TssTrainingSession session, string role)
    {
        foreach (var item in session.EndpointPlacements)
            if (item.DeviceName == role) return TssTrainingSession.GetEndpointOwnerPrefix(item.PlacementId);
        foreach (var item in session.Installed)
            if (item.DeviceName == role) return TssTrainingSession.GetRackOwnerPrefix(item.RackId, item.StartingU, item.Equipment);
        return string.Empty;
    }

    private static string ResolveRequired(TssTrainingSession session, string authored)
    {
        if (authored.StartsWith("Rack:")) return session.ResolveScenarioEndpointId(authored);
        var key = RoleKey(session, authored);
        if (string.IsNullOrEmpty(key)) return authored;
        return key + (authored == "IT2" ? ":GE01" : ":Eth01");
    }
}
