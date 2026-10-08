using System;
using System.Collections.Generic;
using System.Text;
using StarterAssets;
using UnityEngine;
using UnityEngine.UI;

public sealed class TssRuntimeUi : MonoBehaviour
{
    public static TssRuntimeUi Instance { get; private set; }

    [Header("Dependencies")]
    [SerializeField]
    private TssTrainingSession session;

    [SerializeField]
    private StarterAssetsInputs playerInput;

    [SerializeField]
    private ThirdPersonController playerController;

    [SerializeField]
    private Animator playerAnimator;

    [SerializeField]
    private bool enableDebugValidationPanel;

    [Header("UI Prefabs")]
    [SerializeField]
    private GameObject runtimeCanvasPrefab;

    [SerializeField]
    private GameObject inventoryRowPrefab;

    [SerializeField]
    private GameObject sopSectionRowPrefab;

    private Canvas _canvas;
    private Transform _hudRoot;
    private Text _heldText;
    private Text _objectiveText;
    private Text _contextText;
    private Text _progressText;
    private GameObject _cancelHeldButton;
    private Button _sopButton;
    private Button _connectionsButton;
    private Button _debugButton;
    private readonly List<Button> _hudInfoButtons = new();
    private readonly List<GameObject> _hudSlidePanels = new();
    private readonly List<Text> _hudSlideTitleTexts = new();
    private readonly List<Text> _hudSlideBodyTexts = new();
    private int _activeHudSlidePanel = -1;
    private GameObject _sopSlidePanel;
    private GameObject _wp1SlidePanel;
    private Text _wp1SlideTitleText;
    private Text _wp1SlideBodyText;
    private int _activeRightSlidePanel = -1;

    private GameObject _casePanel;
    private Text _caseTitleText;
    private Transform _caseItemsContainer;
    private Button _caseReturnHeldButton;
    private Button _caseCancelButton;
    private Button _caseCloseButton;

    private GameObject _sopPanel;
    private Transform _sopTabsContainer;
    private Transform _sopContentContainer;
    private int _selectedSopSectionIndex;

    private GameObject _namePromptPanel;
    private Text _namePromptTitle;
    private InputField _namePromptInput;
    private Button _nameConfirmButton;
    private Button _nameCancelButton;

    private GameObject _correctionPanel;
    private Text _correctionTitle;
    private InputField _correctionInput;
    private Button _corrRenameButton;
    private Button _corrPickupButton;
    private Button _corrAccessComputerButton;
    private Button _corrCloseButton;

    private GameObject _computerPanel;
    private Text _computerTitle;
    private InputField _computerInput;
    private Button _computerCloseButton;

    private GameObject _debugPanel;
    private Text _debugText;

    private EquipmentCaseStation _openCase;
    private object _activeMenuOwner;
    private Action<string> _namePromptConfirmed;
    private Action<string> _renameConfirmed;
    private Action _pickupConfirmed;
    private Action _accessComputerConfirmed;
    private string _statusMessage;
    private float _statusUntil;
    private bool _controlsLocked;
    private CursorLockMode _previousCursorLockMode;
    private bool _previousCursorVisible;
    private bool _previousPlayerControllerEnabled;
    private readonly HashSet<object> _externalGameplayLocks = new();
    private static readonly int SpeedHash = Animator.StringToHash("Speed");
    private static readonly int MotionSpeedHash = Animator.StringToHash("MotionSpeed");
    private static readonly string[] HudInfoNames = { "Placement", "Cables", "Permanent", "Effective" };
    private static readonly Vector2 HudSlidePanelHiddenPosition = new(-1150f, 0f);
    private static readonly Vector2 HudSlidePanelShownPosition = new(20f, 0f);
    private static readonly Vector2 HudRightSlidePanelHiddenPosition = new(1150f, 0f);
    private static readonly Vector2 HudRightSlidePanelShownPosition = new(-20f, 0f);

    public bool IsGameplayInputBlocked => IsAnyGameplayMenuOpen();

