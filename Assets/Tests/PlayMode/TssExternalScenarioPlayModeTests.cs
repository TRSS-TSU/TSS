using System;
using System.Collections;
using System.IO;
using System.Linq;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using UnityEngine.UI;
using Object = UnityEngine.Object;

public sealed class TssExternalScenarioPlayModeTests
{
    private string manifestPath;
    private string originalManifest;
    private string scenarioPath;
    private string originalScenario;

    [SetUp]
    public void BackupFiles()
    {
        if (Application.platform != RuntimePlatform.WindowsEditor && Application.platform != RuntimePlatform.OSXEditor && Application.platform != RuntimePlatform.LinuxEditor)
            Assert.Ignore("These scene integration tests require writable local StreamingAssets in the Editor.");
        manifestPath = Path.Combine(Application.streamingAssetsPath, "Scenarios/manifest.json");
        originalManifest = File.ReadAllText(manifestPath);
        scenarioPath = Path.Combine(Application.streamingAssetsPath, "Scenarios/balanced-lab.json");
        originalScenario = File.ReadAllText(scenarioPath);
    }

    [TearDown]
    public void RestoreFiles()
    {
        if (originalManifest != null) File.WriteAllText(manifestPath, originalManifest);
        if (originalScenario != null) File.WriteAllText(scenarioPath, originalScenario);
        TssScenarioLaunchState.Select(null);
    }

    [UnityTest]
    public IEnumerator AllFourMenuButtonsLaunchMatchingSessionAndBinder()
    {
        var manifest = JsonUtility.FromJson<TssScenarioManifest>(originalManifest);
        foreach (var entry in manifest.scenarios)
        {
            yield return OpenMenu();
            Assert.AreEqual(4, ScenarioButtons().Length);
            Assert.AreEqual(0, Object.FindObjectsByType<TssTrainingSession>(FindObjectsSortMode.None).Length);
            var button = ScenarioButtons().Single(x => x.GetComponentInChildren<Text>().text == entry.displayName);
            button.onClick.Invoke();
            Assert.AreEqual("MainMenu", SceneManager.GetActiveScene().name, "Blank student ID must not launch.");
            StringAssert.Contains("last name", GameObject.Find("PlayerIdValidation").GetComponent<Text>().text);
            SetStudentInputs();
            button.onClick.Invoke();
            button.onClick.Invoke(); // A double click must not queue a second scene transition.
            yield return WaitUntil(() => SceneManager.GetActiveScene().name == "TSS" && TssTrainingSession.Instance && TssTrainingSession.Instance.Scenario);
            yield return null;
            var data = JsonUtility.FromJson<TssExternalScenarioData>(File.ReadAllText(Path.Combine(Application.streamingAssetsPath, "Scenarios", entry.jsonFile)));
            AssertLiveScenario(data, true);
            var session = TssTrainingSession.Instance;
            var first = session.GetScenarioInventory().First(x => x.equipment.category == EquipmentCategory.Switch);
            Assert.IsTrue(session.TryCheckout(first.equipment, out var message), message);
            Assert.AreSame(first.equipment, session.HeldItem);
            Assert.IsTrue(session.ReturnHeldItem(out message), message);
            InstallRequiredEquipment(data, session);
            ConnectRequiredCables(data, session);
            yield return AssertBinder(data);
        }
    }

    [UnityTest]
    public IEnumerator MissingManifestLaunchesBuiltInFallback()
    {
        File.Delete(manifestPath);
        yield return OpenMenu();
        File.WriteAllText(manifestPath, originalManifest);
        yield return LaunchFallback();
    }

    [UnityTest]
    public IEnumerator MalformedManifestLaunchesBuiltInFallback()
    {
        File.WriteAllText(manifestPath, "{ invalid json");
        yield return OpenMenu();
        File.WriteAllText(manifestPath, originalManifest);
        yield return LaunchFallback();
    }

    [UnityTest]
    public IEnumerator InvalidScenarioIsExcludedAndFallbackRemainsAvailable()
    {
        var data = JsonUtility.FromJson<TssExternalScenarioData>(originalScenario);
        data.inventory[0].equipmentId = "DOES_NOT_EXIST";
        File.WriteAllText(scenarioPath, JsonUtility.ToJson(data));
        yield return OpenMenu();
        File.WriteAllText(scenarioPath, originalScenario);
        Assert.AreEqual(4, ScenarioButtons().Length, "Three external choices plus built-in fallback.");
        Assert.IsFalse(ScenarioButtons().Any(x => x.GetComponentInChildren<Text>().text == data.displayName));
        yield return LaunchFallback();
    }

    [UnityTest]
    public IEnumerator FileInvalidatedAfterMenuLoadDoesNotPartiallyLaunch()
    {
        yield return OpenMenu();
        SetStudentInputs();
        File.WriteAllText(scenarioPath, "{}");
        ScenarioButtons()[0].onClick.Invoke();
        yield return WaitUntil(() => ScenarioButtons().Any(x => x.GetComponentInChildren<Text>().text.EndsWith("(Built-in)")));
        Assert.AreEqual("MainMenu", SceneManager.GetActiveScene().name);
        Assert.IsNull(TssScenarioLaunchState.ConsumeSelectedScenario(out _));
        File.WriteAllText(scenarioPath, originalScenario);
        yield return LaunchFallback();
    }

