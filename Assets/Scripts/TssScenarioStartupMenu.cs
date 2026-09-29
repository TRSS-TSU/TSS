using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

public sealed class TssScenarioStartupMenu : MonoBehaviour
{
    [Header("Scenarios")]
    [SerializeField] private TssScenarioDefinition[] scenarios;
    [SerializeField] private TssTrainingSession session;

    [Header("UI Prefabs")]
    [SerializeField] private GameObject scenarioMenuCanvasPrefab;
    [SerializeField] private GameObject scenarioButtonPrefab;

    private void Start()
    {
        if (!session)
            session = FindFirstObjectByType<TssTrainingSession>();

        BuildMenu();
    }

    private void BuildMenu()
    {
        EnsureEventSystem();

        GameObject canvasInstance;
        if (scenarioMenuCanvasPrefab)
        {
            canvasInstance = Instantiate(scenarioMenuCanvasPrefab, transform, false);
        }
        else
        {
            Debug.LogWarning("TssScenarioStartupMenu requires a scenario menu canvas prefab.");
            return;
        }

        canvasInstance.name = "TSS Scenario Selection UGUI (Prefab)";

        var buttonsContainer = canvasInstance.transform.Find("ScenarioSelectionPanel/ScenarioButtonsContainer");
        if (!buttonsContainer) return;

        var scenario = scenarios != null && scenarios.Length > 0 ? scenarios[0] : session ? session.GetFallbackScenario() : null;
        if (!scenario) return;

        GameObject btnGO;
        if (scenarioButtonPrefab)
        {
            btnGO = Instantiate(scenarioButtonPrefab, buttonsContainer, false);
        }
        else
        {
            Debug.LogWarning("TssScenarioStartupMenu requires a scenario button prefab.");
            return;
        }

        var label = btnGO.transform.Find("Text")?.GetComponent<Text>();
        if (label) label.text = scenario.displayName;

        var button = btnGO.GetComponent<Button>();
        if (button)
        {
            button.onClick.AddListener(() =>
            {
                TssScenarioLaunchState.Select(scenario);
                SceneManager.LoadScene(scenario.sceneName);
            });
        }
    }

    private static void EnsureEventSystem()
    {
        if (FindFirstObjectByType<UnityEngine.EventSystems.EventSystem>())
            return;

        var eventSystem = new GameObject("EventSystem", typeof(UnityEngine.EventSystems.EventSystem));
#if ENABLE_INPUT_SYSTEM
        eventSystem.AddComponent<UnityEngine.InputSystem.UI.InputSystemUIInputModule>();
#else
        eventSystem.AddComponent<UnityEngine.EventSystems.StandaloneInputModule>();
#endif
    }
}

