using StarterAssets;
using UnityEngine;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif

public sealed class EquipmentCaseStation : MonoBehaviour
{
    [SerializeField] private string stationName = "Equipment Case";
    [SerializeField] private TssObjectInteractable interactable;
    [SerializeField] private Color hoverTint = new(0.2f, 0.75f, 1f, 1f);
    [SerializeField, Range(0f, 1f)] private float hoverBlend = 0.35f;

    private bool _playerInRange;
    private Renderer[] _renderers;
    private Material[][] _materials;
    private Color[][] _baseColors;
    private Camera _mainCamera;
    private bool _isHovered;

    public string StationName => stationName;

    private void Awake()
    {
        if (!interactable)
            interactable = GetComponent<TssObjectInteractable>();

        if (interactable)
            interactable.enabled = false;

        _mainCamera = Camera.main;
        CacheRenderers();
    }

    private void Update()
    {
        UpdateHoverAndClick();
    }

    private void OnTriggerEnter(Collider other)
    {
        if (!IsPlayer(other))
            return;

        _playerInRange = true;
    }

    private void OnTriggerExit(Collider other)
    {
        if (!IsPlayer(other))
            return;

        _playerInRange = false;
        SetHighlight(false);
        ClosePanel();
    }

    private void OnDisable()
    {
        SetHighlight(false);
    }

    public void Checkout(EquipmentDefinition equipment)
    {
        var session = TssTrainingSession.Instance;
        if (!session)
            return;

        if (session.HeldItem)
            session.ReturnHeldItem(out _);

        if (session.TryCheckout(equipment, out _))
            ClosePanel();
    }

    public void ReturnHeld()
    {
        TssTrainingSession.Instance?.ReturnHeldItem(out _);
    }

    public void ClosePanel()
    {
        TssRuntimeUi.Instance?.HideCasePanel(this);
    }

    public void CancelSelection()
    {
        ClosePanel();
    }

    private void OpenPanel()
    {
        if (!IsGameplayInputBlocked())
            TssRuntimeUi.Instance?.ShowCaseMenu(this);
    }

    private void UpdateHoverAndClick()
    {
        var hovered = _playerInRange && !IsGameplayInputBlocked() && IsMouseOverCase();
        SetHighlight(hovered);
        _isHovered = hovered;

        if (_isHovered && WasPrimaryClickPressed())
            OpenPanel();
    }

    private bool IsMouseOverCase()
    {
        if (!_mainCamera)
            _mainCamera = Camera.main;

        if (!_mainCamera)
            return false;

        var ray = _mainCamera.ScreenPointToRay(GetMousePosition());
        var hits = Physics.RaycastAll(ray, Mathf.Infinity, ~0, QueryTriggerInteraction.Ignore);
        System.Array.Sort(hits, (left, right) => left.distance.CompareTo(right.distance));

        foreach (var hit in hits)
        {
            if (hit.collider && hit.collider.transform.IsChildOf(transform))
                return true;
        }

        return false;
    }

    private void CacheRenderers()
    {
        _renderers = GetComponentsInChildren<Renderer>(true);
        _materials = new Material[_renderers.Length][];
        _baseColors = new Color[_renderers.Length][];

        for (var rendererIndex = 0; rendererIndex < _renderers.Length; rendererIndex++)
        {
            var materials = _renderers[rendererIndex].materials;
            _materials[rendererIndex] = materials;
            _baseColors[rendererIndex] = new Color[materials.Length];

            for (var materialIndex = 0; materialIndex < materials.Length; materialIndex++)
                _baseColors[rendererIndex][materialIndex] = GetMaterialColor(materials[materialIndex]);
        }
    }

    private void SetHighlight(bool highlighted)
    {
        if (_materials == null)
            return;

        if (IsGameplayInputBlocked())
            highlighted = false;

        for (var rendererIndex = 0; rendererIndex < _materials.Length; rendererIndex++)
        {
            var materials = _materials[rendererIndex];
            for (var materialIndex = 0; materialIndex < materials.Length; materialIndex++)
            {
                var material = materials[materialIndex];
                if (!material)
                    continue;

                var baseColor = _baseColors[rendererIndex][materialIndex];
                SetMaterialColor(material, highlighted ? Color.Lerp(baseColor, hoverTint, hoverBlend) : baseColor);
            }
        }
    }

    private static Color GetMaterialColor(Material material)
    {
        if (!material)
            return Color.white;

        if (material.HasProperty("_BaseColor"))
            return material.GetColor("_BaseColor");

        return material.HasProperty("_Color") ? material.GetColor("_Color") : Color.white;
    }

    private static void SetMaterialColor(Material material, Color color)
    {
        if (material.HasProperty("_BaseColor"))
            material.SetColor("_BaseColor", color);
        else if (material.HasProperty("_Color"))
            material.SetColor("_Color", color);
    }

    private static bool IsPlayer(Collider other)
    {
        return other.CompareTag("Player") || other.GetComponentInParent<ThirdPersonController>();
    }

    private static bool IsGameplayInputBlocked()
    {
        return TssRuntimeUi.Instance && TssRuntimeUi.Instance.IsGameplayInputBlocked;
    }

    private static Vector3 GetMousePosition()
    {
#if ENABLE_INPUT_SYSTEM
        return Mouse.current != null ? (Vector3)Mouse.current.position.ReadValue() : Vector3.zero;
#else
        return Input.mousePosition;
#endif
    }

    private static bool WasPrimaryClickPressed()
    {
#if ENABLE_INPUT_SYSTEM
        return Mouse.current != null && Mouse.current.leftButton.wasPressedThisFrame;
#else
        return Input.GetMouseButtonDown(0);
#endif
    }
}