    private static IEnumerator OpenMenu()
    {
        SceneManager.LoadScene("MainMenu");
        yield return WaitUntil(() => Object.FindFirstObjectByType<TssScenarioStartupMenu>()?.IsReady == true);
    }

    private static Button[] ScenarioButtons() => GameObject.Find("ScenarioButtonsContainer").GetComponentsInChildren<Button>();

    private static void SetStudentInputs()
    {
        GameObject.Find("PlayerLastNameInput").GetComponent<InputField>().text = "  Smith ";
        GameObject.Find("ComputerNumberInput").GetComponent<InputField>().text = "017";
    }

    private IEnumerator LaunchFallback()
    {
        StringAssert.Contains("unavailable", GameObject.Find("ScenarioLoadStatus").GetComponent<Text>().text.ToLowerInvariant());
        var fallback = ScenarioButtons().Single(x => x.GetComponentInChildren<Text>().text.EndsWith("(Built-in)"));
        SetStudentInputs();
        fallback.onClick.Invoke();
        yield return WaitUntil(() => SceneManager.GetActiveScene().name == "TSS" && TssTrainingSession.Instance && TssTrainingSession.Instance.Scenario);
        AssertLiveScenario(JsonUtility.FromJson<TssExternalScenarioData>(originalScenario), false);
    }

    private static void AssertLiveScenario(TssExternalScenarioData data, bool external)
    {
        var sessions = Object.FindObjectsByType<TssTrainingSession>(FindObjectsSortMode.None);
        Assert.AreEqual(1, sessions.Length);
        var session = sessions[0];
        Assert.AreSame(session, TssTrainingSession.Instance);
        Assert.AreEqual("TSS Scenario Runtime", session.name);
        Assert.AreEqual(data.scenarioId, session.Scenario.scenarioId);
        Assert.AreEqual(data.facilityId, session.Scenario.facilityId);
        Assert.AreEqual(data.catalogId, session.Scenario.catalogId);
        Assert.AreEqual(external, session.Scenario.IsExternalRuntime);
        Assert.AreEqual("SMITH-17", session.StudentId);
        var catalog = Resources.Load<TssScenarioCatalog>(TssScenarioCatalog.ResourcePath);
        foreach (var row in data.inventory) Assert.AreEqual(row.quantity, session.GetRemaining(catalog.FindEquipment(row.equipmentId)));
        foreach (var row in data.cableInventory) Assert.AreEqual(row.quantity, session.GetRemaining(catalog.FindCable(row.cableId)));
        // Punch-down links are intentionally absent until both ends are terminated.
        Assert.AreEqual(data.permanentConnections.Count(x => !x.requiresPunchDownTermination), session.PhysicalConnections.Count);
        foreach (var connection in session.PhysicalConnections)
        {
            var expected = data.permanentConnections.Single(x => x.connectionId == connection.ConnectionId);
            Assert.AreEqual(expected.patchPanelEndpointId, connection.EndpointAId);
            Assert.AreEqual(expected.wallportEndpointId, connection.EndpointBId);
            Assert.IsTrue(connection.IsPermanent);
        }
        Assert.AreEqual(JsonUtility.ToJson(data), JsonUtility.ToJson(TssExternalScenarioLoader.Export(session.Scenario)));
        var expectedProfile = TssNetworkRunProfile.Create(TssNetworkRunProfile.DeriveSeed("SMITH-17", data.scenarioId), data.networkScenario, "SMITH-17");
        Assert.AreEqual(expectedProfile.Seed, session.NetworkProfile.Seed);
        Assert.AreEqual(expectedProfile.ManagementVlan, session.NetworkProfile.ManagementVlan);
        Assert.AreEqual(expectedProfile.WanProviderAddress, session.NetworkProfile.WanProviderAddress);
        Assert.AreEqual(expectedProfile.EnableSecret, session.NetworkProfile.EnableSecret);
        Assert.AreEqual(data.expectedRackPlacements.Length + data.expectedEndpointPlacements.Length,
            TssPlacementEvaluator.EvaluateRequirements(session.Scenario, session.Installed, session.EndpointPlacements).Count);
    }

