using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using UnityEngine;
using UnityEngine.UI;

public sealed class TssIpPlanTabPresenter : MonoBehaviour
{
    [SerializeField] private Text scenarioHeading;
    [SerializeField] private Text[] addressingCells;
    [SerializeField] private Text[] hostCells;
    [SerializeField] private Transform addressingTable;
    [SerializeField] private GameObject addressingRowTemplate;
    [SerializeField] private Transform hostTable;
    [SerializeField] private GameObject hostRowTemplate;

    private Color[][] addressingRowStyles;
    private Color[][] hostRowStyles;

    public void SetValues(string scenarioId, TssNetworkRunProfile profile, TssBinderIpPlanContent content = null)
    {
        if (profile == null)
            return;

        if (scenarioHeading)
            scenarioHeading.text = string.IsNullOrWhiteSpace(content?.scenarioHeading)
                ? "ADDRESSING PLAN - " + (scenarioId ?? string.Empty)
                : content.scenarioHeading.Replace("{ScenarioId}", scenarioId ?? string.Empty);

        if (content != null && !string.IsNullOrWhiteSpace(content.paragraph))
        {
            var paragraph = transform.Find("Paragraph")?.GetComponent<Text>();
            if (paragraph)
                paragraph.text = content.paragraph;
        }

        if (addressingTable && addressingRowTemplate && hostTable && hostRowTemplate)
        {
            PopulateAddressingRows(profile);
            PopulateHostRows(profile);
            return;
        }

        var networks = new[]
        {
            new { Vlan = profile.ManagementVlan, Name = profile.ManagementVlanName, Network = profile.ManagementNetwork },
            new { Vlan = profile.ReceptionVlan, Name = profile.ReceptionVlanName, Network = profile.ReceptionNetwork },
            new { Vlan = profile.DataVlan, Name = profile.DataVlanName, Network = profile.DataNetwork }
        };

        for (var i = 0; i < networks.Length; i++)
        {
            var network = networks[i];
            SetAddressingCell(i, 0, network.Vlan.ToString(CultureInfo.InvariantCulture) + " " + network.Name);
            SetAddressingCell(i, 1, network.Network + ".0/24");
            SetAddressingCell(i, 2, network.Network + ".1");
            SetAddressingCell(i, 3, "Fa0/0." + network.Vlan.ToString(CultureInfo.InvariantCulture));
        }

        var hosts = new[] { "IT1", "IT2", "Reception1", "Reception2", "Data1", "Data2", "Data3" };
        for (var i = 0; i < hosts.Length; i++)
        {
            var host = hosts[i];
            if (!profile.EndpointAddresses.TryGetValue(host, out var address))
                address = string.Empty;

            var vlan = VlanForAddress(address, profile);
            SetHostCell(i, 0, vlan.ToString(CultureInfo.InvariantCulture));
            SetHostCell(i, 1, address);
            SetHostCell(i, 2, "255.255.255.0");
            SetHostCell(i, 3, EndpointNetworkState.GatewayFor(address, profile));
            SetHostCell(i, 4, SwitchportFor(host, profile));
        }
    }

    private void PopulateAddressingRows(TssNetworkRunProfile profile)
    {
        var rows = new[]
        {
            new[] { profile.ManagementVlan.ToString(CultureInfo.InvariantCulture) + " " + profile.ManagementVlanName, profile.ManagementNetwork + ".0/24", profile.ManagementNetwork + ".1", "Fa0/0." + profile.ManagementVlan.ToString(CultureInfo.InvariantCulture) },
            new[] { profile.ReceptionVlan.ToString(CultureInfo.InvariantCulture) + " " + profile.ReceptionVlanName, profile.ReceptionNetwork + ".0/24", profile.ReceptionNetwork + ".1", "Fa0/0." + profile.ReceptionVlan.ToString(CultureInfo.InvariantCulture) },
            new[] { profile.DataVlan.ToString(CultureInfo.InvariantCulture) + " " + profile.DataVlanName, profile.DataNetwork + ".0/24", profile.DataNetwork + ".1", "Fa0/0." + profile.DataVlan.ToString(CultureInfo.InvariantCulture) }
        };

        var rowObjects = PrepareRows(addressingTable, addressingRowTemplate, rows.Length, ref addressingRowStyles);
        for (var i = 0; i < rows.Length; i++)
            SetRowTexts(rowObjects[i], rows[i]);
    }

