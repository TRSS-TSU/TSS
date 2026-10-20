using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

public sealed class TssBinderReferenceTabPresenter : MonoBehaviour
{
    public enum TabKind { CircuitLetter, Wireless, Credentials }

    [SerializeField] private TabKind tabKind;
    [SerializeField] private Text scenarioHeading;
    [SerializeField] private Text introParagraph;
    [SerializeField] private Transform tableContent;
    [SerializeField] private GameObject rowTemplate;
    [SerializeField] private Text noteText;

    public void SetValues(TssScenarioDefinition scenario, TssNetworkRunProfile profile)
    {
        if (scenario == null || profile == null)
            return;

        if (scenarioHeading)
            scenarioHeading.text = ConfiguredHeading(scenario);

        var configured = ConfiguredContent(scenario);
        var rows = configured != null && configured.rows != null && configured.rows.Length > 0
            ? BuildConfiguredRows(configured.rows, profile)
            : BuildRows(profile);
        if (introParagraph)
            introParagraph.text = configured != null && !string.IsNullOrWhiteSpace(configured.intro)
                ? ResolveTokens(configured.intro, profile)
                : BuildIntro(profile);
        if (noteText)
            noteText.text = configured != null && !string.IsNullOrWhiteSpace(configured.note)
                ? ResolveTokens(configured.note, profile)
                : BuildNote(profile);
        PopulateRows(rows);
    }

    private TssBinderTabContent ConfiguredContent(TssScenarioDefinition scenario)
    {
        if (scenario.binderReference == null)
            return null;
        return tabKind switch
        {
            TabKind.CircuitLetter => scenario.binderReference.circuitLetter,
            TabKind.Wireless => scenario.binderReference.wireless,
            _ => scenario.binderReference.credentials
        };
    }

    private string ConfiguredHeading(TssScenarioDefinition scenario)
    {
        var configured = ConfiguredContent(scenario);
        return configured != null && !string.IsNullOrWhiteSpace(configured.scenarioHeading)
            ? configured.scenarioHeading.Replace("{ScenarioId}", scenario.scenarioId ?? string.Empty)
            : "SEED " + scenario.scenarioId;
    }

    private static List<string[]> BuildConfiguredRows(IReadOnlyList<TssBinderReferenceRow> configured, TssNetworkRunProfile profile)
    {
        var rows = new List<string[]>(configured.Count);
        foreach (var row in configured)
        {
            if (row == null)
                continue;
            rows.Add(new[] { row.label ?? string.Empty, ResolveTokens(row.value, profile) });
        }
        return rows;
    }

    private static string ResolveTokens(string value, TssNetworkRunProfile profile)
    {
        var resolved = value ?? string.Empty;
        var tokens = new Dictionary<string, string>(StringComparer.Ordinal)
        {
            ["{CircuitId}"] = profile.CircuitId,
            ["{WanProviderAddress}"] = profile.WanProviderAddress,
            ["{WanCustomerAddress}"] = profile.WanCustomerAddress,
            ["{ApManagementAddress}"] = profile.ApManagementAddress,
            ["{Ssid}"] = profile.Ssid,
            ["{WifiPassphrase}"] = profile.WifiPassphrase,
            ["{WirelessChannel}"] = profile.WirelessChannel.ToString(),
            ["{ApAdminPassword}"] = profile.ApAdminPassword,
            ["{ReceptionVlanName}"] = profile.ReceptionVlanName,
            ["{ReceptionVlan}"] = profile.ReceptionVlan.ToString(),
            ["{EnableSecret}"] = profile.EnableSecret,
            ["{ConsolePassword}"] = profile.ConsolePassword,
            ["{VtyPassword}"] = profile.VtyPassword,
            ["{Motd}"] = profile.Motd
        };
        foreach (var token in tokens)
            resolved = resolved.Replace(token.Key, token.Value ?? string.Empty);
        return resolved;
    }

