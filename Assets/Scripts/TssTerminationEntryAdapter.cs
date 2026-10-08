using UnityEngine;

[RequireComponent(typeof(TssObjectInteractable))]
public sealed class TssTerminationEntryAdapter : MonoBehaviour
{
    [SerializeField] private TssObjectInteractable interactable;
    [SerializeField] private TssTerminationGameController controller;
    [SerializeField] private TssTerminationFirstPersonControls controlsPrefab;
    [SerializeField] private Transform controlsParent;

    private TssPortEndpoint _sourceEndpoint;
    private TssTerminationFirstPersonControls _controlsInstance;

    private void Awake()
    {
        if (!interactable)
            interactable = GetComponent<TssObjectInteractable>();
        if (!controller)
            controller = FindFirstObjectByType<TssTerminationGameController>();
    }

    private void OnEnable()
    {
        if (!interactable)
            return;

        interactable.EnteredFirstPerson += ShowControls;
        interactable.ReturnedToThirdPerson += HideControls;
        if (controller)
            controller.Closed += RestoreControls;
    }

    private void OnDisable()
    {
        if (interactable)
        {
            interactable.EnteredFirstPerson -= ShowControls;
            interactable.ReturnedToThirdPerson -= HideControls;
        }
        if (controller)
            controller.Closed -= RestoreControls;

        HideControls();
    }

    private void ShowControls()
    {
        if (_controlsInstance)
            return;
        _sourceEndpoint = FindTerminationEndpoint();
        if (!_sourceEndpoint || !controlsPrefab)
            return;

        _controlsInstance = Instantiate(controlsPrefab, controlsParent);
        _controlsInstance.Bind(interactable.ReturnToThirdPerson, OpenRearTerminationView);
    }

    private void HideControls()
    {
        if (_controlsInstance)
            Destroy(_controlsInstance.gameObject);

        _controlsInstance = null;
        _sourceEndpoint = null;
    }

    private void OpenRearTerminationView()
    {
        if (!controller || !controller.OpenRearTerminationView(_sourceEndpoint, GetComponentsInChildren<TssPortEndpoint>(true)))
            return;

        HideControls();
    }

    private void RestoreControls()
    {
        if (interactable && interactable.IsFirstPerson)
            ShowControls();
    }

    private TssPortEndpoint FindTerminationEndpoint()
    {
        var session = TssTrainingSession.Instance;
        if (!session)
            return null;

        foreach (var endpoint in GetComponentsInChildren<TssPortEndpoint>(true))
        {
            if (endpoint && session.TryGetTerminationLinks(endpoint.EndpointId, out _))
                return endpoint;
        }

        return null;
    }
}
