using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

public sealed class TssResultsView : MonoBehaviour
{
    [SerializeField] private GameObject resultsContent;
    [SerializeField] private GameObject noResultsState;
    [SerializeField] private Text overallSummary;
    [SerializeField] private Text scenarioText;
    [SerializeField] private Transform workPackageContainer;
    [SerializeField] private GameObject resultCardPrefab;
    [SerializeField] private Button mainMenuButton;
    [SerializeField] private Mask resultsMask;
    [SerializeField] private GameObject deviceConfigurationRoot;
    [SerializeField] private Transform deviceTabContainer;
    [SerializeField] private Text deviceConfigurationText;
    [SerializeField] private ScrollRect panelScroll;
    [SerializeField] private Button previousPanelButton;
    [SerializeField] private Button nextPanelButton;
    private readonly List<Button> _deviceTabs = new();
    private IReadOnlyList<TssDeviceConfigurationSnapshot> _devices;
    private int _deviceIndex;
    private int _panelIndex;
    private Coroutine _panelTransition;
    private const float PanelTransitionSeconds = .28f;

    private void Awake()
    {
        if (resultsMask) resultsMask.enabled = true;
        if (mainMenuButton)
            mainMenuButton.onClick.AddListener(ReturnToMainMenu);
        if (previousPanelButton) previousPanelButton.onClick.AddListener(() => ShowPanel(_panelIndex - 1));
        if (nextPanelButton) nextPanelButton.onClick.AddListener(() => ShowPanel(_panelIndex + 1));
        if (panelScroll)
        {
            panelScroll.scrollSensitivity = 0f;
            panelScroll.inertia = false;
        }
        Render(TssResultsHandoff.Snapshot);
    }

    private void Render(TssResultsSnapshot snapshot)
    {
        var hasResults = snapshot != null && snapshot.Assessment != null;
        if (resultsContent) resultsContent.SetActive(hasResults);
        if (noResultsState) noResultsState.SetActive(!hasResults);
        if (!hasResults) return;

        var assessment = snapshot.Assessment;
        if (scenarioText)
            scenarioText.text = string.IsNullOrWhiteSpace(assessment.ScenarioName) ? "Scenario unavailable" : assessment.ScenarioName;
        if (overallSummary)
        {
            overallSummary.text = assessment.IsComplete
                ? "COMPLETE — all four work packages passed at exit."
                : "INCOMPLETE — review the remaining criteria below.";
            overallSummary.color = assessment.IsComplete ? new Color(.35f, 1f, .55f) : new Color(1f, .75f, .3f);
        }

        if (!workPackageContainer || !resultCardPrefab) return;
        var pages = new List<Transform>();
        for (var i = 0; i < Mathf.Min(4, workPackageContainer.childCount); i++) pages.Add(workPackageContainer.GetChild(i));
        for (var i = 0; i < pages.Count; i++)
            for (var child = pages[i].childCount - 1; child >= 0; child--)
                Destroy(pages[i].GetChild(child).gameObject);
        var workPackageIndex = 0;
        foreach (var workPackage in assessment.WorkPackages)
        {
            if (workPackageIndex >= pages.Count) break;
            var card = Instantiate(resultCardPrefab, pages[workPackageIndex++]).GetComponent<TssResultsCard>();
            if (card) card.Bind(workPackage);
        }
        ShowPanel(0);
        RenderDeviceConfigurations(snapshot.DeviceConfigurations);
    }

    private void ShowPanel(int index)
    {
        var panelCount = workPackageContainer ? workPackageContainer.childCount : 0;
        _panelIndex = Mathf.Clamp(index, 0, Mathf.Max(0, panelCount - 1));
        if (panelScroll && panelCount > 1)
        {
            var target = (float)_panelIndex / (panelCount - 1);
            if (_panelTransition != null) StopCoroutine(_panelTransition);
            _panelTransition = StartCoroutine(AnimatePanel(panelScroll.horizontalNormalizedPosition, target));
        }
        if (previousPanelButton) previousPanelButton.interactable = _panelIndex > 0;
        if (nextPanelButton) nextPanelButton.interactable = _panelIndex < panelCount - 1;
    }

