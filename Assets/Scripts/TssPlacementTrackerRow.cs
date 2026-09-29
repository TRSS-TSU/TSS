using UnityEngine;
using UnityEngine.UI;

public sealed class TssPlacementTrackerRow : MonoBehaviour
{
    [SerializeField] private Text nameText;
    [SerializeField] private Text detailText;

    private void Awake()
    {
        if (!nameText)
            nameText = transform.Find("NameText")?.GetComponent<Text>();
        if (!detailText)
            detailText = transform.Find("DetailText")?.GetComponent<Text>();
    }

    public void SetStatus(TssPlacementRequirementStatus status, Color color)
    {
        if (nameText)
        {
            nameText.text = status.RequiredName;
            nameText.color = color;
        }

        if (detailText)
        {
            detailText.text = $"{status.Location}  {status.Category}";
            detailText.color = color;
        }
    }
}
