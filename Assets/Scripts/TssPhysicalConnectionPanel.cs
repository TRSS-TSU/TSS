using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

public sealed class TssPhysicalConnectionPanel : MonoBehaviour
{
    private enum PanelContent
    {
        PlayerCableConnections,
        PermanentInfrastructure,
        EffectiveDevicePaths
    }

    [SerializeField] private TssTrainingSession session;
    [SerializeField] private PanelContent content = PanelContent.PlayerCableConnections;
    [SerializeField] private Text titleText;
    [SerializeField] private Transform rowsContainer;
    [SerializeField] private ScrollRect scrollRect;
    [SerializeField] private Color idleColor = Color.white;
    [SerializeField] private Color activeColor = new(0.35f, 1f, 0.8f, 1f);
    [SerializeField] private Color heldColor = new(1f, 0.82f, 0.2f, 1f);
    [SerializeField] private float rowHeight = 22f;
    [SerializeField] private float rowGap = 3f;

    private readonly List<GameObject> _spawned = new();
    private RectTransform _rowsContent;
    private int _rowCount;

    private void Awake()
    {
        ResolveReferences();
        EnsureScrollableRows();
    }

    private void ResolveReferences()
    {
        if (!session)
            session = FindFirstObjectByType<TssTrainingSession>();
        if (!titleText)
            titleText = transform.Find("Title")?.GetComponent<Text>();
        if (!rowsContainer)
            rowsContainer = transform.Find("Rows");
    }

    private void OnEnable()
    {
        if (session)
            session.StateChanged += Refresh;
        Refresh();
    }

    private void OnDisable()
    {
        if (session)
            session.StateChanged -= Refresh;
    }

    public void Refresh()
    {
        ResolveReferences();
        EnsureScrollableRows();
        ClearRows();
        if (!rowsContainer || !session)
            return;

        var y = 0f;
        if (titleText)
            titleText.text = TitleForContent();

        switch (content)
        {
            case PanelContent.PermanentInfrastructure:
                y = AddConnectionGroup("Permanent Infrastructure", true, y);
                if (Mathf.Approximately(y, 0f))
                    AddRow("No permanent infrastructure", idleColor, y);
                break;
            case PanelContent.EffectiveDevicePaths:
                y = AddEffectivePathGroup(y);
                if (Mathf.Approximately(y, 0f))
                    AddRow("No effective device paths", idleColor, y);
                break;
            default:
                y = AddConnectionGroup("Player Cable Connections", false, y);
                if (Mathf.Approximately(y, 0f))
                    AddPlayerCableEmptyRow(y);
                break;
        }

        UpdateContentHeight();
    }

    private void AddPlayerCableEmptyRow(float y)
    {
        if (session.HeldCable && session.SelectedCableEndpoint)
            AddRow($"{session.HeldCable.displayName}: first port {EndpointLabel(session.SelectedCableEndpoint.EndpointId, session)}", heldColor, y);
        else if (session.HeldCable)
            AddRow($"{session.HeldCable.displayName}: select first port", heldColor, y);
        else
            AddRow("No player cable connections", idleColor, y);
    }

    private string TitleForContent()
    {
        return content switch
        {
            PanelContent.PermanentInfrastructure => "Permanent Infrastructure",
            PanelContent.EffectiveDevicePaths => "Effective Device Paths",
            _ => "Player Cable Connections"
        };
    }

    private float AddConnectionGroup(string heading, bool permanent, float y)
    {
        var any = false;
        foreach (var connection in session.PhysicalConnections)
        {
            if (connection.IsPermanent != permanent)
                continue;

            if (!any)
            {
                AddRow(heading, idleColor, y);
                y -= rowHeight + rowGap;
                any = true;
            }

            AddRow(FormatConnectionRow(connection, session), activeColor, y);
            y -= rowHeight + rowGap;
        }

        return y;
    }

    private float AddEffectivePathGroup(float y)
    {
        var paths = session.GetEffectivePhysicalPaths();
        if (paths.Count == 0)
            return y;

        AddRow("Effective Device Paths", idleColor, y);
        y -= rowHeight + rowGap;
        foreach (var path in paths)
        {
            AddRow($"{EndpointLabel(path.EndpointAId, session)} <-> {EndpointLabel(path.EndpointBId, session)}", activeColor, y);
            y -= rowHeight + rowGap;
        }

        return y;
    }

