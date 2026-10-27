using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

public sealed class TssCableScheduleTabPresenter : MonoBehaviour
{
    [SerializeField] private Text scenarioHeading;
    [SerializeField] private Text[] cells;
    [SerializeField] private Transform tableContent;
    [SerializeField] private GameObject rowTemplate;

    private Color[][] prefabRowStyles;

    private static readonly string[,] Rows =
    {
        { "AN-1", "Port 1", "AN-2", "Port 1", "Crossover" },
        { "IT1", "NIC", "AN-1", "Port 2", "Straight" },
        { "IT2", "NIC", "AN-1", "Port 5", "Straight" },
        { "Reception1", "NIC", "Reception Mount", "Port 1", "Straight" },
        { "Reception2", "NIC", "Reception Mount", "Port 2", "Straight" },
        { "Reception Mount", "Punch 1", "Patch1", "Punch 1", "Structural" },
        { "Reception Mount", "Punch 2", "Patch1", "Punch 2", "Structural" },
        { "Patch1", "Port 1", "AN-1", "Port 3", "Patch cord" },
        { "Patch1", "Port 2", "AN-1", "Port 4", "Patch cord" },
        { "Data1", "NIC", "Office 1 Mount", "Port 1", "Straight" },
        { "Data2", "NIC", "Office 2 Mount", "Port 1", "Straight" },
        { "Data3", "NIC", "Office 2 Mount", "Port 2", "Straight" },
        { "Office 1 Mount", "Punch 1", "Patch2", "Punch 1", "Structural" },
        { "Office 2 Mount", "Punch 1", "Patch2", "Punch 2", "Structural" },
        { "Office 2 Mount", "Punch 2", "Patch2", "Punch 3", "Structural" },
        { "Patch2", "Port 1", "AN-2", "Port 2", "Patch cord" },
        { "Patch2", "Port 2", "AN-2", "Port 3", "Patch cord" },
        { "Patch2", "Port 3", "AN-2", "Port 4", "Patch cord" }
    };

    public void SetValues(string scenarioId, TssNetworkRunProfile profile, ScenarioCableScheduleEntry[] scenarioRows = null, TssBinderCableScheduleContent content = null)
    {
        if (scenarioHeading)
            scenarioHeading.text = string.IsNullOrWhiteSpace(content?.scenarioHeading)
                ? "CABLE RUN SCHEDULE - " + (scenarioId ?? string.Empty)
                : content.scenarioHeading.Replace("{ScenarioId}", scenarioId ?? string.Empty);

        if (content != null && !string.IsNullOrWhiteSpace(content.introParagraph))
        {
            var intro = transform.Find("IntroParagraph")?.GetComponent<Text>();
            if (intro)
                intro.text = content.introParagraph;
        }

        if (scenarioRows != null && scenarioRows.Length > 0 && tableContent && rowTemplate)
        {
            PopulateRows(scenarioRows);
            return;
        }

        for (var row = 0; row < Rows.GetLength(0); row++)
        {
            for (var column = 0; column < Rows.GetLength(1); column++)
            {
                var value = Rows[row, column];
                if (profile != null)
                    value = ResolveDynamicValue(row, column, value, profile);
                SetCell(row * Rows.GetLength(1) + column, value);
            }
        }
    }

    private void PopulateRows(IReadOnlyList<ScenarioCableScheduleEntry> scenarioRows)
    {
        prefabRowStyles ??= CaptureRowStyles();

        for (var i = tableContent.childCount - 1; i >= 1; i--)
        {
            var child = tableContent.GetChild(i);
            if (child.gameObject == rowTemplate || child.name == "Notes")
                continue;
            Destroy(child.gameObject);
        }

        rowTemplate.SetActive(false);
        var notes = tableContent.Find("Notes");
        for (var i = 0; i < scenarioRows.Count; i++)
        {
            var entry = scenarioRows[i];
            if (entry == null)
                continue;

            var row = Instantiate(rowTemplate, tableContent);
            row.name = "GeneratedRow" + i.ToString("00");
            if (notes)
                row.transform.SetSiblingIndex(notes.GetSiblingIndex());
            row.SetActive(true);
            var rowCells = row.GetComponentsInChildren<Text>(true);
            SetRowCell(rowCells, 0, DisplayName(entry.fromEndpoint));
            SetRowCell(rowCells, 1, DisplayPort(entry.fromPort));
            SetRowCell(rowCells, 2, DisplayName(entry.toEndpoint));
            SetRowCell(rowCells, 3, DisplayPort(entry.toPort));
            SetRowCell(rowCells, 4, entry.cableType);
            ApplyRowStyle(row, prefabRowStyles, i);
        }
    }

    private Color[][] CaptureRowStyles()
    {
        var styles = new List<Color[]>();
        for (var i = 0; i < tableContent.childCount && styles.Count < 2; i++)
        {
            var row = tableContent.GetChild(i);
            if (row.name == "Notes")
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

    private static void SetRowCell(Text[] rowCells, int index, string value)
    {
        if (rowCells != null && index >= 0 && index < rowCells.Length && rowCells[index])
        {
            var text = rowCells[index];
            text.text = value ?? string.Empty;
        }
    }

    private static string DisplayName(string value)
    {
        return (value ?? string.Empty)
            .Replace("Mount", "Wallport")
            .Replace("Patch1", "Patch Panel 1")
            .Replace("Patch2", "Patch Panel 2");
    }

    private static string DisplayPort(string value) => (value ?? string.Empty).Replace("Punch", "Port");

    private string ResolveDynamicValue(int row, int column, string fallback, TssNetworkRunProfile profile)
    {
        if ((column != 2 && column != 3) || row == 0)
            return fallback;

        var endpoint = row switch
        {
            1 => "IT1",
            2 => "IT2",
            7 => "Reception1",
            8 => "Reception2",
            15 => "Data1",
            16 => "Data2",
            17 => "Data3",
            _ => string.Empty
        };
        if (endpoint != "IT1" && endpoint != "IT2" && endpoint != "Reception1" && endpoint != "Reception2" && endpoint != "Data1" && endpoint != "Data2" && endpoint != "Data3")
            return fallback;

        var switchPort = SwitchPortFor(endpoint, profile);
        if (switchPort == null)
            return fallback;
        return column == 2 ? switchPort.Value.Role : switchPort.Value.Port;
    }

    private static SwitchPort? SwitchPortFor(string endpointId, TssNetworkRunProfile profile)
    {
        foreach (var required in profile.RequiredPorts ?? Array.Empty<NetworkRequiredPort>())
        {
            if (!string.Equals(required.endpointId, endpointId, StringComparison.OrdinalIgnoreCase))
                continue;

            var interfaceName = required.interfaceName ?? string.Empty;
            var port = interfaceName.StartsWith("Fa0/", StringComparison.OrdinalIgnoreCase)
                ? interfaceName.Substring(4)
                : interfaceName;
            return new SwitchPort(required.deviceRole, "Port " + port);
        }

        return null;
    }

    private readonly struct SwitchPort
    {
        public readonly string Role;
        public readonly string Port;

        public SwitchPort(string role, string port)
        {
            Role = role;
            Port = port;
        }
    }

    private void SetCell(int index, string value)
    {
        if (cells != null && index >= 0 && index < cells.Length && cells[index])
            cells[index].text = value ?? string.Empty;
    }
}
