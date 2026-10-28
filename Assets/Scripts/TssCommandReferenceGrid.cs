using System;
using System.Text;
using UnityEngine;
using UnityEngine.UI;

public sealed class TssCommandReferenceGrid : MonoBehaviour
{
    private const string HeadingColor = "#F2B84B";
    private const string DividerColor = "#54758E";
    private const string Divider = "────────────────────────────────";

    private Text _commandColumn;
    private Text _descriptionColumn;

    public void SetReference(EquipmentCategory category)
    {
        FindColumns();
        _commandColumn.supportRichText = true;
        _descriptionColumn.supportRichText = true;

        var commands = new StringBuilder(Colorize("COMMAND", HeadingColor) + "\n");
        var descriptions = new StringBuilder(Colorize("WHAT IT DOES", HeadingColor) + "\n");

        foreach (var line in TssCliCommandProcessor.BuildCommandReference(category).Split(new[] { '\n' }, StringSplitOptions.None))
        {
            if (string.IsNullOrWhiteSpace(line))
            {
                commands.AppendLine();
                descriptions.AppendLine();
                continue;
            }

            if (line.Contains("COMMAND") && line.Contains("WHAT IT DOES"))
                continue;

            if (line.Contains("---+"))
            {
                commands.AppendLine(Colorize(Divider, DividerColor));
                descriptions.AppendLine(Colorize(Divider, DividerColor));
                continue;
            }

            var separator = line.IndexOf('\t');
            var separatorLength = 1;
            if (separator < 0)
            {
                separator = line.LastIndexOf(" | ", StringComparison.Ordinal);
                separatorLength = 3;
            }
            if (separator < 0)
            {
                commands.AppendLine(Colorize(line.Trim(), HeadingColor));
                descriptions.AppendLine();
                continue;
            }

            commands.AppendLine(line.Substring(0, separator).Trim());
            descriptions.AppendLine(line.Substring(separator + separatorLength).Trim());
        }

        _commandColumn.text = commands.ToString();
        _descriptionColumn.text = descriptions.ToString();

        var scroll = GetComponentInParent<ScrollRect>();
        if (scroll)
        {
            scroll.StopMovement();
            scroll.verticalNormalizedPosition = 1f;
        }
    }

    private static string Colorize(string text, string color) => "<color=" + color + ">" + text + "</color>";

    private void FindColumns()
    {
        _commandColumn = FindColumn("CommandColumn");
        _descriptionColumn = FindColumn("DescriptionColumn");
        if (!_commandColumn || !_descriptionColumn)
            throw new InvalidOperationException("Syntax requires CommandColumn and DescriptionColumn children.");
    }

    private Text FindColumn(string name) => transform.Find(name)?.GetComponent<Text>();
}
