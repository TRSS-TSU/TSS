using System;
using System.Text;
using UnityEngine;
using UnityEngine.UI;

public sealed class TssCommandReferenceGrid : MonoBehaviour
{
    private Text _commandColumn;
    private Text _descriptionColumn;

    public void SetReference(EquipmentCategory category)
    {
        FindColumns();
        var commands = new StringBuilder("COMMAND\n");
        var descriptions = new StringBuilder("WHAT IT DOES\n");

        foreach (var line in TssCliCommandProcessor.BuildCommandReference(category).Split(new[] { '\n' }, StringSplitOptions.RemoveEmptyEntries))
        {
            if (line.Contains("---+") || line.Contains("COMMAND") && line.Contains("WHAT IT DOES"))
                continue;

            var separator = line.IndexOf('\t');
            var separatorLength = 1;
            if (separator < 0)
            {
                separator = line.LastIndexOf(" | ", StringComparison.Ordinal);
                separatorLength = 3;
            }
            if (separator < 0)
            {
                commands.AppendLine(line.Trim());
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

    private void FindColumns()
    {
        _commandColumn = FindColumn("CommandColumn");
        _descriptionColumn = FindColumn("DescriptionColumn");
        if (!_commandColumn || !_descriptionColumn)
            throw new InvalidOperationException("Syntax requires CommandColumn and DescriptionColumn children.");
    }

    private Text FindColumn(string name) => transform.Find(name)?.GetComponent<Text>();
}
