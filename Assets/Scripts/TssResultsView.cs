using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

public sealed class TssResultsView : MonoBehaviour
{
    [SerializeField] private GameObject resultsContent;
    [SerializeField] private GameObject noResultsState;
    [SerializeField] private Text overallSummary;
    [SerializeField] private Text scenarioText;
    [SerializeField] private Transform workPackageContainer;
    [SerializeField] private GameObject resultCardPrefab;
    [SerializeField] private Button mainMenuButton;

    private void Awake()
    {
        if (mainMenuButton)
            mainMenuButton.onClick.AddListener(ReturnToMainMenu);
        Render(TssResultsHandoff.Snapshot);
    }

    private void Render(TssResultsSnapshot snapshot)
    {
        var hasResults = snapshot != null && snapshot.Assessment != null;
        if (resultsContent) resultsContent.SetActive(hasResults);
        if (noResultsState) noResultsState.SetActive(!hasResults);
        if (!hasResults) return;

        var assessment = snapshot.Assessment;
        if (scenarioText)
            scenarioText.text = string.IsNullOrWhiteSpace(assessment.ScenarioName) ? "Scenario unavailable" : assessment.ScenarioName;
        if (overallSummary)
        {
            overallSummary.text = assessment.IsComplete
                ? "COMPLETE — all four work packages passed at exit."
                : "INCOMPLETE — review the remaining criteria below.";
            overallSummary.color = assessment.IsComplete ? new Color(.35f, 1f, .55f) : new Color(1f, .75f, .3f);
        }

        if (!workPackageContainer || !resultCardPrefab) return;
        for (var i = workPackageContainer.childCount - 1; i >= 0; i--)
            Destroy(workPackageContainer.GetChild(i).gameObject);
        foreach (var workPackage in assessment.WorkPackages)
        {
            var card = Instantiate(resultCardPrefab, workPackageContainer).GetComponent<TssResultsCard>();
            if (card) card.Bind(workPackage);
        }
    }

    private void ReturnToMainMenu()
    {
        TssResultsHandoff.Clear();
        SceneManager.LoadScene("MainMenu");
    }
}
