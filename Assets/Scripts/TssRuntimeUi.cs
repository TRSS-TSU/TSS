using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

public sealed class TssRuntimeUi : MonoBehaviour
{
    public static TssRuntimeUi Instance { get; private set; }

    [SerializeField] private TssTrainingSession session;

    private Canvas _canvas;
    private Text _heldText;
    private GameObject _cancelHeldButton;
    private GameObject _casePanel;
    private GameObject _sopPanel;
    private EquipmentCaseStation _openCase;

    private void Awake()
    {
        Instance = this;
        if (!session)
            session = FindFirstObjectByType<TssTrainingSession>();
        BuildUi();
    }

    private void OnEnable()
    {
        if (session)
            session.StateChanged += Refresh;
    }

    private void OnDisable()
    {
        if (session)
            session.StateChanged -= Refresh;
    }

    private void Start()
    {
        Refresh();
    }

    public void ShowCasePanel(EquipmentCaseStation station)
    {
        _openCase = station;
        _casePanel.SetActive(true);
        Refresh();
    }

    public void HideCasePanel(EquipmentCaseStation station)
    {
        if (_openCase == station)
            _casePanel.SetActive(false);
    }

    private void BuildUi()
    {
        EnsureEventSystem();
        _canvas = new GameObject("TSS Runtime UGUI", typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster)).GetComponent<Canvas>();
        _canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        _heldText = Text("Held: none", _canvas.transform, new Vector2(12f, -12f), new Vector2(280f, 40f), 18, TextAnchor.MiddleLeft);
        _cancelHeldButton = Button("Cancel Hold", _canvas.transform, new Vector2(304f, -14f), new Vector2(118f, 34f), CancelHeld).gameObject;
        Button("SOP", _canvas.transform, new Vector2(-96f, -12f), new Vector2(84f, 36f), ToggleSop);
        BuildCasePanel();
        BuildSopPanel();
    }

    private void BuildCasePanel()
    {
        _casePanel = Panel("Case Inventory", new Vector2(0f, 0f), new Vector2(520f, 520f));
        _casePanel.SetActive(false);
    }

    private void BuildSopPanel()
    {
        _sopPanel = Panel("SOP Guide", new Vector2(-12f, -58f), new Vector2(420f, 520f), TextAnchor.UpperRight);
        _sopPanel.SetActive(false);
    }

    private void Refresh()
    {
        if (!session)
            return;

        _heldText.text = session.HeldItem ? $"Held: {session.HeldItem.displayName}" : "Held: none";
        if (_cancelHeldButton)
            _cancelHeldButton.SetActive(session.HeldItem);
        RefreshCasePanel();
        RefreshSopPanel();
    }

    private void RefreshCasePanel()
    {
        if (!_casePanel || !_casePanel.activeSelf || !session)
            return;

        ClearChildren(_casePanel.transform, 1);
        Text(_openCase ? _openCase.StationName : "Equipment Case", _casePanel.transform, new Vector2(0f, -52f), new Vector2(460f, 32f), 22, TextAnchor.MiddleCenter);
        var y = -100f;
        foreach (var item in session.GetScenarioInventory())
        {
            if (item == null || !item.equipment)
                continue;

            var equipment = item.equipment;
            Text($"{equipment.displayName}  x{session.GetRemaining(equipment)}", _casePanel.transform, new Vector2(-112f, y), new Vector2(270f, 32f), 16, TextAnchor.MiddleLeft);
            Button("Take", _casePanel.transform, new Vector2(154f, y), new Vector2(96f, 30f), () => _openCase.Checkout(equipment));
            y -= 38f;
        }

        Button("Return Held", _casePanel.transform, new Vector2(-144f, 202f), new Vector2(132f, 34f), () => _openCase.ReturnHeld());
        Button("Cancel", _casePanel.transform, new Vector2(30f, 202f), new Vector2(96f, 34f), () => _openCase.CancelSelection());
        Button("Close", _casePanel.transform, new Vector2(152f, 202f), new Vector2(96f, 34f), () => _openCase.ClosePanel());
    }

    private void RefreshSopPanel()
    {
        if (!_sopPanel || !_sopPanel.activeSelf || !session || !session.Scenario)
            return;

        ClearChildren(_sopPanel.transform, 1);
        var y = -62f;
        foreach (var section in session.Scenario.sopSections)
        {
            Text(section.title, _sopPanel.transform, new Vector2(0f, y), new Vector2(360f, 28f), 18, TextAnchor.MiddleLeft);
            y -= 28f;
            Text(section.body, _sopPanel.transform, new Vector2(0f, y), new Vector2(360f, 92f), 14, TextAnchor.UpperLeft);
            y -= 106f;
        }
    }

    private void ToggleSop()
    {
        _sopPanel.SetActive(!_sopPanel.activeSelf);
        Refresh();
    }

    private void CancelHeld()
    {
        session?.ReturnHeldItem(out _);
    }

    private GameObject Panel(string title, Vector2 anchoredPosition, Vector2 size, TextAnchor anchor = TextAnchor.MiddleCenter)
    {
        var go = new GameObject(title, typeof(Image));
        go.transform.SetParent(_canvas.transform, false);
        var rect = go.GetComponent<RectTransform>();
        rect.sizeDelta = size;
        rect.anchoredPosition = anchoredPosition;
        if (anchor == TextAnchor.UpperRight)
        {
            rect.anchorMin = rect.anchorMax = rect.pivot = new Vector2(1f, 1f);
        }
        else
        {
            rect.anchorMin = rect.anchorMax = rect.pivot = new Vector2(0.5f, 0.5f);
        }
        go.GetComponent<Image>().color = new Color(0.04f, 0.05f, 0.06f, 0.94f);
        Text(title, go.transform, new Vector2(0f, -18f), new Vector2(size.x - 40f, 32f), 22, TextAnchor.MiddleCenter);
        return go;
    }

    private static Text Text(string label, Transform parent, Vector2 anchoredPosition, Vector2 size, int fontSize, TextAnchor alignment)
    {
        var go = new GameObject(label, typeof(Text));
        go.transform.SetParent(parent, false);
        var rect = go.GetComponent<RectTransform>();
        rect.anchorMin = rect.anchorMax = rect.pivot = new Vector2(0.5f, 1f);
        rect.anchoredPosition = anchoredPosition;
        rect.sizeDelta = size;
        var text = go.GetComponent<Text>();
        text.text = label;
        text.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        text.fontSize = fontSize;
        text.alignment = alignment;
        text.color = Color.white;
        return text;
    }

    private static Button Button(string label, Transform parent, Vector2 anchoredPosition, Vector2 size, UnityEngine.Events.UnityAction onClick)
    {
        var go = new GameObject(label, typeof(Image), typeof(Button));
        go.transform.SetParent(parent, false);
        var rect = go.GetComponent<RectTransform>();
        var topRight = parent.GetComponent<Canvas>();
        rect.anchorMin = rect.anchorMax = rect.pivot = topRight ? new Vector2(1f, 1f) : new Vector2(0.5f, 1f);
        rect.anchoredPosition = anchoredPosition;
        rect.sizeDelta = size;
        go.GetComponent<Image>().color = new Color(0.18f, 0.26f, 0.35f, 1f);
        var button = go.GetComponent<Button>();
        button.onClick.AddListener(onClick);
        Text(label, go.transform, Vector2.zero, size, 15, TextAnchor.MiddleCenter);
        return button;
    }

    private static void ClearChildren(Transform parent, int keepFirst)
    {
        for (var i = parent.childCount - 1; i >= keepFirst; i--)
            Destroy(parent.GetChild(i).gameObject);
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
