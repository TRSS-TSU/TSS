using System.Globalization;
using UnityEngine;
using UnityEngine.UI;

public sealed class TssVlanTabPresenter : MonoBehaviour
{
    [SerializeField] private Text scenarioHeading;
    [SerializeField] private Text[] vlanIds;
    [SerializeField] private Text endingParagraph;

    public void SetValues(string scenarioId, int managementVlan, int receptionVlan, int dataVlan, int nativeVlan, int deadVlan, TssBinderVlanContent content = null, TssNetworkRunProfile profile = null)
    {
        if (scenarioHeading)
            scenarioHeading.text = string.IsNullOrWhiteSpace(content?.scenarioHeading)
                ? "VLAN ASSIGNMENT - " + (scenarioId ?? string.Empty)
                : ResolveTokens(content.scenarioHeading, scenarioId, managementVlan, receptionVlan, dataVlan, nativeVlan, deadVlan);

        SetVlanId(0, managementVlan);
        SetVlanId(1, receptionVlan);
        SetVlanId(2, dataVlan);
        SetVlanId(3, nativeVlan);
        SetVlanId(4, deadVlan);

        if (profile != null)
        {
            var names = new[] { profile.ManagementVlanName, profile.ReceptionVlanName, profile.DataVlanName, profile.NativeVlanName, profile.DeadVlanName };
            for (var i = 0; i < names.Length; i++)
            {
                var label = transform.Find($"VlanTable/Row{i + 2:00}/Cell02/Text")?.GetComponent<Text>();
                if (label) label.text = names[i];
            }
        }

        if (endingParagraph)
        {
            endingParagraph.text = string.IsNullOrWhiteSpace(content?.endingParagraph)
                ?
                "Trunk (Fa0/1 both switches): 802.1Q, allowed "
                + managementVlan.ToString(CultureInfo.InvariantCulture) + ","
                + receptionVlan.ToString(CultureInfo.InvariantCulture) + ","
                + dataVlan.ToString(CultureInfo.InvariantCulture) + ", native "
                + nativeVlan.ToString(CultureInfo.InvariantCulture)
                + ". Every unused port: access VLAN "
                + deadVlan.ToString(CultureInfo.InvariantCulture) + " + shutdown."
                : ResolveTokens(content.endingParagraph, scenarioId, managementVlan, receptionVlan, dataVlan, nativeVlan, deadVlan);
        }

        if (content != null && !string.IsNullOrWhiteSpace(content.intro))
        {
            var intro = transform.Find("IntroParagraph")?.GetComponent<Text>();
            if (intro)
                intro.text = content.intro;
        }
    }

    private static string ResolveTokens(string value, string scenarioId, int managementVlan, int receptionVlan, int dataVlan, int nativeVlan, int deadVlan)
    {
        return (value ?? string.Empty)
            .Replace("{ScenarioId}", scenarioId ?? string.Empty)
            .Replace("{ManagementVlan}", managementVlan.ToString(CultureInfo.InvariantCulture))
            .Replace("{ReceptionVlan}", receptionVlan.ToString(CultureInfo.InvariantCulture))
            .Replace("{DataVlan}", dataVlan.ToString(CultureInfo.InvariantCulture))
            .Replace("{NativeVlan}", nativeVlan.ToString(CultureInfo.InvariantCulture))
            .Replace("{DeadVlan}", deadVlan.ToString(CultureInfo.InvariantCulture));
    }

    private void SetVlanId(int index, int vlan)
    {
        if (vlanIds != null && index >= 0 && index < vlanIds.Length && vlanIds[index])
            vlanIds[index].text = vlan.ToString(CultureInfo.InvariantCulture);
    }
}