    private List<string[]> BuildRows(TssNetworkRunProfile profile)
    {
        switch (tabKind)
        {
            case TabKind.CircuitLetter:
                return new List<string[]>
                {
                    new[] { "Circuit ID", profile.CircuitId },
                    new[] { "Provider side", profile.WanProviderAddress + " /30" },
                    new[] { "Customer side", profile.WanCustomerAddress + " /30" },
                    new[] { "Mask", "255.255.255.252" }
                };
            case TabKind.Wireless:
                return new List<string[]>
                {
                    new[] { "AP name", "WAP1" },
                    new[] { "SSID", profile.Ssid },
                    new[] { "Security", "WPA2-PSK (AES)" },
                    new[] { "Passphrase", profile.WifiPassphrase },
                    new[] { "Channel", profile.WirelessChannel + " (2.4 GHz)" },
                    new[] { "WPS", "DISABLED" },
                    new[] { "Admin password", profile.ApAdminPassword },
                    new[] { "Uplink VLAN", profile.ReceptionVlanName + " (VLAN " + profile.ReceptionVlan + ")" },
                    new[] { "Management IP", profile.ApManagementAddress },
                    new[] { "Broadcast SSID", "Enabled" }
                };
            default:
                return new List<string[]>
                {
                    new[] { "Console password", profile.ConsolePassword },
                    new[] { "VTY password", profile.VtyPassword },
                    new[] { "Enable secret", profile.EnableSecret },
                    new[] { "Local user", "admin / 12#$Qwert" },
                    new[] { "Login banner", profile.Motd },
                    new[] { "Password encryption", "service password-encryption" },
                    new[] { "Domain lookup", "no ip domain-lookup" }
                };
        }
    }

    private string BuildIntro(TssNetworkRunProfile profile)
    {
        switch (tabKind)
        {
            case TabKind.CircuitLetter:
                return "Tapped to the NID on the IT room wall. The only place your WAN addressing appears, so never configure the provider side.";
            case TabKind.Wireless:
                return "The WAP mounts on the Guest Lounge ceiling and lands on a spare switchport in the Reception VLAN. Configure it from IT1's browser at its management IP.";
            default:
                return "Apply identically to AN-1, AN-2, and CN-1. These values are generated for the current training run.";
        }
    }

    private string BuildNote(TssNetworkRunProfile profile)
    {
        switch (tabKind)
        {
            case TabKind.CircuitLetter:
                return "Customer shall install a default route toward " + profile.WanProviderAddress + ".";
            case TabKind.Wireless:
                return "1, 6, and 11 are the only non-overlapping 2.4 GHz channels in North America. Use the assigned channel, disable WPS, and change the AP admin password.";
            default:
                return "The banner is a legal requirement, not decoration. Without it, prosecuting unauthorized access gets much harder.";
        }
    }

    private void PopulateRows(IReadOnlyList<string[]> rows)
    {
        if (!tableContent || !rowTemplate)
            return;

        for (var i = tableContent.childCount - 1; i >= 0; i--)
        {
            var child = tableContent.GetChild(i);
            if (child.gameObject != rowTemplate && child.name != "Header")
            {
                child.gameObject.SetActive(false);
                Destroy(child.gameObject);
            }
        }

        rowTemplate.SetActive(false);
        for (var i = 0; i < rows.Count; i++)
        {
            var row = Instantiate(rowTemplate, tableContent);
            row.name = "GeneratedRow" + i.ToString("00");
            row.SetActive(true);
            var rowColor = i % 2 == 0 ? new Color(0.040f, 0.090f, 0.120f, 1f) : new Color(0.025f, 0.055f, 0.075f, 1f);
            foreach (var image in row.GetComponentsInChildren<Image>(true))
                image.color = rowColor;
            var cells = row.GetComponentsInChildren<Text>(true);
            for (var cell = 0; cell < rows[i].Length && cell < cells.Length; cell++)
                cells[cell].text = rows[i][cell] ?? string.Empty;
        }
    }
}
