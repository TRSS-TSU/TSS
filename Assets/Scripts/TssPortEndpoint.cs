using UnityEngine;

public sealed class TssPortEndpoint : MonoBehaviour
{
    [SerializeField] private string endpointId;
    [SerializeField] private string displayLabel;
    [SerializeField] private string connectorType = "RJ45";
    [SerializeField] private TssCableType[] supportedCableTypes;
    [SerializeField] private Color selectableTint = new(0.1f, 0.9f, 0.45f, 1f);
    [SerializeField] private Color selectedTint = new(1f, 0.85f, 0.15f, 1f);
    [SerializeField, Range(0f, 1f)] private float highlightBlend = 0.55f;

    private Renderer[] _renderers;
    private Material[][] _materials;
    private Color[][] _baseColors;
    private bool _selected;
    private bool _selectable;

    public string EndpointId => string.IsNullOrWhiteSpace(endpointId) ? name : endpointId;
    public string DisplayLabel => string.IsNullOrWhiteSpace(displayLabel) ? EndpointId : displayLabel;
    public string ConnectorType => string.IsNullOrWhiteSpace(connectorType) ? "RJ45" : connectorType.Trim();
    public Vector3 CableAnchorPosition => GetCableAnchorPosition();

    private void Awake()
    {
        CacheMaterials();
    }

    public void Configure(string id, EquipmentInterface definition)
    {
        endpointId = id;

        if (definition == null)
            return;

        displayLabel = string.IsNullOrWhiteSpace(definition.label) ? definition.name : definition.label;
        connectorType = string.IsNullOrWhiteSpace(definition.connectorType) || definition.connectorType == "TBD" ? "RJ45" : definition.connectorType.Trim();
        supportedCableTypes = definition.supportedCableTypes;
    }

    public bool Supports(TssCableType cableType)
    {
        if (supportedCableTypes == null || supportedCableTypes.Length == 0)
            return true;

        foreach (var supported in supportedCableTypes)
        {
            if (supported == cableType)
                return true;
        }

        return false;
    }

    public void SetSelectable(bool selectable)
    {
        _selectable = selectable;
        ApplyHighlight();
    }

    public void SetSelected(bool selected)
    {
        _selected = selected;
        ApplyHighlight();
    }

    private void CacheMaterials()
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

    private void ApplyHighlight()
    {
        if (_materials == null)
            CacheMaterials();

        var color = _selected ? selectedTint : selectableTint;
        var highlighted = _selected || _selectable;

        for (var rendererIndex = 0; rendererIndex < _materials.Length; rendererIndex++)
        {
            var materials = _materials[rendererIndex];
            for (var materialIndex = 0; materialIndex < materials.Length; materialIndex++)
            {
                var material = materials[materialIndex];
                if (!material)
                    continue;

                var baseColor = _baseColors[rendererIndex][materialIndex];
                SetMaterialColor(material, highlighted ? Color.Lerp(baseColor, color, highlightBlend) : baseColor);
            }
        }
    }

    private Vector3 GetCableAnchorPosition()
    {
        var renderers = GetComponentsInChildren<Renderer>(true);
        if (renderers.Length > 0)
        {
            var bounds = renderers[0].bounds;
            for (var i = 1; i < renderers.Length; i++)
                bounds.Encapsulate(renderers[i].bounds);

            return bounds.center;
        }

        var colliders = GetComponentsInChildren<Collider>(true);
        if (colliders.Length > 0)
        {
            var bounds = colliders[0].bounds;
            for (var i = 1; i < colliders.Length; i++)
                bounds.Encapsulate(colliders[i].bounds);

            return bounds.center;
        }

        return transform.position;
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
}
