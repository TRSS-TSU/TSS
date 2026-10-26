using NUnit.Framework;
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
