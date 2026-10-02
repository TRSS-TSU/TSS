using System;
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
    private Text _heldText;
    private GameObject _cancelHeldButton;
    private Button _sopButton;
    private Button _debugButton;

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
    private bool _controlsLocked;
    private CursorLockMode _previousCursorLockMode;
    private bool _previousCursorVisible;
    private static readonly int SpeedHash = Animator.StringToHash("Speed");
    private static readonly int MotionSpeedHash = Animator.StringToHash("MotionSpeed");

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
        ApplyGameplayInputLock(false);
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
        var heldTextTransform = rootInstance.transform.Find("HUD/HeldText");
        if (heldTextTransform)
            _heldText = heldTextTransform.GetComponent<Text>();

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
                _sopButton.onClick.AddListener(ToggleSop);
        }

        var debugBtnTransform = rootInstance.transform.Find("HUD/DebugButton");
        if (debugBtnTransform)
        {
            _debugButton = debugBtnTransform.GetComponent<Button>();
            if (_debugButton)
            {
                if (enableDebugValidationPanel)
                    _debugButton.onClick.AddListener(ToggleDebug);
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

            var retBtn = casePanelTransform.Find("ReturnHeldButton")?.GetComponent<Button>();
            if (retBtn)
                retBtn.onClick.AddListener(() => _openCase?.ReturnHeld());

            var cancelBtn = casePanelTransform.Find("CancelButton")?.GetComponent<Button>();
            if (cancelBtn)
                cancelBtn.onClick.AddListener(() => _openCase?.CancelSelection());

            var closeBtn = casePanelTransform.Find("CloseButton")?.GetComponent<Button>();
            if (closeBtn)
                closeBtn.onClick.AddListener(() => _openCase?.ClosePanel());
        }

        // Wire SopPanel
        var sopPanelTransform = rootInstance.transform.Find("SopPanel");
        if (sopPanelTransform)
        {
            _sopPanel = sopPanelTransform.gameObject;
            _sopTabsContainer = sopPanelTransform.Find("SopTabs");
            _sopContentContainer = sopPanelTransform.Find("SopContent");
        }

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

        RefreshCasePanel();
        RefreshSopPanel();
        RefreshDebugPanel();
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
        Action onPickup
    )
    {
        ShowEndpointMenu(title, true, true, false, onRename, onPickup, null, null, currentName);
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
            _corrAccessComputerButton.gameObject.SetActive(canAccessComputer);
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

    public void ShowComputerMenu(string title, object owner = null)
    {
        CloseActiveMenu();
        _activeMenuOwner = owner;
        if (_computerTitle)
            _computerTitle.text = title;
        if (_computerInput)
            _computerInput.text = string.Empty;
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

            var label = rowInstance.transform.Find("ItemLabel")?.GetComponent<Text>();
            if (label)
                label.text = $"{equipment.displayName}  x{session.GetRemaining(equipment)}";

            var btn = rowInstance.transform.Find("TakeButton")?.GetComponent<Button>();
            if (btn)
            {
                btn.onClick.RemoveAllListeners();
                btn.onClick.AddListener(() => _openCase?.Checkout(equipment));
            }

            y -= 42f;
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

            var label = rowInstance.transform.Find("ItemLabel")?.GetComponent<Text>();
            if (label)
                label.text = $"{cable.displayName} cable  x{session.GetRemaining(cable)}";

            var btn = rowInstance.transform.Find("TakeButton")?.GetComponent<Button>();
            if (btn)
            {
                btn.onClick.RemoveAllListeners();
                btn.onClick.AddListener(() => _openCase?.CheckoutCable(cable));
            }

            y -= 42f;
        }
    }

    private void RefreshSopPanel()
    {
        if (
            !_sopPanel
            || !_sopPanel.activeSelf
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
        if (_sopPanel)
        {
            _sopPanel.SetActive(!_sopPanel.activeSelf);
            Refresh();
        }
    }

    private void ToggleDebug()
    {
        if (_debugPanel)
        {
            _debugPanel.SetActive(!_debugPanel.activeSelf);
            Refresh();
        }
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
            || (_computerPanel && _computerPanel.activeSelf);
    }

    private void CancelHeld()
    {
        if (!session)
            return;

        if (session.HeldItem)
            session.ReturnHeldItem(out _);
        else if (session.HeldCable)
            session.ReturnHeldCable(out _);
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
            playerController.enabled = !locked;

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
                $"{(connection.IsPermanent ? "Permanent " : string.Empty)}{connection.CableType} - {connection.EndpointAId} <-> {connection.EndpointBId}";
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
