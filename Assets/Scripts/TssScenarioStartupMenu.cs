using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

public sealed class TssScenarioStartupMenu : MonoBehaviour
{
    [SerializeField] private TssScenarioDefinition[] scenarios;
    [SerializeField] private TssTrainingSession session;

    private void Start()
    {
        if (!session)
            session = FindFirstObjectByType<TssTrainingSession>();

        BuildMenu();
    }

    private void BuildMenu()
    {
        EnsureEventSystem();
        var canvas = new GameObject("TSS Scenario Selection UGUI", typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster)).GetComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.transform.SetParent(transform, false);

        var panel = new GameObject("Scenario Selection", typeof(Image));
        panel.transform.SetParent(canvas.transform, false);
        var rect = panel.GetComponent<RectTransform>();
        rect.anchorMin = rect.anchorMax = rect.pivot = new Vector2(0.5f, 0.5f);
        rect.sizeDelta = new Vector2(520f, 280f);
        panel.GetComponent<Image>().color = new Color(0.04f, 0.05f, 0.06f, 0.95f);

        MenuText("TSS Rack Installation", panel.transform, new Vector2(0f, -42f), new Vector2(460f, 38f), 24);
        var scenario = scenarios != null && scenarios.Length > 0 ? scenarios[0] : session ? session.GetFallbackScenario() : null;
        if (scenario)
            MenuButton(scenario.displayName, panel.transform, new Vector2(0f, -126f), new Vector2(360f, 48f), () =>
            {
                TssScenarioLaunchState.Select(scenario);
                SceneManager.LoadScene(scenario.sceneName);
            });
    }

    private static void MenuText(string label, Transform parent, Vector2 position, Vector2 size, int fontSize)
    {
        var text = new GameObject(label, typeof(Text)).GetComponent<Text>();
        text.transform.SetParent(parent, false);
        var rect = text.GetComponent<RectTransform>();
        rect.anchorMin = rect.anchorMax = rect.pivot = new Vector2(0.5f, 1f);
        rect.anchoredPosition = position;
        rect.sizeDelta = size;
        text.text = label;
        text.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        text.fontSize = fontSize;
        text.alignment = TextAnchor.MiddleCenter;
        text.color = Color.white;
    }

    private static void MenuButton(string label, Transform parent, Vector2 position, Vector2 size, UnityEngine.Events.UnityAction onClick)
    {
        var button = new GameObject(label, typeof(Image), typeof(Button)).GetComponent<Button>();
        button.transform.SetParent(parent, false);
        var rect = button.GetComponent<RectTransform>();
        rect.anchorMin = rect.anchorMax = rect.pivot = new Vector2(0.5f, 1f);
        rect.anchoredPosition = position;
        rect.sizeDelta = size;
        button.GetComponent<Image>().color = new Color(0.18f, 0.26f, 0.35f, 1f);
        button.onClick.AddListener(onClick);
        MenuText(label, button.transform, Vector2.zero, size, 17);
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