    private IEnumerator AnimatePanel(float from, float to)
    {
        var elapsed = 0f;
        while (elapsed < PanelTransitionSeconds)
        {
            elapsed += Time.unscaledDeltaTime;
            panelScroll.horizontalNormalizedPosition = Mathf.SmoothStep(from, to, Mathf.Clamp01(elapsed / PanelTransitionSeconds));
            yield return null;
        }
        panelScroll.horizontalNormalizedPosition = to;
        _panelTransition = null;
    }

    private void RenderDeviceConfigurations(IReadOnlyList<TssDeviceConfigurationSnapshot> devices)
    {
        if (!deviceConfigurationRoot) return;
        deviceConfigurationRoot.SetActive(true);
        if (devices == null || devices.Count == 0)
        {
            if (deviceConfigurationText) deviceConfigurationText.text = "No configurable network devices were present.";
            if (deviceTabContainer) for (var i = deviceTabContainer.childCount - 1; i >= 0; i--) Destroy(deviceTabContainer.GetChild(i).gameObject);
            return;
        }
        for (var i = deviceTabContainer ? deviceTabContainer.childCount - 1 : -1; i >= 0; i--) Destroy(deviceTabContainer.GetChild(i).gameObject);
        _deviceTabs.Clear();
        for (var i = 0; i < devices.Count; i++)
        {
            var index = i;
            var buttonObject = new GameObject("DeviceTab", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image), typeof(Button));
            buttonObject.transform.SetParent(deviceTabContainer, false);
            var rect = (RectTransform)buttonObject.transform; rect.sizeDelta = new Vector2(260f, 42f);
            var button = buttonObject.GetComponent<Button>(); button.targetGraphic = buttonObject.GetComponent<Image>();
            var text = new GameObject("Text", typeof(RectTransform), typeof(CanvasRenderer), typeof(Text)).GetComponent<Text>(); text.transform.SetParent(buttonObject.transform, false); text.rectTransform.sizeDelta = rect.sizeDelta; text.alignment = TextAnchor.MiddleCenter; text.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf"); text.fontSize = 17; text.color = Color.white; text.text = TabLabel(devices[i]);
            button.onClick.AddListener(() => SelectDevice(devices, index)); _deviceTabs.Add(button);
        }
        SelectDevice(devices, 0);
    }

    private void SelectDevice(IReadOnlyList<TssDeviceConfigurationSnapshot> devices, int index)
    {
        _devices = devices;
        _deviceIndex = index;
        for (var i = 0; i < _deviceTabs.Count; i++) _deviceTabs[i].image.color = i == index ? new Color(.18f, .55f, .28f) : new Color(.12f, .18f, .2f);
        var device = devices[index];
        if (!device.HasTerminalState)
        {
            if (deviceConfigurationText) deviceConfigurationText.text = device.StateMessage;
            return;
        }
        if (deviceConfigurationText)
        {
            deviceConfigurationText.supportRichText = true;
            var starting = device.IsStartingConfiguration ? "<color=#FFFFFF>STARTING CONFIGURATION — NO TERMINAL CHANGES RECORDED</color>\n\n" : string.Empty;
            deviceConfigurationText.text = starting + TssCliCommandProcessor.BuildResultsShowReview(device.Configuration, device.PossibleIncorrectConfiguration);
        }
    }

    private static string TabLabel(TssDeviceConfigurationSnapshot device) => (device.ExpectedRole.Length == 0 ? "EXTRA " : string.Empty) + device.Category.ToString().ToUpperInvariant() + " - " + device.Label;

    private void ReturnToMainMenu()
    {
        TssResultsHandoff.Clear();
        SceneManager.LoadScene("MainMenu");
    }
}
