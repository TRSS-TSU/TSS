using System.Text;
using UnityEngine;
using UnityEngine.UI;

public sealed class TssResultsCard : MonoBehaviour
{
    [SerializeField] private Text title;
    [SerializeField] private Text status;
    [SerializeField] private Text detail;
    [SerializeField] private Text completedDetail;
    [SerializeField] private Text unmetDetail;

    public void Bind(TssAssessmentWorkPackage result)
    {
        if (title) title.text = TssWorkPackageTitles.Get(result.WorkPackage);
        if (status)
        {
            status.text = result.IsComplete ? "PASS" : "INCOMPLETE";
            status.color = result.IsComplete ? new Color(.35f, 1f, .55f) : new Color(1f, .65f, .25f);
        }

        var completed = new StringBuilder("VERIFIED AT EXIT\n\n");
        if (result.CompletedCriteria.Count == 0) completed.Append("None");
        else foreach (var item in result.CompletedCriteria) completed.AppendLine("[PASS] " + item);
        var unmet = new StringBuilder(result.IsComplete ? "REMAINING CRITERIA\n\nNone" : "ACTIONABLE REMAINING CRITERIA\n\n");
        if (!result.IsComplete && result.UnmetCriteria.Count > 0)
            foreach (var item in result.UnmetCriteria) unmet.AppendLine("[ ] " + item);

        if (completedDetail && unmetDetail)
        {
            completedDetail.text = completed.ToString();
            unmetDetail.text = unmet.ToString();
            if (detail) detail.gameObject.SetActive(false);
        }
        else if (detail)
        {
            detail.text = completed + "\n" + unmet;
        }
    }
}
