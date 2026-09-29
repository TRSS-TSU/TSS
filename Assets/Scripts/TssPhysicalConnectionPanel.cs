using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

public sealed class TssPhysicalConnectionPanel : MonoBehaviour
{
    [SerializeField] private TssTrainingSession session;
    [SerializeField] private Text titleText;
    [SerializeField] private Transform rowsContainer;
    [SerializeField] private Color idleColor = Color.white;
    [SerializeField] private Color activeColor = new(0.35f, 1f, 0.8f, 1f);
    [SerializeField] private Color heldColor = new(1f, 0.82f, 0.2f, 1f);
    [SerializeField] private float rowHeight = 22f;
    [SerializeField] private float rowGap = 3f;

    private readonly List<GameObject> _spawned = new();

    private void Awake()
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
        ClearRows();
        if (!rowsContainer || !session)
            return;

        if (titleText)
            titleText.text = "Cable Connections";

        var y = 0f;
        if (session.PhysicalConnections.Count > 0)
        {
            foreach (var connection in session.PhysicalConnections)
            {
                AddRow(FormatConnectionRow(connection, session), activeColor, y);
                y -= rowHeight + rowGap;
            }

            return;
        }

        if (session.HeldCable && session.SelectedCableEndpoint)
            AddRow($"{session.HeldCable.displayName}: first port {EndpointLabel(session.SelectedCableEndpoint.EndpointId, session)}", heldColor, y);
        else if (session.HeldCable)
            AddRow($"{session.HeldCable.displayName}: select first port", heldColor, y);
        else
            AddRow("No physical connections", idleColor, y);
    }

    private void AddRow(string label, Color color, float y)
    {
        var go = new GameObject("ConnectionRow", typeof(RectTransform), typeof(Text));
        go.transform.SetParent(rowsContainer, false);
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
        for (var i = 0; i < _spawned.Count; i++)
        {
            if (_spawned[i])
                Destroy(_spawned[i]);
        }

        _spawned.Clear();
    }
}
