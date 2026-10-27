using System.Text;
using UnityEngine;
using UnityEngine.UI;

public sealed class TssResultsCard : MonoBehaviour
{
    [SerializeField] private Text title;
    [SerializeField] private Text status;
    [SerializeField] private Text detail;

    public void Bind(TssAssessmentWorkPackage result)
    {
        if (title) title.text = TssWorkPackageTitles.Get(result.WorkPackage);
        if (status)
        {
            status.text = result.IsComplete ? "PASS" : "INCOMPLETE";
            status.color = result.IsComplete ? new Color(.35f, 1f, .55f) : new Color(1f, .65f, .25f);
        }

        if (!detail) return;
        var builder = new StringBuilder();
        builder.AppendLine("Verified at exit:");
        if (result.CompletedCriteria.Count == 0)
            builder.AppendLine("  None");
        else
            foreach (var item in result.CompletedCriteria)
                builder.AppendLine("  [PASS] " + item);

        builder.AppendLine();
        builder.AppendLine(result.IsComplete ? "No remaining criteria." : "Actionable remaining criteria:");
        if (result.UnmetCriteria.Count == 0)
            builder.AppendLine("  None");
        else
            foreach (var item in result.UnmetCriteria)
                builder.AppendLine("  [ ] " + item);
        detail.text = builder.ToString();
    }
}
