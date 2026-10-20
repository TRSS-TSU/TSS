using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

public sealed class TssNamingTabPresenter : MonoBehaviour
{
    [SerializeField] private Text scenarioHeading;
    [SerializeField] private Transform firstFloorTable;
    [SerializeField] private GameObject firstFloorRowTemplate;
    [SerializeField] private Transform secondFloorTable;
    [SerializeField] private GameObject secondFloorRowTemplate;

    private Color[][] firstFloorRowStyles;
    private Color[][] secondFloorRowStyles;

    public void SetValues(TssScenarioDefinition scenario)
    {
        if (!scenario || scenario.namingRows == null || scenario.namingRows.Length == 0)
            return;

        var configured = scenario.binderReference?.namingAndRacks;
        var heading = scenarioHeading ? scenarioHeading : transform.Find("ScenarioHeading")?.GetComponent<Text>();
        if (heading)
            heading.text = string.IsNullOrWhiteSpace(configured?.scenarioHeading)
                ? "NAMING & RACKS - " + (scenario.scenarioId ?? string.Empty)
                : configured.scenarioHeading.Replace("{ScenarioId}", scenario.scenarioId ?? string.Empty);

        var firstFloor = new List<ScenarioNamingEntry>();
        var secondFloor = new List<ScenarioNamingEntry>();
        foreach (var entry in scenario.namingRows)
        {
            if (entry == null)
                continue;
            if (string.Equals(entry.rackId, "Rack02", StringComparison.OrdinalIgnoreCase))
                secondFloor.Add(entry);
            else
                firstFloor.Add(entry);
        }

        Populate(firstFloorTable, firstFloorRowTemplate, firstFloor, true, ref firstFloorRowStyles);
        Populate(secondFloorTable, secondFloorRowTemplate, secondFloor, false, ref secondFloorRowStyles);
    }

    private static void Populate(Transform table, GameObject template, IReadOnlyList<ScenarioNamingEntry> entries, bool includeRationale, ref Color[][] rowStyles)
    {
        if (!table || !template)
            return;

        rowStyles ??= CaptureRowStyles(table);

        for (var i = table.childCount - 1; i >= 1; i--)
        {
            var child = table.GetChild(i);
            if (child.gameObject == template)
                continue;
            UnityEngine.Object.Destroy(child.gameObject);
        }

        template.SetActive(false);
        for (var i = 0; i < entries.Count; i++)
        {
            var entry = entries[i];
            var row = Instantiate(template, table);
            row.name = "GeneratedRow" + i.ToString("00");
            row.SetActive(true);
            var texts = row.GetComponentsInChildren<Text>(true);
            SetText(texts, 0, entry.slot);
            SetText(texts, 1, entry.device);
            SetText(texts, 2, entry.assignedName);
            if (includeRationale)
                SetText(texts, 3, entry.rationale);
            ApplyRowStyle(row, rowStyles, i);
        }
    }

    private static Color[][] CaptureRowStyles(Transform table)
    {
        var styles = new List<Color[]>();
        for (var i = 0; i < table.childCount && styles.Count < 2; i++)
        {
            var row = table.GetChild(i);
            if (row.name == "Row01")
                continue;

            var colors = new Color[row.childCount];
            var valid = true;
            for (var cell = 0; cell < row.childCount; cell++)
            {
                var image = row.GetChild(cell).GetComponent<UnityEngine.UI.Image>();
                if (!image)
                {
                    valid = false;
                    break;
                }

                colors[cell] = image.color;
            }

            if (valid)
                styles.Add(colors);
        }

        return styles.Count == 2 ? styles.ToArray() : null;
    }

    private static void ApplyRowStyle(GameObject row, Color[][] styles, int rowIndex)
    {
        if (styles == null || styles.Length == 0)
            return;

        var colors = styles[rowIndex % styles.Length];
        for (var cell = 0; cell < row.transform.childCount && cell < colors.Length; cell++)
        {
            var image = row.transform.GetChild(cell).GetComponent<UnityEngine.UI.Image>();
            if (image)
                image.color = colors[cell];
        }
    }

    private static void SetText(Text[] texts, int index, string value)
    {
        if (texts != null && index >= 0 && index < texts.Length && texts[index])
            texts[index].text = value ?? string.Empty;
    }
}