    private void AddRow(string label, Color color, float y)
    {
        var go = new GameObject("ConnectionRow", typeof(RectTransform), typeof(Text));
        go.transform.SetParent(_rowsContent ? _rowsContent : rowsContainer, false);
        var text = go.GetComponent<Text>();
        text.text = label;
        text.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        text.fontSize = 13;
        text.alignment = TextAnchor.MiddleLeft;
        text.color = color;
        text.raycastTarget = false;

        var rect = go.GetComponent<RectTransform>();
        rect.anchorMin = new Vector2(0f, 1f);
        rect.anchorMax = new Vector2(1f, 1f);
        rect.pivot = new Vector2(0.5f, 1f);
        rect.anchoredPosition = new Vector2(0f, y);
        rect.sizeDelta = new Vector2(0f, rowHeight);
        _spawned.Add(go);
        _rowCount++;
    }

    private void EnsureScrollableRows()
    {
        var viewport = rowsContainer as RectTransform;
        if (!viewport)
            return;

        if (!viewport.GetComponent<RectMask2D>())
            viewport.gameObject.AddComponent<RectMask2D>();

        if (!scrollRect)
            scrollRect = GetComponent<ScrollRect>() ? GetComponent<ScrollRect>() : gameObject.AddComponent<ScrollRect>();

        var content = viewport.Find("ScrollContent") as RectTransform;
        if (!content)
        {
            var go = new GameObject("ScrollContent", typeof(RectTransform));
            go.transform.SetParent(viewport, false);
            content = go.GetComponent<RectTransform>();
        }

        content.anchorMin = new Vector2(0f, 1f);
        content.anchorMax = new Vector2(1f, 1f);
        content.pivot = new Vector2(0.5f, 1f);
        content.anchoredPosition = Vector2.zero;
        content.sizeDelta = Vector2.zero;

        scrollRect.viewport = viewport;
        scrollRect.content = content;
        scrollRect.horizontal = false;
        scrollRect.vertical = true;
        scrollRect.movementType = ScrollRect.MovementType.Clamped;
        _rowsContent = content;
    }

    private void UpdateContentHeight()
    {
        if (!_rowsContent)
            return;

        var viewport = rowsContainer as RectTransform;
        var minHeight = viewport ? viewport.rect.height : 0f;
        var height = Mathf.Max(minHeight, Mathf.Max(0f, _rowCount * (rowHeight + rowGap) - rowGap));
        _rowsContent.sizeDelta = new Vector2(0f, height);
        if (scrollRect)
            scrollRect.verticalNormalizedPosition = 1f;
    }

    public static string FormatConnectionRow(TssPhysicalConnectionRecord connection)
    {
        return FormatConnectionRow(connection, null);
    }

    public static string FormatConnectionRow(TssPhysicalConnectionRecord connection, TssTrainingSession session)
    {
        return $"{connection.CableType}: {EndpointLabel(connection.EndpointAId, session)} <-> {EndpointLabel(connection.EndpointBId, session)}";
    }

    private static string EndpointLabel(string endpointId, TssTrainingSession session)
    {
        var label = endpointId;
        foreach (var endpoint in Object.FindObjectsByType<TssPortEndpoint>(FindObjectsInactive.Include, FindObjectsSortMode.None))
        {
            if (endpoint.EndpointId == endpointId)
            {
                label = endpoint.DisplayLabel;
                break;
            }
        }

        var deviceName = DeviceName(endpointId, session);
        return string.IsNullOrWhiteSpace(deviceName) ? label : $"{deviceName.Trim()} {label}";
    }

    private static string DeviceName(string endpointId, TssTrainingSession session)
    {
        if (!session)
            return string.Empty;

        foreach (var record in session.Installed)
        {
            if (endpointId.StartsWith(TssTrainingSession.GetRackOwnerPrefix(record.RackId, record.StartingU) + ":", System.StringComparison.Ordinal))
                return record.DeviceName;
        }

        foreach (var record in session.EndpointPlacements)
        {
            if (endpointId.StartsWith(TssTrainingSession.GetEndpointOwnerPrefix(record.PlacementId) + ":", System.StringComparison.Ordinal))
                return record.DeviceName;
        }

        return string.Empty;
    }

    private void ClearRows()
    {
        var parent = _rowsContent ? _rowsContent : rowsContainer;
        for (var i = parent ? parent.childCount - 1 : -1; i >= 0; i--)
        {
            var child = parent.GetChild(i).gameObject;
            child.transform.SetParent(null, false);
            if (Application.isPlaying)
                Destroy(child);
            else
                DestroyImmediate(child);
        }

        _spawned.Clear();
        _rowCount = 0;
    }
}