    private void Awake()
    {
        Instance = this;
        if (!session)
            session = FindFirstObjectByType<TssTrainingSession>();
        if (!playerInput)
            playerInput = FindFirstObjectByType<StarterAssetsInputs>();
        if (!playerController)
            playerController = FindFirstObjectByType<ThirdPersonController>();
        if (!playerAnimator && playerController)
            playerAnimator = playerController.GetComponentInChildren<Animator>();
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

    private void Update()
    {
        ApplyGameplayInputLock(IsAnyGameplayMenuOpen());
        if (_contextText && !string.IsNullOrEmpty(_statusMessage) && Time.time >= _statusUntil)
        {
            _statusMessage = null;
            RefreshHudAdditions();
        }
        AnimateHudSlidePanels();

        if (!IsGameplayInputBlocked)
            return;

        if (!playerInput)
            playerInput = FindFirstObjectByType<StarterAssetsInputs>();

        if (!playerInput)
            return;

        playerInput.MoveInput(Vector2.zero);
        playerInput.LookInput(Vector2.zero);
        playerInput.JumpInput(false);
        playerInput.SprintInput(false);
        ForcePlayerIdle();
    }

    public void ShowCasePanel(EquipmentCaseStation station)
    {
        CloseActiveMenu();
        _openCase = station;
        _activeMenuOwner = station;
        if (_casePanel)
            _casePanel.SetActive(true);
        Refresh();
        ApplyGameplayInputLock(true);
    }

    public void ShowCaseMenu(EquipmentCaseStation station)
    {
        ShowCasePanel(station);
    }

    public void HideCasePanel(EquipmentCaseStation station)
    {
        if (_openCase != station)
            return;

        if (_casePanel)
            _casePanel.SetActive(false);

        _openCase = null;
        ClearOwner(station);
        ApplyGameplayInputLock(IsAnyGameplayMenuOpen());
    }

    public void CloseActiveMenu()
    {
        CloseActiveMenu(null);
    }

    public void CloseActiveMenu(object owner)
    {
        if (owner != null && _activeMenuOwner != owner)
            return;

        if (_casePanel)
            _casePanel.SetActive(false);
        if (_namePromptPanel)
            _namePromptPanel.SetActive(false);
        if (_correctionPanel)
            _correctionPanel.SetActive(false);
        if (_computerPanel)
            _computerPanel.SetActive(false);

        _openCase = null;
        _activeMenuOwner = null;
        _namePromptConfirmed = null;
        _renameConfirmed = null;
        _pickupConfirmed = null;
        _accessComputerConfirmed = null;
        ApplyGameplayInputLock(IsAnyGameplayMenuOpen());
    }

    public void PushExternalGameplayLock(object owner)
    {
        if (owner == null)
            return;

        _externalGameplayLocks.Add(owner);
        ApplyGameplayInputLock(true);
    }

    public void PopExternalGameplayLock(object owner)
    {
        if (owner == null)
            return;

        _externalGameplayLocks.Remove(owner);
        ApplyGameplayInputLock(IsAnyGameplayMenuOpen());
    }

    private void BuildUi()
    {
        EnsureEventSystem();

        GameObject rootInstance;
        if (runtimeCanvasPrefab)
        {
            rootInstance = Instantiate(runtimeCanvasPrefab);
        }
        else
        {
            Debug.LogWarning("TssRuntimeUi requires a runtime canvas prefab.");
            return;
        }

        rootInstance.name = "TSS Runtime UGUI (Prefab)";
        _canvas = rootInstance.GetComponent<Canvas>();

        // Wire HUD
        _hudRoot = rootInstance.transform.Find("HUD");
        if (_hudRoot)
        {
            _hudRoot.gameObject.SetActive(true);
            var hudRect = _hudRoot as RectTransform;
            if (hudRect)
            {
                hudRect.anchorMin = Vector2.zero;
                hudRect.anchorMax = Vector2.one;
                hudRect.offsetMin = Vector2.zero;
                hudRect.offsetMax = Vector2.zero;
                hudRect.pivot = new Vector2(0.5f, 0.5f);
            }
        }
        var heldTextTransform = rootInstance.transform.Find("HUD/HeldText");
        if (heldTextTransform)
        {
            _heldText = heldTextTransform.GetComponent<Text>();
        }

        var cancelBtnTransform = rootInstance.transform.Find("HUD/CancelHeldButton");
        if (cancelBtnTransform)
        {
            _cancelHeldButton = cancelBtnTransform.gameObject;
            var btn = cancelBtnTransform.GetComponent<Button>();
            if (btn)
                btn.onClick.AddListener(CancelHeld);
        }

        var sopBtnTransform = rootInstance.transform.Find("HUD/SopButton");
        if (sopBtnTransform)
        {
            _sopButton = sopBtnTransform.GetComponent<Button>();
            if (_sopButton)
                _sopButton.onClick.AddListener(() => ToggleRightSlidePanel(0));
        }

        BuildHudAdditions();

        var debugBtnTransform = rootInstance.transform.Find("HUD/DebugButton");
        if (debugBtnTransform)
        {
            _debugButton = debugBtnTransform.GetComponent<Button>();
            if (_debugButton)
            {
                if (enableDebugValidationPanel)
                    _debugButton.onClick.AddListener(() => ToggleRightSlidePanel(1));
                else
                    _debugButton.gameObject.SetActive(false);
            }
        }

        // Wire CasePanel
        var casePanelTransform = rootInstance.transform.Find("CasePanel");
        if (casePanelTransform)
        {
            _casePanel = casePanelTransform.gameObject;
            var titleTransform = casePanelTransform.Find("StationTitle");
            if (titleTransform)
                _caseTitleText = titleTransform.GetComponent<Text>();
            _caseItemsContainer = casePanelTransform.Find("ItemsContainer");

            var retBtnTransform = casePanelTransform.Find("Buttons/ReturnHeldButton") ?? casePanelTransform.Find("ReturnHeldButton");
            _caseReturnHeldButton = retBtnTransform?.GetComponent<Button>();
            if (_caseReturnHeldButton)
                _caseReturnHeldButton.onClick.AddListener(CancelHeld);

            var caseCancelBtnTransform = casePanelTransform.Find("Buttons/CancelButton") ?? casePanelTransform.Find("CancelButton");
            var cancelBtn = caseCancelBtnTransform?.GetComponent<Button>();
            if (cancelBtn)
                cancelBtn.onClick.AddListener(() => _openCase?.CancelSelection());

            var closeBtnTransform = casePanelTransform.Find("Buttons/CloseButton") ?? casePanelTransform.Find("CloseButton");
            var closeBtn = closeBtnTransform?.GetComponent<Button>();
            if (closeBtn)
                closeBtn.onClick.AddListener(() => _openCase?.ClosePanel());
        }

        // Wire SopPanel
        var sopPanelTransform = rootInstance.transform.Find("SopPanel");
        if (sopPanelTransform)
        {
            _sopPanel = sopPanelTransform.gameObject;
            _sopPanel.SetActive(true);
            _sopTabsContainer = sopPanelTransform.Find("SopTabs");
            _sopContentContainer = sopPanelTransform.Find("SopContent");
        }
        BuildRightSlidePanels();

        // Wire NamePromptPanel
        var namePanelTransform = rootInstance.transform.Find("NamePromptPanel");
        if (namePanelTransform)
        {
            _namePromptPanel = namePanelTransform.gameObject;
            _namePromptTitle = namePanelTransform.Find("Title")?.GetComponent<Text>();
            _namePromptInput = namePanelTransform.Find("DeviceInput")?.GetComponent<InputField>();
            var confirmBtn = namePanelTransform.Find("ConfirmButton")?.GetComponent<Button>();
            if (confirmBtn)
                confirmBtn.onClick.AddListener(ConfirmNamePrompt);
            var cancelBtn = namePanelTransform.Find("CancelButton")?.GetComponent<Button>();
            if (cancelBtn)
                cancelBtn.onClick.AddListener(HideNamePrompt);
        }

        // Wire CorrectionPanel
        var corrPanelTransform = rootInstance.transform.Find("CorrectionPanel");
        if (corrPanelTransform)
        {
            _correctionPanel = corrPanelTransform.gameObject;
            _correctionTitle = corrPanelTransform.Find("Title")?.GetComponent<Text>();
            _correctionInput = corrPanelTransform.Find("DeviceInput")?.GetComponent<InputField>();

            var renameBtn = corrPanelTransform.Find("RenameButton")?.GetComponent<Button>();
            if (renameBtn)
                renameBtn.onClick.AddListener(ConfirmRename);
            _corrRenameButton = renameBtn;
            var pickupBtn = corrPanelTransform.Find("PickupButton")?.GetComponent<Button>();
            if (pickupBtn)
                pickupBtn.onClick.AddListener(ConfirmPickup);
            _corrPickupButton = pickupBtn;
            _corrAccessComputerButton = corrPanelTransform
                .Find("AccessComputerButton")
                ?.GetComponent<Button>();
            if (_corrAccessComputerButton)
                _corrAccessComputerButton.onClick.AddListener(ConfirmAccessComputer);
            else
                Debug.LogWarning("CorrectionPanel is missing AccessComputerButton.");

            var closeBtn = corrPanelTransform.Find("CloseButton")?.GetComponent<Button>();
            if (closeBtn)
                closeBtn.onClick.AddListener(HideCorrectionPanel);
        }

        var computerPanelTransform = rootInstance.transform.Find("ComputerPanel");
        if (computerPanelTransform)
            WireComputerPanel(computerPanelTransform);
        else
            Debug.LogWarning("TssRuntimeUi runtime canvas is missing ComputerPanel.");

        // Wire DebugPanel
        var debugPanelTransform = rootInstance.transform.Find("DebugPanel");
        if (debugPanelTransform)
        {
            _debugPanel = debugPanelTransform.gameObject;
            _debugText = debugPanelTransform.Find("DebugOutputText")?.GetComponent<Text>();
        }
    }

    private void Refresh()
    {
        if (!session)
            return;

        if (_heldText)
            _heldText.text =
                session.HeldItem ? $"Held: {session.HeldItem.displayName}"
                : session.HeldCable ? $"Held cable: {session.HeldCable.displayName}"
                : "Held: none";

        if (_cancelHeldButton)
            _cancelHeldButton.SetActive(session.HeldItem != null || session.HeldCable != null);

        RefreshHudAdditions();
        RefreshCasePanel();
        RefreshSopPanel();
        RefreshDebugPanel();
    }

    public void ShowHudMessage(string message)
    {
        if (string.IsNullOrWhiteSpace(message))
            return;

        _statusMessage = message.Trim();
        _statusUntil = Time.time + 3f;
        RefreshHudAdditions();
    }

    public void ShowNamePrompt(
        string title,
        string initialValue,
        Action<string> onConfirm,
        object owner = null
    )
    {
        CloseActiveMenu();
        _activeMenuOwner = owner;
        _namePromptConfirmed = onConfirm;
        if (_namePromptTitle)
            _namePromptTitle.text = title;
        if (_namePromptInput)
        {
            _namePromptInput.text = initialValue ?? string.Empty;
        }
        if (_namePromptPanel)
            _namePromptPanel.SetActive(true);
        if (_correctionPanel)
            _correctionPanel.SetActive(false);
        if (_computerPanel)
            _computerPanel.SetActive(false);
        if (_namePromptInput)
            _namePromptInput.ActivateInputField();
        ApplyGameplayInputLock(true);
    }

    public void ShowPlacementCorrection(
        string title,
        string currentName,
        Action<string> onRename,
        Action onPickup,
        Action onInspect = null
    )
    {
        ShowEndpointMenu(title, true, true, onInspect != null, onRename, onPickup, onInspect, null, currentName);
    }

    public void ShowEndpointMenu(
        string title,
        bool canRename,
        bool canPickup,
        bool canAccessComputer,
        Action<string> onRename,
        Action onPickup,
        Action onAccessComputer,
        object owner = null,
        string currentName = ""
    )
    {
        CloseActiveMenu();
        _activeMenuOwner = owner;
        _renameConfirmed = onRename;
        _pickupConfirmed = onPickup;
        _accessComputerConfirmed = onAccessComputer;
        if (_correctionTitle)
            _correctionTitle.text = title;
        if (_correctionInput)
        {
            _correctionInput.text = currentName ?? string.Empty;
        }
        if (_corrRenameButton)
            _corrRenameButton.gameObject.SetActive(canRename);
        if (_corrPickupButton)
            _corrPickupButton.gameObject.SetActive(canPickup);
        if (_corrAccessComputerButton)
        {
            _corrAccessComputerButton.gameObject.SetActive(canAccessComputer);
            var btnText = _corrAccessComputerButton.GetComponentInChildren<Text>(true);
            if (btnText)
                btnText.text = "Inspect Device";
        }
        if (_correctionPanel)
            _correctionPanel.SetActive(true);
        if (_namePromptPanel)
            _namePromptPanel.SetActive(false);
        if (_computerPanel)
            _computerPanel.SetActive(false);
        if (_correctionInput && canRename)
            _correctionInput.ActivateInputField();
        ApplyGameplayInputLock(true);
    }

    public void ShowComputerMenu(string title, object owner = null, string message = "")
    {
        CloseActiveMenu();
        _activeMenuOwner = owner;
        if (_computerTitle)
            _computerTitle.text = title;
        if (_computerInput)
            _computerInput.text = message ?? string.Empty;
        if (_computerPanel)
            _computerPanel.SetActive(true);
        if (_namePromptPanel)
            _namePromptPanel.SetActive(false);
        if (_correctionPanel)
            _correctionPanel.SetActive(false);
        if (_computerInput)
            _computerInput.ActivateInputField();
        ApplyGameplayInputLock(true);
    }

    private void RefreshCasePanel()
    {
        if (!_casePanel || !_casePanel.activeSelf || !session || !_caseItemsContainer)
            return;

        if (_caseTitleText)
            _caseTitleText.text = _openCase ? _openCase.StationName : "Equipment Case";

        if (_caseReturnHeldButton)
        {
            var hasHeld = session.HeldItem != null || session.HeldCable != null;
            _caseReturnHeldButton.interactable = hasHeld;
            var retText = _caseReturnHeldButton.GetComponentInChildren<Text>();
            if (retText)
                retText.color = hasHeld ? Color.white : new Color(0.55f, 0.58f, 0.60f, 0.75f);
        }

        ClearChildren(_caseItemsContainer);

        var y = 0f;
        foreach (var item in session.GetScenarioInventory())
        {
            if (item == null || !item.equipment)
                continue;

            var equipment = item.equipment;
            GameObject rowInstance;
            if (inventoryRowPrefab)
            {
                rowInstance = Instantiate(inventoryRowPrefab, _caseItemsContainer, false);
            }
            else
            {
                Debug.LogWarning("TssRuntimeUi requires an inventory row prefab.");
                return;
            }

            var rt = rowInstance.GetComponent<RectTransform>();
            if (rt)
            {
                rt.anchorMin = new Vector2(0, 1);
                rt.anchorMax = new Vector2(1, 1);
                rt.pivot = new Vector2(0.5f, 1);
                rt.sizeDelta = new Vector2(0, rt.sizeDelta.y);
                rt.anchoredPosition = new Vector2(0, y);
            }

            var remaining = session.GetRemaining(equipment);
            var label = rowInstance.transform.Find("ItemLabel")?.GetComponent<Text>();
            if (label)
                label.text = $"{equipment.displayName}  x{remaining}";

            var btn = rowInstance.transform.Find("TakeButton")?.GetComponent<Button>();
            if (btn)
            {
                btn.onClick.RemoveAllListeners();
                var hasStock = remaining > 0;
                btn.interactable = hasStock;

                var btnText = btn.GetComponentInChildren<Text>();
                if (btnText)
                    btnText.color = hasStock ? Color.white : new Color(0.55f, 0.58f, 0.60f, 0.75f);

                if (hasStock)
                    btn.onClick.AddListener(() => _openCase?.Checkout(equipment));
            }

            y -= 52f;
        }

        foreach (var item in session.GetScenarioCableInventory())
        {
            if (item == null || !item.cable)
                continue;

            var cable = item.cable;
            GameObject rowInstance;
            if (inventoryRowPrefab)
            {
                rowInstance = Instantiate(inventoryRowPrefab, _caseItemsContainer, false);
            }
            else
            {
                Debug.LogWarning("TssRuntimeUi requires an inventory row prefab.");
                return;
            }

            var rt = rowInstance.GetComponent<RectTransform>();
            if (rt)
            {
                rt.anchorMin = new Vector2(0, 1);
                rt.anchorMax = new Vector2(1, 1);
                rt.pivot = new Vector2(0.5f, 1);
                rt.sizeDelta = new Vector2(0, rt.sizeDelta.y);
                rt.anchoredPosition = new Vector2(0, y);
            }

            var remainingCables = session.GetRemaining(cable);
            var label = rowInstance.transform.Find("ItemLabel")?.GetComponent<Text>();
            if (label)
                label.text = $"{cable.displayName} cable  x{remainingCables}";

            var btn = rowInstance.transform.Find("TakeButton")?.GetComponent<Button>();
            if (btn)
            {
                btn.onClick.RemoveAllListeners();
                var hasStock = remainingCables > 0;
                btn.interactable = hasStock;

                var btnText = btn.GetComponentInChildren<Text>();
                if (btnText)
                    btnText.color = hasStock ? Color.white : new Color(0.55f, 0.58f, 0.60f, 0.75f);

                if (hasStock)
                    btn.onClick.AddListener(() => _openCase?.CheckoutCable(cable));
            }

            y -= 52f;
        }
    }

    private void BuildHudAdditions()
    {
        if (!_hudRoot)
            return;

        _objectiveText = _hudRoot.Find("ObjectiveText")?.GetComponent<Text>();
        _contextText = _hudRoot.Find("ContextText")?.GetComponent<Text>();

        var oldProgressText = _hudRoot.Find("ProgressText");
        if (oldProgressText)
            oldProgressText.gameObject.SetActive(false);

        BuildHudInfoButtons();
        BuildHudSlidePanels();

        var oldConnectionsButton = _hudRoot.Find("ConnectionsButton");
        if (oldConnectionsButton)
        {
            _connectionsButton = oldConnectionsButton.GetComponent<Button>();
            oldConnectionsButton.gameObject.SetActive(false);
        }
    }

    private void BuildHudInfoButtons()
    {
        _hudInfoButtons.Clear();

        for (var i = 0; i < HudInfoNames.Length; i++)
        {
            var index = i;
            var button = _hudRoot.Find($"HudInfo{HudInfoNames[i]}Button")?.GetComponent<Button>();
            if (!button)
                continue;

            button.onClick.RemoveAllListeners();
            button.onClick.AddListener(() => ToggleHudSlidePanel(index));
            _hudInfoButtons.Add(button);
        }
    }

    private void BuildHudSlidePanels()
    {
        _hudSlidePanels.Clear();
        _hudSlideTitleTexts.Clear();
        _hudSlideBodyTexts.Clear();

        for (var i = 0; i < HudInfoNames.Length; i++)
        {
            var panelTransform = _hudRoot.Find($"Hud{HudInfoNames[i]}SlidePanel");
            if (!panelTransform)
                continue;

            var panel = panelTransform.gameObject;
            var title = panelTransform.Find("Title")?.GetComponent<Text>();
            var body = panelTransform.Find("Body")?.GetComponent<Text>();
            _hudSlidePanels.Add(panel);
            _hudSlideTitleTexts.Add(title);
            _hudSlideBodyTexts.Add(body);
        }
    }

    private void BuildRightSlidePanels()
    {
        _sopSlidePanel = _sopPanel;

        var wp1 = _hudRoot.Find("HudWp1SlidePanel");
        _wp1SlidePanel = wp1 ? wp1.gameObject : null;
        _wp1SlideTitleText = wp1 ? wp1.Find("Title")?.GetComponent<Text>() : null;
        _wp1SlideBodyText = wp1 ? wp1.Find("Body")?.GetComponent<Text>() : null;
    }

    private void RefreshHudAdditions()
    {
        if (!session)
            return;

        if (_objectiveText)
            _objectiveText.text = HudObjectiveText();
        if (_contextText)
            _contextText.text = Time.time < _statusUntil ? _statusMessage : HudContextText();
        if (_progressText)
            _progressText.text = HudProgressText();
        RefreshHudInfoButtons();
        RefreshHudSlidePanels();
        RefreshRightSlidePanels();
    }

    private string HudObjectiveText()
    {
        var scenarioName = session.Scenario && !string.IsNullOrWhiteSpace(session.Scenario.displayName)
            ? session.Scenario.displayName
            : "No scenario";

        foreach (var status in TssPlacementEvaluator.EvaluateRequirements(
                     session.Scenario,
                     session.Installed,
                     session.EndpointPlacements))
        {
            if (status.IsComplete)
                continue;

            var phase = string.IsNullOrWhiteSpace(status.PhaseLabel) ? "Placement" : status.PhaseLabel;
            return $"{scenarioName} | {phase}: {status.RequiredName} at {status.Location}";
        }

        return $"{scenarioName} | Connect, terminate, and verify physical paths";
    }

    private string HudContextText()
    {
        if (IsAnyGameplayMenuOpen())
            return "Menu open - close it to resume walking";

        if (session.HeldCable && session.SelectedCableEndpoint)
            return $"First port: {TssPhysicalConnectionPanel.EndpointLabel(session.SelectedCableEndpoint.EndpointId, session)} -> select second port";

        if (session.HeldCable)
            return $"Carrying {session.HeldCable.displayName} cable - select first port";

        if (session.HeldItem)
            return $"Carrying {session.HeldItem.displayName} - click a valid rack U or placement station";

        return "Walk near highlighted equipment and click to interact";
    }

    private string HudProgressText()
    {
        var completePlacements = 0;
        var totalPlacements = 0;
        foreach (var status in TssPlacementEvaluator.EvaluateRequirements(
                     session.Scenario,
                     session.Installed,
                     session.EndpointPlacements))
        {
            totalPlacements++;
            if (status.IsComplete)
                completePlacements++;
        }

        var playerCables = 0;
        var permanent = 0;
        foreach (var connection in session.PhysicalConnections)
        {
            if (connection.IsPermanent)
                permanent++;
            else
                playerCables++;
        }

        var requiredTerminations = RequiredTerminationCount();
        var savedTerminations = session.GetTerminationModuleStates().Count;
        var terminations = requiredTerminations > 0
            ? $"Terminations {savedTerminations}/{requiredTerminations}"
            : "Terminations n/a";

        return $"Placement {completePlacements}/{totalPlacements}   Cables {playerCables}   Permanent {permanent}   Effective paths {session.GetEffectivePhysicalPaths().Count}   {terminations}";
    }

    private void RefreshHudInfoButtons()
    {
        for (var i = 0; i < _hudInfoButtons.Count; i++)
        {
            var text = _hudInfoButtons[i].GetComponentInChildren<Text>(true);
            if (text)
                text.text = HudInfoButtonText(i);
        }
    }

    private void RefreshHudSlidePanels()
    {
        for (var i = 0; i < _hudSlideTitleTexts.Count; i++)
        {
            if (_hudSlideTitleTexts[i])
                _hudSlideTitleTexts[i].text = HudInfoNames[i];
            if (_hudSlideBodyTexts[i])
                _hudSlideBodyTexts[i].text = HudInfoPanelText(i);
        }
    }

    private void RefreshRightSlidePanels()
    {
        if (_wp1SlideTitleText)
            _wp1SlideTitleText.text = "WP1";
        if (_wp1SlideBodyText)
            _wp1SlideBodyText.text = Wp1SlideText();
    }

    private string HudInfoButtonText(int index)
    {
        PlacementCounts(out var completePlacements, out var totalPlacements);
        ConnectionCounts(out var playerCables, out var permanent);
        var effectivePaths = session.GetEffectivePhysicalPaths().Count;

        return index switch
        {
            0 => $"Placement {completePlacements}/{totalPlacements}",
            1 => $"Cables {playerCables}",
            2 => $"Permanent {permanent}",
            3 => $"Effective {effectivePaths}",
            _ => string.Empty
        };
    }

    private string HudInfoPanelText(int index)
    {
        return index switch
        {
            0 => PlacementPanelText(),
            1 => CablePanelText(false),
            2 => PermanentPanelText(),
            3 => EffectivePathPanelText(),
            _ => string.Empty
        };
    }

    private void PlacementCounts(out int completePlacements, out int totalPlacements)
    {
        completePlacements = 0;
        totalPlacements = 0;
        foreach (var status in TssPlacementEvaluator.EvaluateRequirements(
                     session.Scenario,
                     session.Installed,
                     session.EndpointPlacements))
        {
            totalPlacements++;
            if (status.IsComplete)
                completePlacements++;
        }
    }

    private void ConnectionCounts(out int playerCables, out int permanent)
    {
        playerCables = 0;
        permanent = 0;
        foreach (var connection in session.PhysicalConnections)
        {
            if (connection.IsPermanent)
                permanent++;
            else
                playerCables++;
        }
    }

    private string PlacementPanelText()
    {
        var builder = new StringBuilder();
        PlacementCounts(out var complete, out var total);
        builder.AppendLine($"Complete: {complete}/{total}");
        builder.AppendLine();

        foreach (var status in TssPlacementEvaluator.EvaluateRequirements(
                     session.Scenario,
                     session.Installed,
                     session.EndpointPlacements))
        {
            var phase = string.IsNullOrWhiteSpace(status.PhaseLabel) ? "Placement" : status.PhaseLabel;
            builder.Append(status.IsComplete ? "[x] " : "[ ] ");
            builder.Append(phase);
            builder.Append(": ");
            builder.Append(status.RequiredName);
            builder.Append(" at ");
            builder.AppendLine(status.Location);
        }

        return builder.Length > 0 ? builder.ToString() : "No placement requirements.";
    }

    private string CablePanelText(bool permanent)
    {
        var builder = new StringBuilder();
        foreach (var connection in session.PhysicalConnections)
        {
            if (connection.IsPermanent != permanent)
                continue;

            builder.AppendLine(TssPhysicalConnectionPanel.FormatConnectionRow(connection, session));
        }

        if (builder.Length == 0)
            return permanent ? "No permanent infrastructure connections." : "No player cable connections.";

        return builder.ToString();
    }

    private string PermanentPanelText()
    {
        var builder = new StringBuilder();
        builder.AppendLine("Permanent infrastructure");
        builder.AppendLine(CablePanelText(true));
        builder.AppendLine();
        builder.AppendLine("Terminations");

        var requiredTerminations = RequiredTerminationCount();
        var states = session.GetTerminationModuleStates();
        builder.AppendLine(requiredTerminations > 0
            ? $"{states.Count}/{requiredTerminations} saved"
            : "No required terminations");

        foreach (var state in states)
            builder.AppendLine($"{TssPhysicalConnectionPanel.EndpointLabel(state.endpointId, session)} - {state.moduleId}");

        return builder.ToString();
    }

    private string EffectivePathPanelText()
    {
        var paths = session.GetEffectivePhysicalPaths();
        if (paths.Count == 0)
            return "No effective device paths.";

        var builder = new StringBuilder();
        foreach (var path in paths)
        {
            builder.Append(TssPhysicalConnectionPanel.EndpointLabel(path.EndpointAId, session));
            builder.Append(" <-> ");
            builder.AppendLine(TssPhysicalConnectionPanel.EndpointLabel(path.EndpointBId, session));
        }

        return builder.ToString();
    }

    private string Wp1SlideText()
    {
        if (!session)
            return "No training session.";

        var result = TssPlacementEvaluator.Evaluate(
            session.Scenario,
            session.Installed,
            session.EndpointPlacements);
        var builder = new StringBuilder();
        builder.AppendLine(result.IsComplete ? "WP1 equipment placement complete." : "WP1 needs attention:");
        foreach (var message in result.Messages)
            builder.AppendLine(message);

        var cableText = PhysicalConnectionText(session);
        if (!string.IsNullOrWhiteSpace(cableText))
        {
            builder.AppendLine();
            builder.AppendLine("Physical connections");
            builder.AppendLine(cableText);
        }

        return builder.ToString();
    }

    private int RequiredTerminationCount()
    {
        if (!session.Scenario || session.Scenario.permanentConnections == null)
            return 0;

        var count = 0;
        foreach (var connection in session.Scenario.permanentConnections)
        {
            if (connection != null && connection.requiresPunchDownTermination)
                count += 2;
        }

        return count;
    }

    private void RefreshSopPanel()
    {
        if (
            !_sopPanel
            || !session
            || !session.Scenario
            || !_sopContentContainer
        )
            return;

        var sections = session.Scenario.sopSections;
        var sectionCount = sections?.Length ?? 0;
        if (sectionCount == 0)
        {
            SetSopChildrenActive(_sopContentContainer, 0);
            if (_sopTabsContainer)
                SetSopChildrenActive(_sopTabsContainer, 0);
            return;
        }

        if (_selectedSopSectionIndex >= sectionCount)
            _selectedSopSectionIndex = 0;

        RefreshSopTabs(sections, sectionCount);

        for (var i = 0; i < _sopContentContainer.childCount; i++)
            _sopContentContainer.GetChild(i).gameObject.SetActive(i == 0);

        if (_sopContentContainer.childCount == 0)
        {
            Debug.LogWarning("TssRuntimeUi SOP panel requires one prefab content row.");
            return;
        }

        var section = sections[_selectedSopSectionIndex];
        var row = _sopContentContainer.GetChild(0);

        var titleText = row.Find("SectionTitle")?.GetComponent<Text>();
        if (titleText)
            titleText.text = section.title;

        var bodyText = row.Find("SectionBody")?.GetComponent<Text>();
        if (bodyText)
            bodyText.text = section.body;
    }

    private void RefreshSopTabs(SopSection[] sections, int sectionCount)
    {
        if (!_sopTabsContainer)
            return;

        for (var i = 0; i < _sopTabsContainer.childCount; i++)
        {
            var tab = _sopTabsContainer.GetChild(i);
            var hasSection = i < sectionCount && sections[i] != null;
            tab.gameObject.SetActive(hasSection);
            if (!hasSection)
                continue;

            var label = tab.GetComponentInChildren<Text>();
            if (label)
                label.text = sections[i].title;

            var button = tab.GetComponent<Button>();
            if (!button)
                continue;

            var sectionIndex = i;
            button.onClick.RemoveAllListeners();
            button.onClick.AddListener(() =>
            {
                _selectedSopSectionIndex = sectionIndex;
                RefreshSopPanel();
            });
        }

        if (sectionCount > _sopTabsContainer.childCount)
            Debug.LogWarning(
                "TssRuntimeUi SOP panel has fewer prefab tabs than scenario SOP sections."
            );
    }

    private static void SetSopChildrenActive(Transform parent, int activeCount)
    {
        for (var i = 0; i < parent.childCount; i++)
            parent.GetChild(i).gameObject.SetActive(i < activeCount);
    }

    private void RefreshDebugPanel()
    {
        if (!_debugPanel || !_debugPanel.activeSelf || !session || !_debugText)
            return;

        var result = TssPlacementEvaluator.Evaluate(
            session.Scenario,
            session.Installed,
            session.EndpointPlacements
        );
        var placementText = result.IsComplete
            ? "WP1 equipment placement complete."
            : string.Join("\n", result.Messages);
        var cableText = PhysicalConnectionText(session);
        _debugText.text = string.IsNullOrWhiteSpace(cableText)
            ? placementText
            : $"{placementText}\n\nPhysical connections:\n{cableText}";
    }

    private void ToggleSop()
    {
        ToggleRightSlidePanel(0);
    }

    private void ToggleConnections()
    {
        ToggleHudSlidePanel(1);
    }

    private void ToggleDebug()
    {
        ToggleRightSlidePanel(1);
    }

    private void ToggleHudSlidePanel(int index)
    {
        _activeHudSlidePanel = _activeHudSlidePanel == index ? -1 : index;
        RefreshHudAdditions();
    }

    private void ToggleRightSlidePanel(int index)
    {
        _activeRightSlidePanel = _activeRightSlidePanel == index ? -1 : index;
        RefreshHudAdditions();
    }

    private void AnimateHudSlidePanels()
    {
        for (var i = 0; i < _hudSlidePanels.Count; i++)
        {
            var rect = _hudSlidePanels[i] ? _hudSlidePanels[i].GetComponent<RectTransform>() : null;
            if (!rect)
                continue;

            var target = i == _activeHudSlidePanel ? HudSlidePanelShownPosition : HudSlidePanelHiddenPosition;
            rect.anchoredPosition = Vector2.Lerp(rect.anchoredPosition, target, Time.unscaledDeltaTime * 12f);
        }

        AnimateRightSlidePanel(_sopSlidePanel, _activeRightSlidePanel == 0);
        AnimateRightSlidePanel(_wp1SlidePanel, _activeRightSlidePanel == 1);
    }

    private static void AnimateRightSlidePanel(GameObject panel, bool shown)
    {
        var rect = panel ? panel.GetComponent<RectTransform>() : null;
        if (!rect)
            return;

        var target = shown ? HudRightSlidePanelShownPosition : HudRightSlidePanelHiddenPosition;
        rect.anchoredPosition = Vector2.Lerp(rect.anchoredPosition, target, Time.unscaledDeltaTime * 12f);
    }

    private void ConfirmNamePrompt()
    {
        var onConfirm = _namePromptConfirmed;
        HideNamePrompt();
        onConfirm?.Invoke(_namePromptInput ? _namePromptInput.text : string.Empty);
    }

    private void HideNamePrompt()
    {
        if (_namePromptPanel)
            _namePromptPanel.SetActive(false);
        _namePromptConfirmed = null;
        ClearOwner(_activeMenuOwner);
        ApplyGameplayInputLock(IsAnyGameplayMenuOpen());
    }

    private void ConfirmRename()
    {
        _renameConfirmed?.Invoke(_correctionInput ? _correctionInput.text : string.Empty);
        HideCorrectionPanel();
    }

    private void ConfirmPickup()
    {
        var onPickup = _pickupConfirmed;
        HideCorrectionPanel();
        onPickup?.Invoke();
    }

    private void HideCorrectionPanel()
    {
        if (_correctionPanel)
            _correctionPanel.SetActive(false);
        _renameConfirmed = null;
        _pickupConfirmed = null;
        _accessComputerConfirmed = null;
        ClearOwner(_activeMenuOwner);
        ApplyGameplayInputLock(IsAnyGameplayMenuOpen());
    }

    private void ConfirmAccessComputer()
    {
        var onAccess = _accessComputerConfirmed;
        HideCorrectionPanel();
        onAccess?.Invoke();
    }

    private bool IsAnyGameplayMenuOpen()
    {
        return (_casePanel && _casePanel.activeSelf)
            || (_namePromptPanel && _namePromptPanel.activeSelf)
            || (_correctionPanel && _correctionPanel.activeSelf)
            || (_computerPanel && _computerPanel.activeSelf)
            || _externalGameplayLocks.Count > 0;
    }

    private void CancelHeld()
    {
        if (!session)
            return;

        if (session.HeldItem)
        {
            if (session.ReturnHeldItem(out var message))
                ShowHudMessage(message);
        }
        else if (session.HeldCable)
        {
            if (session.ReturnHeldCable(out var message))
                ShowHudMessage(message);
        }
    }

    private void ApplyGameplayInputLock(bool locked)
    {
        if (locked == _controlsLocked)
            return;

        if (!playerController)
            playerController = FindFirstObjectByType<ThirdPersonController>();
        if (!playerAnimator && playerController)
            playerAnimator = playerController.GetComponentInChildren<Animator>();

        if (locked)
        {
            _previousCursorLockMode = Cursor.lockState;
            _previousCursorVisible = Cursor.visible;
            _previousPlayerControllerEnabled = !playerController || playerController.enabled;
            Cursor.lockState = CursorLockMode.None;
            Cursor.visible = true;
            ForcePlayerIdle();
        }
        else
        {
            Cursor.lockState = _previousCursorLockMode;
            Cursor.visible = _previousCursorVisible;
        }

        if (playerController)
            playerController.enabled = locked ? false : _previousPlayerControllerEnabled;

        _controlsLocked = locked;
    }

    private void ForcePlayerIdle()
    {
        if (!playerAnimator)
            return;

        SetAnimatorFloatIfPresent(SpeedHash, 0f);
        SetAnimatorFloatIfPresent(MotionSpeedHash, 0f);
    }

    private void SetAnimatorFloatIfPresent(int parameterHash, float value)
    {
        foreach (var parameter in playerAnimator.parameters)
        {
            if (
                parameter.nameHash == parameterHash
                && parameter.type == AnimatorControllerParameterType.Float
            )
            {
                playerAnimator.SetFloat(parameterHash, value);
                return;
            }
        }
    }

    private void ClearOwner(object owner)
    {
        if (_activeMenuOwner == owner)
            _activeMenuOwner = null;
    }

    private void WireComputerPanel(Transform panelTransform)
    {
        _computerPanel = panelTransform.gameObject;
        _computerTitle = panelTransform.Find("Title")?.GetComponent<Text>();
        _computerInput = panelTransform.Find("DeviceInput")?.GetComponent<InputField>();
        _computerCloseButton = panelTransform.Find("CloseButton")?.GetComponent<Button>();
        if (_computerCloseButton)
            _computerCloseButton.onClick.AddListener(CloseActiveMenu);
        _computerPanel.SetActive(false);
    }

    private static void ClearChildren(Transform parent)
    {
        for (var i = parent.childCount - 1; i >= 0; i--)
            Destroy(parent.GetChild(i).gameObject);
    }

    private static string PhysicalConnectionText(TssTrainingSession session)
    {
        if (session.PhysicalConnections.Count == 0)
            return string.Empty;

        var lines = new string[session.PhysicalConnections.Count];
        for (var i = 0; i < session.PhysicalConnections.Count; i++)
        {
            var connection = session.PhysicalConnections[i];
            lines[i] =
                $"{(connection.IsPermanent ? "Permanent " : string.Empty)}{TssPhysicalConnectionPanel.FormatConnectionRow(connection, session)}";
        }

        return string.Join("\n", lines);
    }

    private static void EnsureEventSystem()
    {
        if (FindFirstObjectByType<UnityEngine.EventSystems.EventSystem>())
            return;

        var eventSystem = new GameObject(
            "EventSystem",
            typeof(UnityEngine.EventSystems.EventSystem)
        );
#if ENABLE_INPUT_SYSTEM
        eventSystem.AddComponent<UnityEngine.InputSystem.UI.InputSystemUIInputModule>();
#else
        eventSystem.AddComponent<UnityEngine.EventSystems.StandaloneInputModule>();
#endif
    }
}
