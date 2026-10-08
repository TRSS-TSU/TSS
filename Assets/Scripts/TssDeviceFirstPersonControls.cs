using UnityEngine;
using UnityEngine.UI;

public sealed class TssDeviceFirstPersonControls : MonoBehaviour
{
    [SerializeField] private Button returnButton;
    [SerializeField] private Button sessionButton;
    [SerializeField] private Button renameButton;
    [SerializeField] private Button pickupButton;
    [SerializeField] private Text sessionButtonText;
    [SerializeField] private Text deviceTitleText;
    [SerializeField] private bool isTerminalSession;

    private TssObjectInteractable _targetInteractable;
    private TssDeviceSessionController.DeviceContext _cachedContext;

    private void Awake()
    {
        if (returnButton)
            returnButton.onClick.AddListener(OnReturnClicked);

        if (sessionButton)
            sessionButton.onClick.AddListener(OnSessionClicked);

        if (renameButton)
            renameButton.onClick.AddListener(OnRenameClicked);

        if (pickupButton)
            pickupButton.onClick.AddListener(OnPickupClicked);
    }

    private void Start()
    {
        BindToActiveDevice();
    }

    public void Configure(bool terminalSession, string title = null)
    {
        isTerminalSession = terminalSession;
        if (sessionButtonText)
            sessionButtonText.text = terminalSession ? "Terminal Session" : "Desktop Session";

        if (!string.IsNullOrEmpty(title) && deviceTitleText)
            deviceTitleText.text = title;
    }

    public void BindToActiveDevice()
    {
        _targetInteractable = TssObjectInteractable.ActiveInteractable;
        if (!_targetInteractable)
            _targetInteractable = GetComponentInParent<TssObjectInteractable>();

        var devTitle = "Device Inspection";
        if (TssDeviceSessionController.Instance)
        {
            var targetGo = _targetInteractable ? _targetInteractable.gameObject : null;
            _cachedContext = TssDeviceSessionController.Instance.ResolveContext(targetGo);
            if (_cachedContext != null)
            {
                devTitle = $"{_cachedContext.DeviceName} ({_cachedContext.Category})";
                isTerminalSession = _cachedContext.Category == EquipmentCategory.Switch || _cachedContext.Category == EquipmentCategory.Router;
            }
        }

        if (deviceTitleText)
            deviceTitleText.text = devTitle;

        if (sessionButtonText)
            sessionButtonText.text = isTerminalSession ? "Terminal Session" : "Desktop Session";
    }

    private void OnReturnClicked()
    {
        if (!_targetInteractable)
            _targetInteractable = TssObjectInteractable.ActiveInteractable;

        if (_targetInteractable)
            _targetInteractable.ReturnToThirdPerson();
    }

    private void OnSessionClicked()
    {
        if (TssDeviceSessionController.Instance)
        {
            var targetGo = _targetInteractable ? _targetInteractable.gameObject : null;
            TssDeviceSessionController.Instance.OpenSessionForDevice(targetGo, isTerminalSession);
        }
    }

    private void OnRenameClicked()
    {
        if (_cachedContext == null && TssDeviceSessionController.Instance)
        {
            var targetGo = _targetInteractable ? _targetInteractable.gameObject : null;
            _cachedContext = TssDeviceSessionController.Instance.ResolveContext(targetGo);
        }

        if (_cachedContext == null || !TssRuntimeUi.Instance)
            return;

        var ctx = _cachedContext;
        TssRuntimeUi.Instance.ShowNamePrompt($"Rename {ctx.DeviceName}", ctx.DeviceName, newName =>
        {
            if (string.IsNullOrWhiteSpace(newName))
                return;

            newName = newName.Trim();
            if (ctx.Rack != null && TssTrainingSession.Instance)
            {
                if (TssTrainingSession.Instance.RenameRackInstalled(ctx.Rack.RackId, ctx.StartingU, newName))
                {
                    ctx.Rack.SetInstalledDeviceName(ctx.StartingU, newName);
                    ctx.DeviceName = newName;
                    BindToActiveDevice();
                }
            }
            else if (ctx.PlacementStation != null && TssTrainingSession.Instance)
            {
                if (TssTrainingSession.Instance.RenameEndpoint(ctx.PlacementId, newName))
                {
                    ctx.DeviceName = newName;
                    BindToActiveDevice();
                }
            }
        });
    }

    private void OnPickupClicked()
    {
        if (_cachedContext == null && TssDeviceSessionController.Instance)
        {
            var targetGo = _targetInteractable ? _targetInteractable.gameObject : null;
            _cachedContext = TssDeviceSessionController.Instance.ResolveContext(targetGo);
        }

        if (_cachedContext == null)
            return;

        var ctx = _cachedContext;
        if (ctx.Rack != null)
        {
            ctx.Rack.PickupInstalled(ctx.StartingU);
            OnReturnClicked();
        }
        else if (ctx.PlacementStation != null && ctx.Equipment != null)
        {
            ctx.PlacementStation.PickupPlaced(ctx.PlacementId, ctx.Equipment);
            OnReturnClicked();
        }
    }
}