    private static IEnumerator AssertBinder(TssExternalScenarioData data)
    {
        var hud = Object.FindObjectsByType<Button>(FindObjectsInactive.Include, FindObjectsSortMode.None).Single(x => x.name == "BinderButton");
        hud.onClick.Invoke();
        yield return null;
        var panel = GameObject.Find("BinderPanel");
        Assert.IsNotNull(panel);
        var tabs = panel.transform.Find("BinderTabs").GetComponentsInChildren<Button>();
        Assert.AreEqual(7, tabs.Length);
        for (var index = 0; index < tabs.Length; index++)
        {
            tabs[index].onClick.Invoke();
            yield return null;
            var texts = panel.GetComponentsInChildren<Text>().Select(x => x.text).ToArray();
            Assert.IsTrue(texts.Any(x => x.Contains(data.scenarioId)), "Selected binder tab must show scenario ID: " + index);
            if (index == 0) Assert.IsTrue(texts.Any(x => x.Contains(data.binderReference.namingAndRacks.intro)));
            if (index == 1) CollectionAssert.Contains(texts, data.networkScenario.managementVlanName);
            if (index == 4) Assert.IsTrue(texts.Any(x => x.Contains(TssTrainingSession.Instance.NetworkProfile.WanProviderAddress)));
            if (index == 6) Assert.IsTrue(texts.Any(x => x.Contains(TssTrainingSession.Instance.NetworkProfile.EnableSecret)));
        }
    }

    private static void InstallRequiredEquipment(TssExternalScenarioData data, TssTrainingSession session)
    {
        foreach (var row in data.expectedRackPlacements)
        {
            var equipment = session.GetScenarioInventory().First(x => x.equipment.category == row.category).equipment;
            var rack = Object.FindObjectsByType<RackMountController>(FindObjectsSortMode.None).Single(x => x.RackId == row.rackId);
            var lastU = row.endingU == 0 ? row.startingU : row.endingU;
            var installU = Enumerable.Range(row.startingU, lastU - row.startingU + 1).FirstOrDefault(u => rack.CanInstall(equipment, u));
            Assert.Greater(installU, 0, "No available position in the authored range for " + row.requiredName);
            Assert.IsTrue(session.TryCheckout(equipment, out var message), message);
            Assert.IsTrue(session.TryInstallHeld(rack, installU, row.requiredName, out message), row.requiredName + ": " + message);
        }
        foreach (var row in data.expectedEndpointPlacements)
        {
            var equipment = session.GetScenarioInventory().First(x => x.equipment.category == row.category).equipment;
            var station = Object.FindObjectsByType<EndpointPlacementStation>(FindObjectsSortMode.None).Single(x => x.StationId == row.stationId);
            Assert.IsTrue(session.TryCheckout(equipment, out var message), message);
            // Use the same placement action as the name-prompt callback, including the actual visual and bindings.
            station.SendMessage("PlaceHeld", row.requiredName, SendMessageOptions.RequireReceiver);
            Assert.IsNull(session.HeldItem, "Placement rejected at " + row.stationId);
        }
        var result = TssPlacementEvaluator.Evaluate(session.Scenario, session.Installed, session.EndpointPlacements);
        Assert.IsTrue(result.IsComplete, string.Join("\n", result.Messages));
        Assert.IsTrue(TssPlacementEvaluator.EvaluateRequirements(session.Scenario, session.Installed, session.EndpointPlacements).All(x => x.IsComplete));
    }

    private static void ConnectRequiredCables(TssExternalScenarioData data, TssTrainingSession session)
    {
        var authoredRequirements = JsonUtility.ToJson(TssExternalScenarioLoader.Export(session.Scenario));
        var statuses = session.GetCableTypeStatuses().ToArray();
        Assert.AreEqual(data.cableTypeRequirements.Length, statuses.Length);
        var endpoints = Object.FindObjectsByType<TssPortEndpoint>(FindObjectsInactive.Include, FindObjectsSortMode.None);
        foreach (var status in statuses)
        {
            var endpointA = endpoints.SingleOrDefault(x => x.EndpointId == status.EndpointAId);
            var endpointB = endpoints.SingleOrDefault(x => x.EndpointId == status.EndpointBId);
            Assert.IsNotNull(endpointA, "Required endpoint is not present: " + status.EndpointAId);
            Assert.IsNotNull(endpointB, "Required endpoint is not present: " + status.EndpointBId);
            var cable = session.GetScenarioCableInventory().First(x => x.cable.cableType == status.RequiredCableType).cable;
            Assert.IsTrue(session.TryCheckoutCable(cable, out var message), message);
            Assert.IsTrue(session.TryUsePort(endpointA, out message), message);
            Assert.IsTrue(session.TryUsePort(endpointB, out message), message);
        }
        Assert.IsTrue(session.GetCableTypeStatuses().All(x => x.IsConnected && x.IsCorrect));
        var layer2 = new TssNetworkWorkPackageEvaluator(session.NetworkState, session).EvaluateLayer2();
        Assert.IsFalse(layer2.UnmetCategories.Any(x => x.Id.StartsWith("WP2.CableTypes.")));
        Assert.AreEqual(authoredRequirements, JsonUtility.ToJson(TssExternalScenarioLoader.Export(session.Scenario)), "Resolving installed ports must not mutate the scenario.");
    }

    private static IEnumerator WaitUntil(Func<bool> condition)
    {
        var deadline = Time.realtimeSinceStartup + 40f;
        while (!condition() && Time.realtimeSinceStartup < deadline) yield return null;
        Assert.IsTrue(condition(), "Scene/menu did not become ready within 40 seconds.");
    }
}
