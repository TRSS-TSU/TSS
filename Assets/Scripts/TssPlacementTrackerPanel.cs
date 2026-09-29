using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

public sealed class TssPlacementTrackerPanel : MonoBehaviour
{
    [SerializeField] private TssTrainingSession session;
    [SerializeField] private TssPlacementTrackerRow rowPrefab;
    [SerializeField] private Transform rowsContainer;
    [SerializeField] private Color pendingColor = Color.white;
    [SerializeField] private Color completeColor = new(0.35f, 1f, 0.45f, 1f);
    [SerializeField] private Color phaseColor = new(1f, 0.68f, 0.2f, 1f);
    [SerializeField] private float phaseHeight = 20f;
    [SerializeField] private float rowHeight = 24f;
    [SerializeField] private float rowGap = 2f;
    [SerializeField] private float phaseGap = 7f;

    private readonly List<GameObject> _spawned = new();

    private void Awake()
    {
        if (!session)
            session = FindFirstObjectByType<TssTrainingSession>();
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
        if (!session || !rowsContainer || !rowPrefab)
            return;

        var statuses = TssPlacementEvaluator.EvaluateRequirements(session.Scenario, session.Installed, session.EndpointPlacements);
        var currentPhase = string.Empty;
        var y = 0f;
        foreach (var status in statuses)
        {
            var phase = string.IsNullOrWhiteSpace(status.PhaseLabel) ? "Placement" : status.PhaseLabel;
            if (phase != currentPhase)
            {
                if (!string.IsNullOrEmpty(currentPhase))
                    y -= phaseGap;

                currentPhase = phase;
                Position(AddPhaseHeader(phase), y, phaseHeight);
                y -= phaseHeight + rowGap;
            }

            var row = Instantiate(rowPrefab, rowsContainer, false);
            row.SetStatus(status, status.IsComplete ? completeColor : pendingColor);
            Position(row.GetComponent<RectTransform>(), y, rowHeight);
            _spawned.Add(row.gameObject);
            y -= rowHeight + rowGap;
        }
    }

    private RectTransform AddPhaseHeader(string label)
    {
        var go = new GameObject(label, typeof(RectTransform), typeof(Text));
        go.transform.SetParent(rowsContainer, false);
        var text = go.GetComponent<Text>();
        text.text = label.ToUpperInvariant();
        text.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        text.fontSize = 13;
        text.alignment = TextAnchor.MiddleLeft;
        text.color = phaseColor;
        _spawned.Add(go);
        return go.GetComponent<RectTransform>();
    }

    private void Position(RectTransform rect, float y, float height)
    {
        if (!rect)
            return;

        rect.anchorMin = new Vector2(0f, 1f);
        rect.anchorMax = new Vector2(1f, 1f);
        rect.pivot = new Vector2(0.5f, 1f);
        rect.anchoredPosition = new Vector2(0f, y);
        rect.sizeDelta = new Vector2(0f, height);
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