    private void PopulateHostRows(TssNetworkRunProfile profile)
    {
        var hosts = (profile.EndpointAddresses?.Keys ?? Enumerable.Empty<string>())
            .Where(key => !string.Equals(key, "AP-1", StringComparison.OrdinalIgnoreCase) && !string.Equals(key, "TestLaptop", StringComparison.OrdinalIgnoreCase))
            .OrderBy(HostOrder)
            .ThenBy(key => key, StringComparer.OrdinalIgnoreCase)
            .ToArray();
        var rowObjects = PrepareRows(hostTable, hostRowTemplate, hosts.Length, ref hostRowStyles);
        for (var i = 0; i < hosts.Length; i++)
        {
            var host = hosts[i];
            profile.EndpointAddresses.TryGetValue(host, out var address);
            var values = new[]
            {
                host,
                VlanForAddress(address, profile).ToString(CultureInfo.InvariantCulture),
                address,
                "255.255.255.0",
                EndpointNetworkState.GatewayFor(address, profile),
                SwitchportFor(host, profile)
            };
            SetRowTexts(rowObjects[i], values);
        }
    }

    private static List<GameObject> PrepareRows(Transform table, GameObject template, int count, ref Color[][] rowStyles)
    {
        rowStyles ??= CaptureRowStyles(table);

        for (var i = table.childCount - 1; i >= 1; i--)
        {
            var child = table.GetChild(i);
            if (child.gameObject == template)
                continue;
            UnityEngine.Object.Destroy(child.gameObject);
        }

        template.SetActive(false);
        var rows = new List<GameObject>(count);
        for (var i = 0; i < count; i++)
        {
            var row = Instantiate(template, table);
            row.name = "GeneratedRow" + i.ToString("00");
            row.SetActive(true);
            ApplyRowStyle(row, rowStyles, i);
            rows.Add(row);
        }
        return rows;
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

    private static void SetRowTexts(GameObject row, IReadOnlyList<string> values)
    {
        var texts = row.GetComponentsInChildren<Text>(true);
        for (var i = 0; i < values.Count && i < texts.Length; i++)
            texts[i].text = values[i] ?? string.Empty;
    }

    private static int HostOrder(string host)
    {
        var preferred = new[] { "IT1", "IT2", "Reception1", "Reception2", "Data1", "Data2", "Data3" };
        var index = Array.FindIndex(preferred, value => string.Equals(value, host, StringComparison.OrdinalIgnoreCase));
        return index < 0 ? preferred.Length : index;
    }

    private void SetAddressingCell(int row, int column, string value)
    {
        SetText(addressingCells, row * 4 + column, value);
    }

    private void SetHostCell(int row, int column, string value)
    {
        SetText(hostCells, row * 5 + column, value);
    }

    private static void SetText(Text[] cells, int index, string value)
    {
        if (cells != null && index >= 0 && index < cells.Length && cells[index])
            cells[index].text = value ?? string.Empty;
    }

    private static int VlanForAddress(string address, TssNetworkRunProfile profile)
    {
        if (string.IsNullOrWhiteSpace(address))
            return 0;
        if (address.StartsWith(profile.ManagementNetwork + ".", StringComparison.Ordinal))
            return profile.ManagementVlan;
        if (address.StartsWith(profile.ReceptionNetwork + ".", StringComparison.Ordinal))
            return profile.ReceptionVlan;
        if (address.StartsWith(profile.DataNetwork + ".", StringComparison.Ordinal))
            return profile.DataVlan;
        return 0;
    }

    private static string SwitchportFor(string endpointId, TssNetworkRunProfile profile)
    {
        foreach (var port in profile.RequiredPorts ?? Array.Empty<NetworkRequiredPort>())
        {
            if (!string.Equals(port.endpointId, endpointId, StringComparison.OrdinalIgnoreCase))
                continue;

            var interfaceName = port.interfaceName ?? string.Empty;
            var portNumber = interfaceName.StartsWith("Fa0/", StringComparison.OrdinalIgnoreCase)
                ? interfaceName.Substring(4)
                : interfaceName;
            return port.deviceRole + " Port " + portNumber + " (" + interfaceName + ")";
        }

        return string.Empty;
    }
}
