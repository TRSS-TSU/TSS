using System;
using System.Collections.Generic;
using System.Threading.Tasks;
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
    [Tooltip("Optional serialized scene instance. When assigned, the menu is visible and editable in MainMenu before Play Mode.")]
    [SerializeField] private GameObject scenarioMenuCanvasInstance;

    private Transform buttonsContainer;
    private InputField lastNameInput;
    private InputField computerNumberInput;
    private Text validation;
    private Text loadStatus;
    private TssExternalScenarioLoader loader;
    private readonly List<Button> buttons = new();
    private bool launching;
    private bool fallbackAdded;
    public bool IsReady { get; private set; }

    private async void Start()
    {
        if (!session)
            session = FindFirstObjectByType<TssTrainingSession>();

        BuildMenu();
        if (!buttonsContainer) return;
        loader = new TssExternalScenarioLoader(Resources.Load<TssScenarioCatalog>(TssScenarioCatalog.ResourcePath));
        try
        {
            var manifest = await loader.LoadManifestAsync();
            foreach (var entry in manifest.scenarios)
            {
                try
                {
                    var preview = await loader.LoadScenarioAsync(entry.scenarioId);
                    TssScenarioLaunchState.ReleaseRuntime(preview);
                    if (!this) return;
                    AddButton(entry.displayName, () => LaunchAsync(entry, null));
                }
                catch (Exception ex)
                {
                    loader.Warnings.Add(entry.displayName + ": " + ex.Message);
                }
            }
            if (!this) return;
            if (loader.Warnings.Count > 0 || buttons.Count == 0)
            {
                loadStatus.text = "Some external scenarios are unavailable. Built-in fallback is listed below.";
                foreach (var warning in loader.Warnings) Debug.LogWarning("[TSS Scenarios] " + warning);
                AddFallbackButtons();
            }
            else loadStatus.text = "Choose a scenario to begin.";
        }
        catch (Exception ex)
        {
            if (!this) return;
            Debug.LogWarning("[TSS Scenarios] " + ex.Message);
            loadStatus.text = "External scenarios unavailable. Using built-in scenarios.";
            AddFallbackButtons();
        }
        if (!this) return;
        IsReady = true;
        foreach (var button in buttons) button.interactable = true;
        if (buttons.Count == 0) loadStatus.text = "No scenarios available. Check the scenario catalog and menu configuration.";
    }

    private void BuildMenu()
    {
        EnsureEventSystem();
        if (!scenarioButtonPrefab)
        {
            Debug.LogWarning("TssScenarioStartupMenu requires a scenario button prefab.");
            return;
        }

        GameObject canvasInstance = scenarioMenuCanvasInstance;
        if (!canvasInstance && scenarioMenuCanvasPrefab)
        {
            canvasInstance = Instantiate(scenarioMenuCanvasPrefab, transform, false);
        }
        else if (!canvasInstance)
        {
            Debug.LogWarning("TssScenarioStartupMenu requires a scenario menu canvas prefab.");
            return;
        }

        if (!scenarioMenuCanvasInstance)
            canvasInstance.name = "TSS Scenario Selection UGUI (Prefab)";

        buttonsContainer = canvasInstance.transform.Find("ScenarioSelectionPanel/ScenarioButtonsContainer");
        if (!buttonsContainer) return;

        var panel = buttonsContainer.parent as RectTransform;
        if (panel)
        {
            panel.anchorMin = panel.anchorMax = new Vector2(1f, .5f);
            panel.pivot = new Vector2(1f, .5f);
            panel.anchoredPosition = new Vector2(-72f, 0f);
            panel.sizeDelta = new Vector2(620f, 840f);
        }
        var buttonsRect = buttonsContainer as RectTransform;
        if (buttonsRect)
        {
            buttonsRect.anchorMin = buttonsRect.anchorMax = new Vector2(.5f, 1f);
            buttonsRect.pivot = new Vector2(.5f, 1f);
            buttonsRect.anchoredPosition = new Vector2(0f, -390f);
            buttonsRect.sizeDelta = new Vector2(540f, 380f);
        }

        lastNameInput = panel.Find("PlayerLastNameInput")?.GetComponent<InputField>()
            ?? CreateInput(panel, "PlayerLastNameInput", "Last name", new Vector2(0f, 275f), InputField.ContentType.Standard);
        computerNumberInput = panel.Find("ComputerNumberInput")?.GetComponent<InputField>()
            ?? CreateInput(panel, "ComputerNumberInput", "Computer number", new Vector2(0f, 215f), InputField.ContentType.IntegerNumber);
        validation = panel.Find("PlayerIdValidation")?.GetComponent<Text>()
            ?? CreateText(panel, "PlayerIdValidation", string.Empty, new Vector2(0f, 155f), 16, new Color(1f, .7f, .35f));
        validation.rectTransform.sizeDelta = new Vector2(540f, 36f);
        loadStatus = panel.Find("ScenarioLoadStatus")?.GetComponent<Text>()
            ?? CreateText(panel, "ScenarioLoadStatus", "Loading scenarios...", new Vector2(0f, 95f), 18, new Color(.9f, .9f, .75f));
        loadStatus.rectTransform.sizeDelta = new Vector2(540f, 65f);
    }

    private void AddFallbackButtons()
    {
        if (fallbackAdded) return;
        fallbackAdded = true;
        foreach (var scenario in TssExternalScenarioLoader.GetFallbackScenarios(scenarios, session ? session.GetFallbackScenario() : null))
            AddButton(scenario.displayName + " (Built-in)", () => LaunchAsync(null, scenario));
    }

    private void AddButton(string title, Func<Task> launch)
    {
        if (!scenarioButtonPrefab) throw new InvalidOperationException("Scenario button prefab is missing.");
        var btnGO = Instantiate(scenarioButtonPrefab, buttonsContainer, false);
        var rect = (RectTransform)btnGO.transform;
        rect.anchorMin = rect.anchorMax = new Vector2(.5f, 1f);
        rect.anchoredPosition = new Vector2(0f, -26f - buttons.Count * 60f);
        rect.sizeDelta = new Vector2(580f, 52f);
        var label = btnGO.transform.Find("Text")?.GetComponent<Text>();
        if (label) label.text = title;
        var button = btnGO.GetComponent<Button>();
        button.interactable = IsReady;
        buttons.Add(button);
        button.onClick.AddListener(async () => await launch());
    }

    private async Task LaunchAsync(TssScenarioManifestEntry entry, TssScenarioDefinition fallback)
    {
        if (launching || !IsReady) return;
        if (!TssScenarioLaunchState.TryCreateStudentId(lastNameInput.text, computerNumberInput.text, out var studentId))
        {
            validation.text = "Enter a last name and a positive computer number.";
            return;
        }
        launching = true;
        validation.text = string.Empty;
        loadStatus.text = "Loading selected scenario...";
        foreach (var button in buttons) button.interactable = false;
        TssScenarioDefinition selected = null;
        try
        {
            selected = entry != null ? await loader.LoadScenarioAsync(entry.scenarioId) : fallback;
            if (!this) { TssScenarioLaunchState.ReleaseRuntime(selected); return; }
            if (!selected || !Application.CanStreamedLevelBeLoaded(selected.sceneName)) throw new InvalidOperationException("Selected scene is unavailable.");
            TssScenarioLaunchState.Select(selected, studentId);
            SceneManager.LoadScene(selected.sceneName);
        }
        catch (Exception ex)
        {
            TssScenarioLaunchState.Select(null);
            TssScenarioLaunchState.ReleaseRuntime(selected);
            if (!this) return;
            Debug.LogWarning("[TSS Scenarios] Launch rejected: " + ex.Message);
            loadStatus.text = "Scenario unavailable. Select another scenario or the built-in fallback.";
            AddFallbackButtons();
            foreach (var button in buttons) button.interactable = true;
            launching = false;
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
        var existing = FindFirstObjectByType<UnityEngine.EventSystems.EventSystem>();
        if (existing)
        {
            if (!existing.GetComponent<TssUiTabNavigation>())
                existing.gameObject.AddComponent<TssUiTabNavigation>();
            return;
        }

        var eventSystem = new GameObject(
            "EventSystem",
            typeof(UnityEngine.EventSystems.EventSystem),
            typeof(TssUiTabNavigation)
        );
#if ENABLE_INPUT_SYSTEM
        eventSystem.AddComponent<UnityEngine.InputSystem.UI.InputSystemUIInputModule>();
#else
        eventSystem.AddComponent<UnityEngine.EventSystems.StandaloneInputModule>();
#endif
    }
}

