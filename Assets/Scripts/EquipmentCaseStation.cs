using UnityEngine;

public sealed class EquipmentCaseStation : MonoBehaviour
{
    [SerializeField] private string stationName = "Equipment Case";
    [SerializeField] private TssObjectInteractable interactable;

    public string StationName => stationName;

    private void Awake()
    {
        if (!interactable)
            interactable = GetComponent<TssObjectInteractable>();
    }

    private void OnEnable()
    {
        if (interactable)
        {
            interactable.EnteredFirstPerson += OpenPanel;
            interactable.ReturnedToThirdPerson += ClosePanel;
        }
    }

    private void OnDisable()
    {
        if (interactable)
        {
            interactable.EnteredFirstPerson -= OpenPanel;
            interactable.ReturnedToThirdPerson -= ClosePanel;
        }
    }

    public void Checkout(EquipmentDefinition equipment)
    {
        var session = TssTrainingSession.Instance;
        if (!session)
            return;

        if (session.HeldItem)
            session.ReturnHeldItem(out _);

        if (session.TryCheckout(equipment, out _))
            interactable?.ReturnToThirdPerson();
    }

    public void ReturnHeld()
    {
        TssTrainingSession.Instance?.ReturnHeldItem(out _);
    }

    public void ClosePanel()
    {
        TssRuntimeUi.Instance?.HideCasePanel(this);
    }

    public void CancelSelection()
    {
        ClosePanel();
        interactable?.ReturnToThirdPerson();
    }

    private void OpenPanel()
    {
        TssRuntimeUi.Instance?.ShowCasePanel(this);
    }
}
