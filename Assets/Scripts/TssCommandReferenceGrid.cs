using System;
using UnityEngine;
using UnityEngine.UI;

public sealed class TssCommandReferenceGrid : MonoBehaviour
{
    private const string HeadingColor = "#F2B84B";
    private const string SectionColor = "#8FA9BA";
    private const string CommandColor = "#D9E6EE";
    private const string DescriptionColor = "#B6C6D0";

    public void SetReference(EquipmentCategory category)
    {
        ClearGeneratedRows();

        var hasRows = false;
        var needsSpacer = false;
        foreach (var line in TssCliCommandProcessor.BuildCommandReference(category)
                     .Split(new[] { '\n' }, StringSplitOptions.None))
        {
            if (string.IsNullOrWhiteSpace(line))
            {
                if (hasRows)
                    needsSpacer = true;
                continue;
            }

            if (needsSpacer)
            {
                CreateSpacerRow();
                needsSpacer = false;
            }

            if (line.Contains("COMMAND") && line.Contains("WHAT IT DOES"))
            {
                CreateCommandRow("COMMAND", "DESCRIPTION", HeadingColor, HeadingColor);
                hasRows = true;
                continue;
            }

            if (line.Contains("---+"))
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
                CreateSectionRow(line.Trim());
                hasRows = true;
                continue;
            }

            CreateCommandRow(
                line.Substring(0, separator).Trim(),
                line.Substring(separator + separatorLength).Trim(),
                CommandColor,
                DescriptionColor);
            hasRows = true;
        }

        var scroll = GetComponentInParent<ScrollRect>();
        if (scroll && scroll.content && scroll.viewport)
        {
            Canvas.ForceUpdateCanvases();
            scroll.StopMovement();
            scroll.verticalNormalizedPosition = 1f;
        }
    }

    private void ClearGeneratedRows()
    {
        for (var i = transform.childCount - 1; i >= 0; i--)
        {
            var child = transform.GetChild(i).gameObject;
            child.SetActive(false);
            if (Application.isPlaying)
                Destroy(child);
            else
                DestroyImmediate(child);
        }
    }

    private void CreateSectionRow(string title)
    {
        var row = CreateRow("Section");
        var text = CreateText(row.transform, title, SectionColor, 11);
        text.fontStyle = FontStyle.Bold;
        text.alignment = TextAnchor.MiddleLeft;
        SetFlexibleWidth(text, 1f);
    }

    private void CreateCommandRow(string command, string description, string commandColor, string descriptionColor)
    {
        var row = CreateRow("CommandRow");
        var commandText = CreateText(row.transform, command, commandColor, 10);
        var descriptionText = CreateText(row.transform, description, descriptionColor, 10);
        SetFlexibleWidth(commandText, 0.45f);
        SetFlexibleWidth(descriptionText, 0.55f);
    }

    private void CreateSpacerRow()
    {
        var spacer = new GameObject("Generated_Spacer", typeof(RectTransform), typeof(LayoutElement));
        spacer.transform.SetParent(transform, false);
        spacer.GetComponent<LayoutElement>().preferredHeight = 8f;
    }

    private GameObject CreateRow(string kind)
    {
        var row = new GameObject("Generated_" + kind, typeof(RectTransform), typeof(HorizontalLayoutGroup), typeof(ContentSizeFitter));
        row.transform.SetParent(transform, false);

        var layout = row.GetComponent<HorizontalLayoutGroup>();
        layout.spacing = 8f;
        layout.padding = new RectOffset(4, 4, 2, 2);
        layout.childAlignment = TextAnchor.MiddleLeft;
        layout.childControlWidth = true;
        layout.childControlHeight = true;
        layout.childForceExpandWidth = false;
        layout.childForceExpandHeight = false;

        var fitter = row.GetComponent<ContentSizeFitter>();
        fitter.horizontalFit = ContentSizeFitter.FitMode.Unconstrained;
        fitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;
        return row;
    }

    private static Text CreateText(Transform parent, string value, string color, int fontSize)
    {
        var go = new GameObject("Text", typeof(RectTransform), typeof(Text), typeof(ContentSizeFitter));
        go.transform.SetParent(parent, false);

        var text = go.GetComponent<Text>();
        text.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        text.fontSize = fontSize;
        text.color = ColorUtility.TryParseHtmlString(color, out var parsed) ? parsed : Color.white;
        text.text = value;
        text.supportRichText = true;
        text.alignment = TextAnchor.MiddleLeft;
        text.horizontalOverflow = HorizontalWrapMode.Wrap;
        text.verticalOverflow = VerticalWrapMode.Overflow;
        text.lineSpacing = 1.05f;
        text.raycastTarget = false;

        var fitter = go.GetComponent<ContentSizeFitter>();
        fitter.horizontalFit = ContentSizeFitter.FitMode.Unconstrained;
        fitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;
        return text;
    }

    private static void SetFlexibleWidth(Text text, float value)
    {
        var element = text.gameObject.AddComponent<LayoutElement>();
        element.minWidth = 0f;
        element.preferredWidth = 0f;
        element.flexibleWidth = value;
    }
}
