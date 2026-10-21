using System.IO;
using UnityEditor;
using UnityEditor.TestTools.TestRunner.Api;
using UnityEngine;

// Keeps results available after the Test Runner's Play Mode domain reload.
[InitializeOnLoad]
public static class TssExternalScenarioTestRunner
{
    private static readonly TestRunnerApi Api;
    static TssExternalScenarioTestRunner()
    {
        Api = ScriptableObject.CreateInstance<TestRunnerApi>();
        Api.RegisterCallbacks(new Results());
    }

    [MenuItem("TSS/External Scenarios/Run Edit Mode Tests")]
    public static void RunEditMode() => Run(TestMode.EditMode, "TSS.EditMode.Tests");

    [MenuItem("TSS/External Scenarios/Run Play Mode Tests")]
    public static void RunPlayMode() => Run(TestMode.PlayMode, "TSS.PlayMode.Tests");

    private static void Run(TestMode mode, string assembly)
    {
        SessionState.SetString("TssExternalTestMode", mode.ToString());
        Api.Execute(new ExecutionSettings(new Filter { testMode = mode, assemblyNames = new[] { assembly } }));
    }

    private sealed class Results : ICallbacks
    {
        public void RunStarted(ITestAdaptor testsToRun) { }
        public void TestStarted(ITestAdaptor test) { }
        public void TestFinished(ITestResultAdaptor result) { }
        public void RunFinished(ITestResultAdaptor result)
        {
            Directory.CreateDirectory("Temp/TssScenarioTests");
            var mode = SessionState.GetString("TssExternalTestMode", "Tests");
            TestRunnerApi.SaveResultToFile(result, "Temp/TssScenarioTests/" + mode + ".xml");
            Debug.Log($"[TSS Scenarios] {mode}: {result.PassCount} passed, {result.FailCount} failed, {result.SkipCount} skipped.");
        }
    }
}
