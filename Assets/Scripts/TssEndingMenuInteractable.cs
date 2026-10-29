using UnityEngine;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif

public sealed class TssEndingMenuInteractable : MonoBehaviour
{
    [SerializeField, Min(0f)] private float interactionRange = 4f;
    [SerializeField] private Color hoverTint = new(0.2f, 0.75f, 1f, 1f);
    [SerializeField, Range(0f, 1f)] private float hoverBlend = 0.45f;

    private Camera _mainCamera;
    private Renderer[] _renderers;
    private Material[][] _materials;
    private Color[][] _baseColors;
    private bool _hovered;

    private void Awake()
    {
        _mainCamera = Camera.main;
        _renderers = GetComponentsInChildren<Renderer>(true);
        _materials = new Material[_renderers.Length][];
        _baseColors = new Color[_renderers.Length][];

        for (var rendererIndex = 0; rendererIndex < _renderers.Length; rendererIndex++)
        {
            _materials[rendererIndex] = _renderers[rendererIndex].materials;
            _baseColors[rendererIndex] = new Color[_materials[rendererIndex].Length];

            for (var materialIndex = 0; materialIndex < _materials[rendererIndex].Length; materialIndex++)
                _baseColors[rendererIndex][materialIndex] = GetMaterialColor(_materials[rendererIndex][materialIndex]);
        }
    }

    private void Update()
    {
        var canInteract = !IsGameplayInputBlocked() && IsPlayerInRange();
        var hovered = canInteract && IsMouseOverTrigger();
        if (_hovered != hovered)
        {
            _hovered = hovered;
            SetHighlight(hovered);
        }

        if (!hovered || !WasPrimaryClickPressed())
            return;

        ShowMenu();
    }

    private void OnMouseDown()
    {
        ShowMenu();
    }

    private void ShowMenu()
    {
        if (!IsGameplayInputBlocked() && TssRuntimeUi.Instance)
            TssRuntimeUi.Instance.ShowEndScenarioMenu();
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
            if (hit.collider && (hit.collider.transform == transform || hit.collider.transform.IsChildOf(transform)))
                return true;
        }

        return false;
    }

    private bool IsPlayerInRange()
    {
        var player = GameObject.FindGameObjectWithTag("Player");
        return !player || interactionRange <= 0f || Vector3.Distance(player.transform.position, transform.position) <= interactionRange;
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

    private void SetHighlight(bool highlighted)
    {
        if (_materials == null || _baseColors == null)
            return;

        for (var rendererIndex = 0; rendererIndex < _materials.Length; rendererIndex++)
        {
            for (var materialIndex = 0; materialIndex < _materials[rendererIndex].Length; materialIndex++)
            {
                var material = _materials[rendererIndex][materialIndex];
                if (!material)
                    continue;

                var color = _baseColors[rendererIndex][materialIndex];
                SetMaterialColor(material, highlighted ? Color.Lerp(color, hoverTint, hoverBlend) : color);
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
        if (!material)
            return;

        if (material.HasProperty("_BaseColor"))
            material.SetColor("_BaseColor", color);
        else if (material.HasProperty("_Color"))
            material.SetColor("_Color", color);
    }

    private void OnDisable()
    {
        SetHighlight(false);
    }
}
