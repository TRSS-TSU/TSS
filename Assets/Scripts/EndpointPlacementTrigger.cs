using UnityEngine;
using TMPro;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif

public sealed class EndpointPlacementTrigger : MonoBehaviour
{
    private const string CategoryLabelName = "Placement Category Label";
    [SerializeField] private EndpointPlacementStation station;
    [SerializeField] private EquipmentCategory category;
    [SerializeField] private Transform placementAnchor;
    [SerializeField] private TextMeshPro categoryLabel;
    [SerializeField] private Color baseColor = new(0.12f, 0.18f, 0.24f, 1f);
    [SerializeField] private Vector3 categoryLabelLocalOffset = new(-0.34f, 3.31f, -0.03f);
    [SerializeField] private Vector3 categoryLabelLocalEuler = new(0f, 266.08f, 0f);
    [SerializeField] private Vector3 categoryLabelLocalScale = new(0.64f, 22.5f, 0.9f);
    [SerializeField] private float categoryLabelFontSize = 2f;
    private Renderer _renderer;
    private Material _material;
    private Camera _mainCamera;
    private bool _hovered;

    public EquipmentCategory Category => category;

    private EndpointPlacementStation Station => station ? station : GetComponentInParent<EndpointPlacementStation>();

    private void Awake()
    {
        _renderer = GetComponent<Renderer>();
        _material = _renderer ? (Application.isPlaying ? _renderer.material : _renderer.sharedMaterial) : null;
        _mainCamera = Camera.main;

        ApplyBaseColor();
        EnsureCategoryLabel();
    }

    public void Configure(EndpointPlacementStation placementStation, EquipmentCategory placementCategory, Color color)
    {
        station = placementStation;
        category = placementCategory;
        baseColor = color;
        _renderer = GetComponent<Renderer>();
        _material = _renderer ? (Application.isPlaying ? _renderer.material : _renderer.sharedMaterial) : null;
        ApplyBaseColor();
        EnsureCategoryLabel();
    }

    internal void RefreshAvailability()
    {
        var placementStation = Station;
        var shouldBeActive = placementStation && (!placementAnchor || !placementStation.IsAnchorOccupied(placementAnchor));
        if (gameObject.activeSelf != shouldBeActive)
            gameObject.SetActive(shouldBeActive);
    }

    private void ApplyBaseColor()
    {
        if (!_renderer)
            return;

        if (Application.isPlaying)
        {
            SetMaterialColor(_material, baseColor);
            return;
        }

        var material = _renderer.sharedMaterial ? new Material(_renderer.sharedMaterial) : new Material(Shader.Find("Standard"));
        SetMaterialColor(material, baseColor);
        _renderer.sharedMaterial = material;
    }

    private void Update()
    {
        var placementStation = Station;
        var hovered = placementStation
            && placementStation.IsPlayerInRange
            && placementStation.CanPlaceCategory(category)
            && !IsGameplayInputBlocked()
            && IsMouseOverTrigger();

        if (_hovered != hovered)
        {
            _hovered = hovered;
            SetColor(_hovered ? Color.Lerp(baseColor, new Color(0.2f, 0.75f, 1f), 0.65f) : baseColor);
        }

        if (_hovered && WasPrimaryClickPressed())
            placementStation.ClickPlacementTrigger(category);
    }

    private void EnsureCategoryLabel()
    {
        if (!categoryLabel)
            categoryLabel = transform.Find(CategoryLabelName)?.GetComponent<TextMeshPro>();

        var createdLabel = false;
        if (!categoryLabel)
        {
            var labelObject = new GameObject(CategoryLabelName);
            labelObject.transform.SetParent(transform, false);
            categoryLabel = labelObject.AddComponent<TextMeshPro>();
            createdLabel = true;
        }

        categoryLabel.text = category.ToString();
        categoryLabel.alignment = TextAlignmentOptions.Center;
        categoryLabel.fontSize = categoryLabelFontSize;
        categoryLabel.textWrappingMode = TextWrappingModes.NoWrap;
        categoryLabel.overflowMode = TextOverflowModes.Truncate;
        categoryLabel.color = Color.white;
        categoryLabel.outlineWidth = 0.2f;
        categoryLabel.outlineColor = Color.black;
        categoryLabel.rectTransform.sizeDelta = new Vector2(4f, 1f);
        if (createdLabel)
        {
            categoryLabel.transform.localPosition = categoryLabelLocalOffset;
            categoryLabel.transform.localRotation = Quaternion.Euler(categoryLabelLocalEuler);
            categoryLabel.transform.localScale = categoryLabelLocalScale;
        }
    }

    private bool IsMouseOverTrigger()
    {
        if (!_mainCamera)
            _mainCamera = Camera.main ? Camera.main : FindFirstObjectByType<Camera>();

        if (!_mainCamera)
            return false;

        var ray = _mainCamera.ScreenPointToRay(GetMousePosition());
        var hits = Physics.RaycastAll(ray, Mathf.Infinity, ~0, QueryTriggerInteraction.Collide);
        System.Array.Sort(hits, (left, right) => left.distance.CompareTo(right.distance));

        foreach (var hit in hits)
        {
            if (hit.collider && hit.collider.GetComponentInParent<EndpointPlacementTrigger>() == this)
                return true;
        }

        return false;
    }

    private void SetColor(Color color)
    {
        SetMaterialColor(_material, color);
    }

    private static void SetMaterialColor(Material material, Color color)
    {
        if (!material)
            return;

        if (material.HasProperty("_BaseColor"))
            material.SetColor("_BaseColor", color);
        else if (material.HasProperty("_Color"))
            material.SetColor("_Color", color);
    }

    private void OnDisable()
    {
        SetColor(baseColor);
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

    private static bool IsGameplayInputBlocked()
    {
        return TssRuntimeUi.Instance && TssRuntimeUi.Instance.IsGameplayInputBlocked;
    }
}
