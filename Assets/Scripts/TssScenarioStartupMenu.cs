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

        var panel = buttonsContainer.parent as RectTransform;
        if (panel)
            panel.sizeDelta = new Vector2(560f, 420f);
        var buttonsRect = buttonsContainer as RectTransform;
        if (buttonsRect)
        {
            buttonsRect.anchoredPosition = new Vector2(0f, -105f);
            buttonsRect.sizeDelta = new Vector2(-80f, -230f);
        }

        var lastNameInput = CreateInput(buttonsContainer.parent, "PlayerLastNameInput", "Last name", new Vector2(0f, 75f), InputField.ContentType.Standard);
        var computerNumberInput = CreateInput(buttonsContainer.parent, "ComputerNumberInput", "Computer number", new Vector2(0f, 20f), InputField.ContentType.IntegerNumber);
        var validation = CreateText(buttonsContainer.parent, "PlayerIdValidation", string.Empty, new Vector2(0f, -35f), 16, new Color(1f, .7f, .35f));

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
                if (!TssScenarioLaunchState.TryCreateStudentId(lastNameInput.text, computerNumberInput.text, out var studentId))
                {
                    validation.text = "Enter a last name and a positive computer number.";
                    return;
                }

                TssScenarioLaunchState.Select(scenario, studentId);
                SceneManager.LoadScene(scenario.sceneName);
            });
        }
    }

    private static InputField CreateInput(Transform parent, string name, string placeholder, Vector2 position, InputField.ContentType contentType)
    {
        var root = new GameObject(name, typeof(RectTransform), typeof(CanvasRenderer), typeof(Image), typeof(InputField));
        root.transform.SetParent(parent, false);
        var rect = (RectTransform)root.transform;
        rect.anchorMin = rect.anchorMax = new Vector2(.5f, .5f);
        rect.anchoredPosition = position;
        rect.sizeDelta = new Vector2(400f, 42f);
        root.GetComponent<Image>().color = new Color(.16f, .2f, .16f, .98f);

        var input = root.GetComponent<InputField>();
        input.contentType = contentType;
        input.textComponent = CreateText(root.transform, "Text", string.Empty, Vector2.zero, 20, Color.white);
        input.placeholder = CreateText(root.transform, "Placeholder", placeholder, Vector2.zero, 20, new Color(.65f, .7f, .65f));
        Stretch(input.textComponent.rectTransform);
        Stretch(input.placeholder.rectTransform);
        return input;
    }

    private static Text CreateText(Transform parent, string name, string value, Vector2 position, int size, Color color)
    {
        var root = new GameObject(name, typeof(RectTransform), typeof(CanvasRenderer), typeof(Text));
        root.transform.SetParent(parent, false);
        var rect = (RectTransform)root.transform;
        rect.anchorMin = rect.anchorMax = new Vector2(.5f, .5f);
        rect.anchoredPosition = position;
        rect.sizeDelta = new Vector2(400f, 30f);
        var text = root.GetComponent<Text>();
        text.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        text.fontSize = size;
        text.alignment = TextAnchor.MiddleLeft;
        text.color = color;
        text.text = value;
        return text;
    }

    private static void Stretch(RectTransform rect)
    {
        rect.anchorMin = Vector2.zero;
        rect.anchorMax = Vector2.one;
        rect.offsetMin = new Vector2(12f, 0f);
        rect.offsetMax = new Vector2(-12f, 0f);
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

