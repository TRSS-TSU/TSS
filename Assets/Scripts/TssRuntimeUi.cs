using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using StarterAssets;
using TMPro;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;
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
    private Image _heldCableImage;
    private Text _rackSlotHoverText;
    private TMP_Text _playerNameText;
    private TMP_Text _playerNumberText;
    private Text _objectiveText;
    private Text _contextText;
    private Text _progressText;
    private GameObject _cancelHeldButton;
    private Button _sopButton;
    private Button _connectionsButton;
    private Button _debugButton;
    private Button _endScenarioButton;
    private GameObject _endScenarioPanel;
    private readonly List<Button> _hudInfoButtons = new();
    private readonly List<GameObject> _hudSlidePanels = new();
    private readonly List<Text> _hudSlideTitleTexts = new();
    private readonly List<Text> _hudSlideBodyTexts = new();
    private int _activeHudSlidePanel = -1;
    private GameObject _sopSlidePanel;
    private GameObject _wp1SlidePanel;
    private Text _wp1SlideTitleText;
    private Text _wp1SlideBodyText;
    private readonly Text[] _workPackageTitleTexts = new Text[4];
    private readonly Text[] _workPackageBodyTexts = new Text[4];
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
    private bool _namePromptClosing;

    private GameObject _correctionPanel;
    private Text _correctionTitle;
    private InputField _correctionInput;
    private Button _corrRenameButton;
    private Button _corrPickupButton;
    private Button _corrAccessComputerButton;
    private Button _corrCloseButton;
    private bool _correctionClosing;

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
    private static readonly string[] HudInfoNames = { "Cables", "Permanent", "Effective" };
    private static readonly string[] BinderTabTitles = { "Naming & Racks", "VLANs", "IP Plan", "Cable Schedule", "Circuit Letter", "Wireless", "Credentials" };
    private static readonly Vector2 HudSlidePanelHiddenPosition = new(-1150f, 0f);
    private static readonly Vector2 HudSlidePanelShownPosition = new(20f, 0f);
    private static readonly Vector2 HudRightSlidePanelShownPosition = new(-20f, -101f);
    private const float HudSlideAnimationSpeed = 8f;
    private const float ModalScreenWidth = 3840f;
    private const float ModalSlideDuration = 0.4f;

    public bool IsGameplayInputBlocked => IsAnyGameplayMenuOpen();

    public void SetFirstPersonControlsHudVisible(bool visible)
    {
        if (!_hudRoot)
            return;

        for (var i = 0; i < HudInfoNames.Length; i++)
        {
            _hudRoot.Find($"Hud{HudInfoNames[i]}SlidePanel")?.gameObject.SetActive(visible);
            _hudRoot.Find($"HudInfo{HudInfoNames[i]}Button")?.gameObject.SetActive(visible);
        }

        _hudRoot.Find("ContextTextBackdrop")?.gameObject.SetActive(visible);
        _contextText?.gameObject.SetActive(visible);
    }

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
        if (WasEscapePressed())
        {
            CloseOpenUi();
            return;
        }

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
        CloseActiveMenu(null, false);
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
        if (_endScenarioPanel)
            _endScenarioPanel.SetActive(false);

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
        CloseActiveMenu(owner, true);
    }

    private void CloseActiveMenu(object owner, bool animateModalPanels)
    {
        if (owner != null && _activeMenuOwner != owner)
            return;

        if (_casePanel)
            _casePanel.SetActive(false);
        if (animateModalPanels)
        {
            BeginHideNamePrompt();
            BeginHideCorrectionPanel();
        }
        else
        {
            HideModalPanelImmediately(_namePromptPanel, ref _namePromptClosing);
            HideModalPanelImmediately(_correctionPanel, ref _correctionClosing);
        }
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

            _rackSlotHoverText = _hudRoot.Find("RackSlotHoverText")?.GetComponent<Text>();
            if (_rackSlotHoverText && !_rackSlotHoverText.font)
                _rackSlotHoverText.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");

            var playerTexts = _hudRoot.GetComponentsInChildren<TMP_Text>(true);
            _playerNameText = playerTexts.FirstOrDefault(text => text && text.gameObject.name == "PlayerNameText");
            _playerNumberText = playerTexts.FirstOrDefault(text => text && text.gameObject.name == "PlayerNumberText");
        }
        var heldTextTransform = rootInstance.transform.Find("HUD/HeldText");
        if (heldTextTransform)
        {
            _heldText = heldTextTransform.GetComponent<Text>();
        }

        if (_hudRoot)
        {
            var cableImageObject = new GameObject("HeldCableSprite", typeof(RectTransform), typeof(Image));
            cableImageObject.transform.SetParent(_hudRoot, false);
            var cableImageRect = (RectTransform)cableImageObject.transform;
            cableImageRect.anchorMin = new Vector2(0f, 1f);
            cableImageRect.anchorMax = new Vector2(0f, 1f);
            cableImageRect.pivot = new Vector2(0f, 1f);
            cableImageRect.localPosition = new Vector3(-1859.69995f, 958.599976f, 0f);
            cableImageRect.sizeDelta = new Vector2(44f, 44f);
            cableImageRect.localScale = Vector3.one * 6.66f;
            _heldCableImage = cableImageObject.GetComponent<Image>();
            _heldCableImage.preserveAspect = true;
            _heldCableImage.raycastTarget = false;
            cableImageObject.SetActive(false);
        }

        var cancelBtnTransform = rootInstance.transform.Find("HUD/CancelHeldButton");
        if (cancelBtnTransform)
        {
            _cancelHeldButton = cancelBtnTransform.gameObject;
            var btn = cancelBtnTransform.GetComponent<Button>();
            if (btn)
                btn.onClick.AddListener(CancelHeld);
        }

        var sopBtnTransform = rootInstance.transform.Find("HUD/BinderButton");
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

        var endScenarioButtonTransform = rootInstance.transform.Find("HUD/EndScenarioButton");
        if (endScenarioButtonTransform)
        {
            _endScenarioButton = endScenarioButtonTransform.GetComponent<Button>();
            if (_endScenarioButton)
                _endScenarioButton.onClick.AddListener(ShowEndScenarioConfirmation);
        }

        var endScenarioPanelTransform = rootInstance.transform.Find("EndScenarioPanel");
        if (endScenarioPanelTransform)
        {
            _endScenarioPanel = endScenarioPanelTransform.gameObject;
            _endScenarioPanel.SetActive(false);
            var confirm = endScenarioPanelTransform.Find("ConfirmButton")?.GetComponent<Button>();
            if (confirm)
                confirm.onClick.AddListener(ConfirmEndScenario);
            var cancel = endScenarioPanelTransform.Find("CancelButton")?.GetComponent<Button>();
            if (cancel)
                cancel.onClick.AddListener(HideEndScenarioConfirmation);
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

        // Wire BinderPanel
        var sopPanelTransform = rootInstance.transform.Find("BinderPanel");
        if (sopPanelTransform)
        {
            _sopPanel = sopPanelTransform.gameObject;
            _sopPanel.SetActive(true);
            _sopTabsContainer = sopPanelTransform.Find("BinderTabs");
            _sopContentContainer = sopPanelTransform.Find("BinderContent");
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

        if (_heldCableImage)
        {
            var sprite = session.HeldCable ? session.HeldCable.hudSprite : null;
            _heldCableImage.sprite = sprite;
            _heldCableImage.enabled = sprite != null;
            _heldCableImage.gameObject.SetActive(sprite != null);
        }

        RefreshPlayerIdentity();

        if (_cancelHeldButton)
            _cancelHeldButton.SetActive(session.HeldItem != null || session.HeldCable != null);

        RefreshHudAdditions();
        RefreshCasePanel();
        RefreshSopPanel();
        RefreshDebugPanel();
    }

    private void RefreshPlayerIdentity()
    {
        if (!session)
            return;

        var studentId = session.StudentId ?? string.Empty;
        var separator = studentId.LastIndexOf('-');
        var playerName = separator > 0 ? studentId.Substring(0, separator) : studentId;
        var playerNumber = separator >= 0 && separator < studentId.Length - 1 ? studentId.Substring(separator + 1) : string.Empty;

        if (_playerNameText)
            _playerNameText.text = playerName;
        if (_playerNumberText)
            _playerNumberText.text = playerNumber;
    }

    public void ShowHudMessage(string message)
    {
        if (string.IsNullOrWhiteSpace(message))
            return;

        _statusMessage = message.Trim();
        _statusUntil = Time.time + 3f;
        RefreshHudAdditions();
    }

    public void ShowRackSlotHover(int u, bool rear = false)
    {
        if (!_rackSlotHoverText && _hudRoot)
            _rackSlotHoverText = _hudRoot.Find("RackSlotHoverText")?.GetComponent<Text>();

        if (_rackSlotHoverText && !_rackSlotHoverText.font)
            _rackSlotHoverText.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");

        if (!_rackSlotHoverText)
            return;

        _rackSlotHoverText.text = rear ? $"REAR U{u:00}" : $"FRONT U{u:00}";
        _rackSlotHoverText.gameObject.SetActive(true);
    }

    public void HideRackSlotHover()
    {
        if (_rackSlotHoverText)
            _rackSlotHoverText.gameObject.SetActive(false);
    }

    public void ShowNamePrompt(
        string title,
        string initialValue,
        Action<string> onConfirm,
        object owner = null
    )
    {
        CloseActiveMenu(null, false);
        _activeMenuOwner = owner;
        _namePromptConfirmed = onConfirm;
        if (_namePromptTitle)
            _namePromptTitle.text = title;
        if (_namePromptInput)
        {
            _namePromptInput.text = initialValue ?? string.Empty;
        }
        if (_namePromptPanel)
        {
            _namePromptClosing = false;
            _namePromptPanel.SetActive(true);
            SetModalPanelStartPosition(_namePromptPanel);
        }
        if (_correctionPanel)
            _correctionPanel.SetActive(false);
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
        CloseActiveMenu(null, false);
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
        {
            _correctionClosing = false;
            _correctionPanel.SetActive(true);
            SetModalPanelStartPosition(_correctionPanel);
        }
        if (_namePromptPanel)
            _namePromptPanel.SetActive(false);
        if (_correctionInput && canRename)
            _correctionInput.ActivateInputField();
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
            var btn = rowInstance.transform.Find("TakeButton")?.GetComponent<Button>();
            if (btn)
            {
                btn.onClick.RemoveAllListeners();
                var hasStock = remaining > 0;
                btn.interactable = hasStock;

                var btnText = btn.GetComponentInChildren<Text>();
                if (btnText)
                {
                    btnText.text = $"{equipment.displayName}  x{remaining}";
                    btnText.color = hasStock ? Color.white : new Color(0.55f, 0.58f, 0.60f, 0.75f);
                }

                if (hasStock)
                    btn.onClick.AddListener(() => _openCase?.Checkout(equipment));
            }

            y -= 96f;
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
            var btn = rowInstance.transform.Find("TakeButton")?.GetComponent<Button>();
            if (btn)
            {
                btn.onClick.RemoveAllListeners();
                var hasStock = remainingCables > 0;
                btn.interactable = hasStock;

                var btnText = btn.GetComponentInChildren<Text>();
                if (btnText)
                {
                    btnText.text = $"{cable.displayName}  x{remainingCables}";
                    btnText.color = hasStock ? Color.white : new Color(0.55f, 0.58f, 0.60f, 0.75f);
                }

                if (hasStock)
                    btn.onClick.AddListener(() => _openCase?.CheckoutCable(cable));
            }

            y -= 96f;
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

        var wp1Button = _hudRoot.Find("WP1Button")?.GetComponent<Button>();
        if (wp1Button)
        {
            wp1Button.onClick.RemoveAllListeners();
            wp1Button.onClick.AddListener(() => ToggleRightSlidePanel(1));
        }

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
            EnsureSlidePanelOverlay(panel);
            var title = panelTransform.Find("Title")?.GetComponent<Text>();
            var body = panelTransform.Find("Body")?.GetComponent<Text>();
            _hudSlidePanels.Add(panel);
            _hudSlideTitleTexts.Add(title);
            _hudSlideBodyTexts.Add(body);
        }
    }

    private static void EnsureSlidePanelOverlay(GameObject panel)
    {
        if (!panel)
            return;

        var canvas = panel.GetComponent<Canvas>() ?? panel.AddComponent<Canvas>();
        canvas.overrideSorting = true;
        canvas.sortingOrder = 600;
        var raycaster = panel.GetComponent<GraphicRaycaster>() ?? panel.AddComponent<GraphicRaycaster>();
        raycaster.enabled = true;
    }

    private void BuildRightSlidePanels()
    {
        _sopSlidePanel = _sopPanel;
        EnsureSlidePanelOverlay(_sopSlidePanel);

        var wp1 = _hudRoot.Find("HudWp1SlidePanel");
        _wp1SlidePanel = wp1 ? wp1.gameObject : null;
        EnsureSlidePanelOverlay(_wp1SlidePanel);
        _wp1SlideTitleText = wp1 ? wp1.Find("Title")?.GetComponent<Text>() : null;
        _wp1SlideBodyText = wp1 ? wp1.Find("Body")?.GetComponent<Text>() : null;

        for (var i = 0; i < _workPackageBodyTexts.Length; i++)
        {
            var section = wp1?.Find($"WorkPackage{i + 1}");
            _workPackageTitleTexts[i] = section?.Find("Title")?.GetComponent<Text>();
            _workPackageBodyTexts[i] = section?.Find("Body")?.GetComponent<Text>();
        }
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
        for (var i = 0; i < _workPackageBodyTexts.Length; i++)
        {
            if (_workPackageTitleTexts[i])
                _workPackageTitleTexts[i].text = $"WP{i + 1}";
            if (_workPackageBodyTexts[i])
                _workPackageBodyTexts[i].text = WorkPackageChecklistText(i + 1);
        }

        if (_wp1SlideTitleText)
            _wp1SlideTitleText.text = "WORK PACKAGES";
        if (_wp1SlideBodyText)
            _wp1SlideBodyText.text = WorkPackageChecklistText(1);
    }

    private string HudInfoButtonText(int index)
    {
        PlacementCounts(out var completePlacements, out var totalPlacements);
        ConnectionCounts(out var playerCables, out var permanent);
        var effectivePaths = session.GetEffectivePhysicalPaths().Count;

        return index switch
        {
            0 => $"Cables {playerCables}",
            1 => $"Permanent {permanent}",
            2 => $"Effective {effectivePaths}",
            _ => string.Empty
        };
    }

    private string HudInfoPanelText(int index)
    {
        return index switch
        {
            0 => CablePanelText(false),
            1 => PermanentPanelText(),
            2 => EffectivePathPanelText(),
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

    private string WorkPackageChecklistText(int workPackage)
    {
        if (!session)
            return "No training session.";

        if (workPackage == 1)
            return PhysicalWorkPackageChecklistText();

        if (session.NetworkState == null)
        {
            if (workPackage == 4)
                return StaticWorkPackageChecklist(
                    "Place the access point at its scenario location",
                    "Land the AP uplink through the patch panel",
                    "Assign the AP switchport to the required VLAN",
                    "Configure and verify the guest wireless service");

            return workPackage == 2
                ? StaticWorkPackageChecklist(
                    "Harden the scenario's access switches",
                    "Build the scenario VLAN databases",
                    "Configure the required access ports and trunks",
                    "Save both switch configurations")
                : StaticWorkPackageChecklist(
                    "Harden the scenario router",
                    "Configure inter-VLAN gateways and the provider WAN",
                    "Verify endpoint connectivity to the provider",
                    "Save the router configuration");
        }

        var evaluator = new TssNetworkWorkPackageEvaluator(session.NetworkState);
        var result = workPackage == 2
            ? evaluator.EvaluateLayer2(false)
            : workPackage == 3
                ? evaluator.EvaluateLayer3()
                : evaluator.EvaluateLayer4();

        if (workPackage == 2)
            return Layer2ChecklistText(result);

        var builder = new StringBuilder();
        var complete = 0;
        var total = workPackage == 3 ? 6 : 7;
        builder.AppendLine(TssWorkPackageTitles.Get(workPackage));
        builder.AppendLine($"Complete: {complete}/{total}");
        builder.AppendLine();

        if (workPackage == 3)
        {
            AppendChecklistLine(builder, result, "Harden CN-1", "WP3.Hardening", ref complete);
            AppendChecklistLine(builder, result, "Configure the scenario's inter-VLAN gateways", "WP3.Subinterfaces", ref complete);
            AppendChecklistLine(builder, result, "Configure the provider WAN interface", "WP3.Wan", ref complete);
            AppendChecklistLine(builder, result, "Point the default route at the provider", "WP3.DefaultRoute", ref complete);
            AppendChecklistLine(builder, result, "Verify required endpoint connectivity", "WP3.EndpointEvidence.", ref complete, prefix: true);
            AppendChecklistLine(builder, result, "Save the CN-1 configuration", "WP3.Save", ref complete);
        }
        else
        {
            AppendChecklistLine(builder, result, "Connect the AP uplink", "WP4.APUplink", ref complete);
            AppendChecklistLine(builder, result, "Place the AP switchport in the Reception VLAN", "WP4.SwitchPort", ref complete);
            AppendChecklistLine(builder, result, "Configure the scenario's guest wireless settings", "WP4.APConfig", ref complete);
            AppendChecklistLine(builder, result, "Associate TestLaptop over Wi-Fi", "WP4.LaptopAssociation", ref complete);
            AppendChecklistLine(builder, result, "Obtain the TestLaptop DHCP lease", "WP4.Dhcp", ref complete);
            AppendChecklistLine(builder, result, "Run the required TestLaptop diagnostics", "WP4.Diagnostics", ref complete);
            AppendChecklistLine(builder, result, "Load the RedWolf page", "WP4.RedWolf", ref complete);
        }

        var text = builder.ToString();
        var titleEnd = text.IndexOf('\n');
        var checklist = titleEnd < 0 ? string.Empty : text.Substring(titleEnd + 1);
        var firstLineEnd = checklist.IndexOf('\n');
        return firstLineEnd < 0 ? text : $"{TssWorkPackageTitles.Get(workPackage)}\nComplete: {complete}/{total}{checklist.Substring(firstLineEnd)}";
    }

    private static string Layer2ChecklistText(TssWorkPackageResult result)
    {
        var builder = new StringBuilder();
        var conditions = result?.Conditions ?? new List<TssWorkPackageCondition>();
        var complete = conditions.Count(x => x.IsComplete);
        builder.AppendLine(TssWorkPackageTitles.Get(2));
        builder.AppendLine($"Complete: {complete}/{conditions.Count}");
        builder.AppendLine();
        foreach (var condition in conditions)
        {
            builder.Append(condition.IsComplete ? "[x] " : "[ ] ");
            builder.AppendLine(condition.Label);
            builder.AppendLine();
        }
        return builder.ToString();
    }

    private static string StaticWorkPackageChecklist(params string[] items)
    {
        var builder = new StringBuilder();
        builder.AppendLine("Complete: 0/" + items.Length);
        builder.AppendLine();
        foreach (var item in items)
            AppendUncheckedLine(builder, item);
        return builder.ToString();
    }

    private string PhysicalWorkPackageChecklistText()
    {
        var result = new TssPhysicalWorkPackageEvaluator(session).Evaluate();
        var builder = new StringBuilder();
        var complete = 0;
        var total = 0;
        builder.AppendLine(TssWorkPackageTitles.Get(1));
        builder.AppendLine($"Complete: {complete}/{total}");
        builder.AppendLine();
        if (result.Conditions.Any(x => x.Id.StartsWith("WP1.Placement.")))
        {
            total++;
            AppendChecklistLine(builder, result, "Place all required rack and endpoint equipment", "WP1.Placement.", ref complete, prefix: true);
        }
        if (result.Conditions.Any(x => x.Id.StartsWith("WP1.CableTypes.")))
        {
            total++;
            AppendChecklistLine(builder, result, "Install all required player cable runs with the specified types", "WP1.CableTypes.", ref complete, prefix: true);
        }
        if (result.Conditions.Any(x => x.Id.StartsWith("WP1.PermanentConnections.")))
        {
            total++;
            AppendChecklistLine(builder, result, "Complete all permanent infrastructure connections", "WP1.PermanentConnections.", ref complete, prefix: true);
        }
        if (result.Conditions.Any(x => x.Id == "WP1.Terminations"))
        {
            total++;
            AppendChecklistLine(builder, result, "Terminate every required permanent run", "WP1.Terminations", ref complete);
        }
        var text = builder.ToString();
        var titleEnd = text.IndexOf('\n');
        var checklist = titleEnd < 0 ? string.Empty : text.Substring(titleEnd + 1);
        var firstLineEnd = checklist.IndexOf('\n');
        return firstLineEnd < 0 ? text : $"{TssWorkPackageTitles.Get(1)}\nComplete: {complete}/{total}{checklist.Substring(firstLineEnd)}";
    }

    private static void AppendChecklistLine(StringBuilder builder, TssWorkPackageResult result, string label, string id, ref int complete, params string[] additionalIds)
    {
        var isComplete = IsChecklistItemComplete(result, id);
        foreach (var additionalId in additionalIds)
            isComplete &= IsChecklistItemComplete(result, additionalId);

        if (isComplete)
            complete++;
        builder.Append(isComplete ? "[x] " : "[ ] ");
        builder.AppendLine(label);
        builder.AppendLine();
    }

    private static void AppendChecklistLine(StringBuilder builder, TssWorkPackageResult result, string label, string id, ref int complete, bool prefix)
    {
        var isComplete = IsChecklistItemComplete(result, id, prefix);
        if (isComplete)
            complete++;
        builder.Append(isComplete ? "[x] " : "[ ] ");
        builder.AppendLine(label);
        builder.AppendLine();
    }

    private static void AppendUncheckedLine(StringBuilder builder, string label)
    {
        builder.Append("[ ] ");
        builder.AppendLine(label);
        builder.AppendLine();
    }

    private static bool IsChecklistItemComplete(TssWorkPackageResult result, string id, bool prefix = false)
    {
        if (result == null)
            return false;

        return result.IsComplete(id, prefix);
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

        var profile = session.NetworkProfile;
        var hasBinder = profile != null;
        var sections = Array.Empty<SopSection>();
        var sectionCount = hasBinder ? BinderTabTitles.Length : 0;
        var panelTitle = _sopPanel.transform.Find("PanelTitle")?.GetComponent<Text>();
        if (panelTitle)
            panelTitle.text = hasBinder ? "Training Binder" : "SOP Guide";
        if (sectionCount == 0)
        {
            SetSopChildrenActive(_sopContentContainer, 0);
            if (_sopTabsContainer)
                SetSopChildrenActive(_sopTabsContainer, 0);
            return;
        }

        if (_selectedSopSectionIndex >= sectionCount)
            _selectedSopSectionIndex = 0;

        EnsureSopTabCapacity(sectionCount);
        RefreshSopTabs(sections, sectionCount, hasBinder);
        if (_sopContentContainer.childCount == 0)
        {
            Debug.LogWarning("TssRuntimeUi SOP panel requires one prefab content row.");
            return;
        }

        while (_sopContentContainer.childCount < sectionCount)
        {
            var row = Instantiate(_sopContentContainer.GetChild(0).gameObject, _sopContentContainer);
            row.name = "SectionRow" + _sopContentContainer.childCount.ToString("00");
        }

        for (var i = 0; i < _sopContentContainer.childCount; i++)
        {
            var row = _sopContentContainer.GetChild(i);
            var hasSection = i < sectionCount && (hasBinder || sections[i] != null);
            row.gameObject.SetActive(hasSection && i == _selectedSopSectionIndex);
            if (!hasSection)
                continue;

            var titleText = row.Find("SectionTitle")?.GetComponent<Text>();
            if (titleText)
                titleText.text = hasBinder ? BinderTabTitles[i] : sections[i].title;
            if (hasBinder)
                ApplyBinderReferenceText(row, i, session.Scenario.binderReference);

            var namingTab = row.GetComponent<TssNamingTabPresenter>();
            if (hasBinder && i == 0 && namingTab)
            {
                namingTab.SetValues(session.Scenario);
                continue;
            }
            var vlanTab = row.GetComponent<TssVlanTabPresenter>();
            if (hasBinder && i == 1 && vlanTab)
            {
                vlanTab.SetValues(
                    session.Scenario.scenarioId,
                    profile.ManagementVlan,
                    profile.ReceptionVlan,
                    profile.DataVlan,
                    profile.NativeVlan,
                    profile.DeadVlan,
                    session.Scenario.binderReference?.vlans,
                    profile
                );
                continue;
            }
            var ipPlanTab = row.GetComponent<TssIpPlanTabPresenter>();
            if (hasBinder && i == 2 && ipPlanTab)
            {
                ipPlanTab.SetValues(session.Scenario.scenarioId, profile, session.Scenario.binderReference?.ipPlan);
                continue;
            }

            var cableScheduleTab = row.GetComponent<TssCableScheduleTabPresenter>();
            if (hasBinder && i == 3 && cableScheduleTab)
            {
                cableScheduleTab.SetValues(session.Scenario.scenarioId, profile, session.Scenario.cableSchedule, session.Scenario.binderReference?.cableSchedule);
                continue;
            }

            var referenceTab = row.GetComponent<TssBinderReferenceTabPresenter>();
            if (hasBinder && i >= 4 && i <= 6 && referenceTab)
            {
                referenceTab.SetValues(session.Scenario, profile);
                continue;
            }


            var bodyText = row.Find("SectionBody")?.GetComponent<Text>();
            if (bodyText)
            {
                bodyText.fontSize = hasBinder && i == 0 ? 31 : 40;
                bodyText.text = hasBinder ? BinderBody(i) : SopBody(sections[i]);
            }
        }
    }

    private static void ApplyBinderReferenceText(Transform row, int index, TssBinderReferenceContent content)
    {
        if (!row || content == null)
            return;

        var configuredSectionTitle = index switch
        {
            0 => content.namingAndRacks?.sectionTitle,
            1 => content.vlans?.sectionTitle,
            2 => content.ipPlan?.sectionTitle,
            3 => content.cableSchedule?.sectionTitle,
            4 => content.circuitLetter?.sectionTitle,
            5 => content.wireless?.sectionTitle,
            6 => content.credentials?.sectionTitle,
            _ => string.Empty
        };
        if (!string.IsNullOrWhiteSpace(configuredSectionTitle))
        {
            var sectionTitle = row.Find("SectionTitle")?.GetComponent<Text>();
            if (sectionTitle)
                sectionTitle.text = configuredSectionTitle;
        }

        if (index == 2 && content.ipPlan != null && !string.IsNullOrWhiteSpace(content.ipPlan.hostAddressesHeading))
        {
            var hostHeading = row.Find("HostAddressesHeading")?.GetComponent<Text>();
            if (hostHeading)
                hostHeading.text = content.ipPlan.hostAddressesHeading;
        }

        var intro = index switch
        {
            0 => content.namingAndRacks?.intro,
            1 => content.vlans?.intro,
            2 => content.ipPlan?.paragraph,
            3 => content.cableSchedule?.introParagraph,
            _ => string.Empty
        };
        if (string.IsNullOrWhiteSpace(intro))
            return;

        var targetName = index == 2 ? "Paragraph" : "IntroParagraph";
        var text = row.Find(targetName)?.GetComponent<Text>();
        if (text)
            text.text = intro;
    }

    private void EnsureSopTabCapacity(int count)
    {
        if (!_sopTabsContainer || _sopTabsContainer.childCount == 0)
            return;

        while (_sopTabsContainer.childCount < count)
        {
            var tab = Instantiate(_sopTabsContainer.GetChild(0).gameObject, _sopTabsContainer);
            tab.name = "Tab" + _sopTabsContainer.childCount.ToString("00");
        }
    }

    private string BinderBody(int index)
    {
        var profile = session.NetworkProfile;
        var scenario = session.Scenario;
        switch (index)
        {
            case 0:
                return BinderHeading("Naming standard")
                    + "AN = Access Node (switch). CN = Core Node (router). Rack order is not cosmetic: power at the top, away from data; patch panel directly above the switch it feeds; router beside the switch it uplinks to; and the heavy box lowest for center of gravity.\n\n"
                    + "<color=#E69529><b>FIRST FLOOR RACK / RACK01</b></color>\n"
                    + "<color=#9DB7C9><b>SLOT       DEVICE                 NAME</b></color>\n"
                    + "U1 (top)  Rack PDU                PDU1\nPower at the top, away from data cabling\n\n"
                    + "U2        8-port patch panel     Patch1\nDirectly above its switch - short patch runs\n\n"
                    + "U3        8-port switch           AN-1\nAccess node, ground floor\n\n"
                    + "U4        Core router              CN-1\nBeside the switch it trunks to; feeds the demarc\n\n"
                    + "U5        Rack server              IT2\nHeavy and hot - lowest in the rack\n\n"
                    + "U6        -                        spare\nLeave open for growth\n\n"
                    + "<color=#E69529><b>SECOND FLOOR RACK / RACK02</b></color>\n"
                    + "<color=#9DB7C9><b>SLOT       DEVICE                 NAME</b></color>\n"
                    + "U1        Rack PDU                PDU2\n"
                    + "U2        Patch panel             Patch2\n"
                    + "U3        Switch                  AN-2\n\n"
                    + "<color=#9DB7C9><i>No server upstairs. Wall mounts are already installed - facilities did that.</i></color>";
            case 1:
                return BinderHeading("Layer 2 plan")
                    + $"Create on both switches:\n{profile.ManagementVlanName}: VLAN {profile.ManagementVlan}\n{profile.ReceptionVlanName}: VLAN {profile.ReceptionVlan}\n{profile.DataVlanName}: VLAN {profile.DataVlan}\n{profile.NativeVlanName}: VLAN {profile.NativeVlan} (native)\n{profile.DeadVlanName}: VLAN {profile.DeadVlan} (unused ports)\n\n"
                    + "ACCESS PORTS\nAN-1 Fa0/2 IT1; Fa0/3 Reception1; Fa0/4 Reception2; Fa0/5 IT2\n"
                    + "AN-2 Fa0/2 Data1; Fa0/3 Data2; Fa0/4 Data3\n\n"
                    + "802.1Q TRUNKS\nAN-1 Fa0/1 to AN-2 Fa0/1; AN-1 Fa0/6 to CN-1 Fa0/0\n"
                    + "Set dot1q, trunk mode, native VLAN, and allow Management, Reception, and Data.";
            case 2:
                return BinderHeading("Addressing and gateways")
                    + $"MANAGEMENT / VLAN {profile.ManagementVlan}\nNetwork {profile.ManagementNetwork}.0/24 | gateway {profile.ManagementNetwork}.1\n"
                    + $"RECEPTION / VLAN {profile.ReceptionVlan}\nNetwork {profile.ReceptionNetwork}.0/24 | gateway {profile.ReceptionNetwork}.1\n"
                    + $"DATA / VLAN {profile.DataVlan}\nNetwork {profile.DataNetwork}.0/24 | gateway {profile.DataNetwork}.1\n\n"
                    + "ENDPOINT ADDRESSES\n"
                    + string.Join("  ", new[] { "IT1", "IT2" }.Select(key => key + " " + profile.EndpointAddresses[key])) + "\n"
                    + string.Join("  ", new[] { "Reception1", "Reception2" }.Select(key => key + " " + profile.EndpointAddresses[key])) + "\n"
                    + string.Join("  ", new[] { "Data1", "Data2", "Data3" }.Select(key => key + " " + profile.EndpointAddresses[key])) + "\n"
                    + $"AP-1 {profile.ApManagementAddress} | TestLaptop {profile.TestLaptopAddress} (DHCP)\n\n"
                    + "CN-1: Fa0/0.<VLAN> subinterfaces use dot1q and the matching gateway above.";
            case 3:
                return BinderHeading("Cable schedule")
                    + "INTER-CLOSET LINK\nRack01 Patch1 Port48 to Rack02 Patch2 Port48: permanent punch-down run; complete both terminations.\n"
                    + "Patch cords: AN-1 Fa0/1 to Patch1 Port48: crossover; AN-2 Fa0/1 to Patch2 Port48: straight-through.\n"
                    + "CN-1 Fa0/0 to AN-1 Fa0/6: straight-through.\n\n"
                    + "DEMARC\nCN-1 Fa0/1 to Patch1 Port18: straight-through.\n\n"
                    + "GROUND FLOOR\nIT1 to AN-1 Fa0/2; IT2 to AN-1 Fa0/5\nReception1/2 to wall ports 1/2, then Patch1 ports 1/2 to AN-1 Fa0/3/4.\n\n"
                    + "SECOND FLOOR\nData1/2/3 to Office wall ports, then Patch2 ports 1/5/6 to AN-2 Fa0/2/3/4.\n\n"
                    + "CIRCUIT AND WIRELESS\nCN-1 Fa0/1 to Patch1 Port18; AP-1 to any Reception Wall Port 01-04, then the matching Patch1 Port 01-04 to AN-1 Fa0/7.\n"
                    + "Complete each punch-down and patch cord in the structured path.";
            case 4:
                return BinderHeading("Carrier circuit letter")
                    + $"CIRCUIT ID\n{profile.CircuitId}\n\n"
                    + $"CUSTOMER / CN-1 Fa0/1\nAddress {profile.WanCustomerAddress} /30\nProvider next hop {profile.WanProviderAddress}\n\n"
                    + "PHYSICAL HANDOFF\nPatch CN-1 Fa0/1 to Rack01 Patch1 Port18. The carrier side is landed at the DEMARC.\n\n"
                    + $"ROUTING\nConfigure a default route to {profile.WanProviderAddress}. Verify with show ip route and an endpoint ping.";
            case 5:
                return BinderHeading("Guest wireless")
                    + $"AP-1 MANAGEMENT\nAddress {profile.ApManagementAddress} on the Reception network.\n\n"
                    + $"GUEST WI-FI\nSSID {profile.Ssid}\nPassphrase {profile.WifiPassphrase}\nChannel {profile.WirelessChannel}; disable WPS and apply the secured settings.\n\n"
                    + "UPLINK\nAP-1 to Reception Wall Port 01-04, then the matching Rack01 Patch1 Port 01-04 to AN-1 Fa0/7 in the Reception VLAN.\n\n"
                    + $"TEST LAPTOP\nJoin the guest SSID, use DHCP, then verify {profile.TestLaptopAddress}, provider reachability, and the RedWolf page.";
            case 6:
                return BinderHeading("Run credentials")
                    + $"NETWORK DEVICES\nEnable secret: {profile.EnableSecret}\nConsole password: {profile.ConsolePassword}\nVTY password: {profile.VtyPassword}\n\n"
                    + $"ACCESS POINT\nAdministrator password: {profile.ApAdminPassword}\n\n"
                    + "These credentials are generated for this run. Keep them in the binder; do not copy them into notes or debug output.";
            default:
                return string.Empty;
        }
    }

    private static string BinderHeading(string title) => $"<color=#E69529><b>{title.ToUpperInvariant()}</b></color>\n\n";

    private string SopBody(SopSection section)
    {
        var profile = session.NetworkProfile;
        if (profile == null || section == null)
            return section?.body ?? string.Empty;

        if (section.title == "Phases 9 - 15")
            return $"Configure and verify AN-1 and AN-2 using the terminal.\nVLANs: {profile.ManagementVlanName} {profile.ManagementVlan}, {profile.ReceptionVlanName} {profile.ReceptionVlan}, {profile.DataVlanName} {profile.DataVlan}, {profile.NativeVlanName} {profile.NativeVlan}, and {profile.DeadVlanName} {profile.DeadVlan}.\nManagement network: {profile.ManagementNetwork}.0/24\nReception network: {profile.ReceptionNetwork}.0/24\nData network: {profile.DataNetwork}.0/24\nRequired endpoint values: {string.Join(", ", profile.EndpointAddresses.Select(x => x.Key + " " + x.Value))}\nComplete the required switch verification and save each switch configuration.";

        if (section.title == "Phases 16 - 19")
            return $"Configure CN-1 router-on-a-stick and the provider handoff.\nGateway networks: {profile.ManagementNetwork}.0/24, {profile.ReceptionNetwork}.0/24, and {profile.DataNetwork}.0/24\nWAN customer address: {profile.WanCustomerAddress} / 255.255.255.252\nWAN provider target: {profile.WanProviderAddress}\nUse endpoint-originated diagnostics for the required verification, then save CN-1.";

        return section.body;
    }

    private void RefreshSopTabs(SopSection[] sections, int sectionCount, bool hasBinder)
    {
        if (!_sopTabsContainer)
            return;

        var tabsRect = _sopTabsContainer as RectTransform;
        var tabContainerWidth = tabsRect ? tabsRect.rect.width : 0f;
        if (tabContainerWidth <= 0f && tabsRect?.parent is RectTransform parentRect)
            tabContainerWidth = parentRect.rect.width + (tabsRect.sizeDelta.x < 0f ? tabsRect.sizeDelta.x : 0f);

        var tabWidth = sectionCount > 0 && tabContainerWidth > 0f
            ? tabContainerWidth / sectionCount
            : 78f;
        for (var i = 0; i < _sopTabsContainer.childCount; i++)
        {
            var tab = _sopTabsContainer.GetChild(i);
            var hasSection = i < sectionCount && (hasBinder || sections[i] != null);
            tab.gameObject.SetActive(hasSection);
            if (!hasSection)
                continue;

            if (tab is RectTransform tabRect)
            {
                tabRect.anchorMin = new Vector2(0f, 1f);
                tabRect.anchorMax = new Vector2(0f, 1f);
                tabRect.pivot = new Vector2(0f, 1f);
                tabRect.anchoredPosition = new Vector2(i * tabWidth, 0f);
                tabRect.sizeDelta = new Vector2(Mathf.Max(1f, tabWidth - 2f), 88f);
            }

            var label = tab.GetComponentInChildren<Text>();
            if (label)
            {
                label.text = hasBinder ? BinderTabTitles[i] : sections[i].title;
                label.resizeTextForBestFit = true;
                label.resizeTextMinSize = 26;
                label.resizeTextMaxSize = 30;
            }

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

        var result = new TssPhysicalWorkPackageEvaluator(session).Evaluate();
        var placementText = result.Complete
            ? "WP1 physical installation complete."
            : string.Join("\n", result.UnmetConditions);
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
            rect.anchoredPosition = SmoothSlidePosition(rect.anchoredPosition, target);
        }

        AnimateRightSlidePanel(_sopSlidePanel, _activeRightSlidePanel == 0);
        AnimateRightSlidePanel(_wp1SlidePanel, _activeRightSlidePanel == 1);
        AnimateModalPanel(_namePromptPanel, ref _namePromptClosing);
        AnimateModalPanel(_correctionPanel, ref _correctionClosing);
    }

    private static void SetModalPanelStartPosition(GameObject panel)
    {
        var rect = panel ? panel.GetComponent<RectTransform>() : null;
        if (rect)
            rect.anchoredPosition = new Vector2(-ModalTravelDistance(rect), 0f);
    }

    private void AnimateModalPanel(GameObject panel, ref bool closing)
    {
        if (!panel || !panel.activeSelf)
            return;

        var rect = panel.GetComponent<RectTransform>();
        if (!rect)
            return;

        var target = closing
            ? new Vector2(ModalTravelDistance(rect), 0f)
            : Vector2.zero;
        var step = ModalTravelDistance(rect) / ModalSlideDuration * Time.unscaledDeltaTime;
        rect.anchoredPosition = Vector2.MoveTowards(rect.anchoredPosition, target, step);

        if (closing && rect.anchoredPosition == target)
        {
            panel.SetActive(false);
            closing = false;
            ApplyGameplayInputLock(IsAnyGameplayMenuOpen());
        }
    }

    private static void AnimateRightSlidePanel(GameObject panel, bool shown)
    {
        var rect = panel ? panel.GetComponent<RectTransform>() : null;
        if (!rect)
            return;

        var target = shown ? HudRightSlidePanelShownPosition : new Vector2(rect.rect.width + 60f, -101f);
        rect.anchoredPosition = SmoothSlidePosition(rect.anchoredPosition, target);
    }

    private static Vector2 SmoothSlidePosition(Vector2 current, Vector2 target)
    {
        var t = 1f - Mathf.Exp(-HudSlideAnimationSpeed * Time.unscaledDeltaTime);
        var next = Vector2.Lerp(current, target, t);
        return (target - next).sqrMagnitude <= 0.25f ? target : next;
    }

    private static float ModalTravelDistance(RectTransform rect)
    {
        return (ModalScreenWidth + rect.rect.width) * 0.5f;
    }

    private void ConfirmNamePrompt()
    {
        var onConfirm = _namePromptConfirmed;
        HideNamePrompt();
        onConfirm?.Invoke(_namePromptInput ? _namePromptInput.text : string.Empty);
    }

    private void HideNamePrompt()
    {
        BeginHideNamePrompt();
        _namePromptConfirmed = null;
        ClearOwner(_activeMenuOwner);
        ApplyGameplayInputLock(IsAnyGameplayMenuOpen());
    }

    private void BeginHideNamePrompt()
    {
        if (_namePromptPanel && _namePromptPanel.activeSelf)
            _namePromptClosing = true;
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
        BeginHideCorrectionPanel();
        _renameConfirmed = null;
        _pickupConfirmed = null;
        _accessComputerConfirmed = null;
        ClearOwner(_activeMenuOwner);
        ApplyGameplayInputLock(IsAnyGameplayMenuOpen());
    }

    private void BeginHideCorrectionPanel()
    {
        if (_correctionPanel && _correctionPanel.activeSelf)
            _correctionClosing = true;
    }

    private static void HideModalPanelImmediately(GameObject panel, ref bool closing)
    {
        if (panel)
        {
            panel.SetActive(false);
            var rect = panel.GetComponent<RectTransform>();
            if (rect)
                rect.anchoredPosition = Vector2.zero;
        }

        closing = false;
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
            || (_endScenarioPanel && _endScenarioPanel.activeSelf)
            || _externalGameplayLocks.Count > 0;
    }

    private void ShowEndScenarioConfirmation()
    {
        CloseActiveMenu(null, false);
        if (_endScenarioPanel)
            _endScenarioPanel.SetActive(true);
        ApplyGameplayInputLock(true);
    }

    private void HideEndScenarioConfirmation()
    {
        if (_endScenarioPanel)
            _endScenarioPanel.SetActive(false);
        ApplyGameplayInputLock(IsAnyGameplayMenuOpen());
    }

    private void ConfirmEndScenario()
    {
        if (!session)
        {
            ShowHudMessage("No training session is loaded.");
            HideEndScenarioConfirmation();
            return;
        }

        TssResultsHandoff.Capture(session);
        SceneManager.LoadScene("Results");
    }

    private void CloseOpenUi()
    {
        if (!IsAnyGameplayMenuOpen() && _activeHudSlidePanel < 0 && _activeRightSlidePanel < 0)
            return;

        CloseActiveMenu();
        _activeHudSlidePanel = -1;
        _activeRightSlidePanel = -1;
        RefreshHudAdditions();
    }

    private static bool WasEscapePressed()
    {
        return Keyboard.current != null && Keyboard.current.escapeKey.wasPressedThisFrame;
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
        var existing = FindFirstObjectByType<UnityEngine.EventSystems.EventSystem>();
        if (existing)
        {
            if (!existing.GetComponent<TssUiTabNavigation>())
                existing.gameObject.AddComponent<TssUiTabNavigation>();
            return;
        }

        var eventSystem = new GameObject(
            "EventSystem",
            typeof(UnityEngine.EventSystems.EventSystem)
        );
        eventSystem.AddComponent<TssUiTabNavigation>();
#if ENABLE_INPUT_SYSTEM
        eventSystem.AddComponent<UnityEngine.InputSystem.UI.InputSystemUIInputModule>();
#else
        eventSystem.AddComponent<UnityEngine.EventSystems.StandaloneInputModule>();
#endif
    }
}
