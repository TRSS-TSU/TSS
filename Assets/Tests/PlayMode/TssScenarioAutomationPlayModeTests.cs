using System.Collections;
using System.IO;
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
        var automation = new TssScenarioAutomation(session);

        var placement = automation.PlaceAllEquipment();
        Assert.AreEqual(0, placement.failedPlacements, placement.ToString());
        Assert.IsTrue(HasInstalled(session, "CN-1", EquipmentCategory.Router), placement.ToString());
        Assert.AreEqual(6, session.Installed.Count);
        Assert.AreEqual(8, session.EndpointPlacements.Count);

        var cables = automation.ConnectAllCables();
        Assert.AreEqual(0, cables.failedConnections, cables.ToString());
        Assert.AreEqual(10, cables.connected);
        foreach (var status in session.GetCableTypeStatuses())
            Assert.IsTrue(status.IsConnected && status.IsCorrect, $"Incorrect cable: {status.ConnectionId}");

        var terminations = automation.TerminateAll();
        Assert.AreEqual(0, terminations.failedTerminations, terminations.ToString());
        Assert.AreEqual(82, session.GetTerminationModuleStates().Count);
        Assert.AreEqual(42, CountPermanent(session));

        var rerun = automation.RunAll();
        Assert.AreEqual(0, rerun.failedPlacements + rerun.failedConnections + rerun.failedTerminations, rerun.ToString());
        Assert.AreEqual(6, session.Installed.Count);
        Assert.AreEqual(8, session.EndpointPlacements.Count);
        Assert.AreEqual(52, session.PhysicalConnections.Count);
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
}
