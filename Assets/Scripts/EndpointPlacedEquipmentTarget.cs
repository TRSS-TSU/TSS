using UnityEngine;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif

public sealed class EndpointPlacedEquipmentTarget : MonoBehaviour
{
    [SerializeField] private Color hoverTint = new(0.2f, 0.75f, 1f, 1f);
    [SerializeField, Range(0f, 1f)] private float hoverBlend = 0.45f;

    private EndpointPlacementStation _station;
    private string _placementId;
    private EquipmentDefinition _equipment;
    private Renderer[] _renderers;
    private Material[][] _materials;
    private Color[][] _baseColors;
    private Camera _mainCamera;
    private bool _isHovered;
    private TssObjectInteractable _interactable;

    public void Initialize(EndpointPlacementStation station, string placementId, EquipmentDefinition equipment)
    {
        _station = station;
        _placementId = placementId;
        _equipment = equipment;
        _mainCamera = Camera.main;
        _interactable = GetComponentInChildren<TssObjectInteractable>(true);
        CacheRenderers();
    }

    private void Update()
    {
        var hovered = EndpointPlacementStation.CanInspectPlacedEndpoint(TssTrainingSession.Instance)
            && (!_interactable || !_interactable.IsFirstPerson)
            && IsMouseOverTarget();
        SetHighlight(hovered);
        _isHovered = hovered;

        if (_isHovered && WasPrimaryClickPressed() && _station)
            _station.ClickPlaced(_placementId, _equipment, this);
    }

    private void OnDisable()
    {
        SetHighlight(false);
    }

    private bool IsMouseOverTarget()
    {
        if (!_mainCamera)
            _mainCamera = Camera.main;

        if (!_mainCamera)
            return false;

        var ray = _mainCamera.ScreenPointToRay(GetMousePosition());
        var hits = Physics.RaycastAll(ray, Mathf.Infinity, ~0, QueryTriggerInteraction.Collide);
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
        if (!CanHighlight() || _materials == null)
            return;

        if (TssRuntimeUi.Instance && TssRuntimeUi.Instance.IsGameplayInputBlocked)
            highlighted = false;

        for (var rendererIndex = 0; rendererIndex < _materials.Length; rendererIndex++)
        {
            if (_renderers[rendererIndex] && _renderers[rendererIndex].GetComponentInParent<TssPortEndpoint>())
                continue;

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

    private bool CanHighlight()
    {
        return _equipment
            && (_equipment.category == EquipmentCategory.DesktopPc || _equipment.category == EquipmentCategory.Printer);
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
