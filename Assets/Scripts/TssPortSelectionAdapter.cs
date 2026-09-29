using System;
using UnityEngine;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif

public sealed class TssPortSelectionAdapter : MonoBehaviour
{
    [SerializeField] private TssObjectInteractable interactable;
    [SerializeField] private Camera selectionCamera;

    private TssPortEndpoint[] _endpoints;
    private bool _selecting;

    private void Awake()
    {
        if (!interactable)
            interactable = GetComponent<TssObjectInteractable>();
        if (!selectionCamera)
            selectionCamera = Camera.main;

        RefreshEndpointCache();
    }

    private void OnEnable()
    {
        if (!interactable)
            return;

        interactable.EnteredFirstPerson += BeginSelection;
        interactable.ReturnedToThirdPerson += EndSelection;
    }

    private void OnDisable()
    {
        if (interactable)
        {
            interactable.EnteredFirstPerson -= BeginSelection;
            interactable.ReturnedToThirdPerson -= EndSelection;
        }

        EndSelection();
    }

    private void Update()
    {
        if (!_selecting || !WasPrimaryClickPressed())
            return;

        var endpoint = FindClickedEndpoint();
        if (!endpoint || !TssTrainingSession.Instance)
            return;

        TssTrainingSession.Instance.TryUsePort(endpoint, out _);
        RefreshHighlights();
    }

    private void BeginSelection()
    {
        _selecting = true;
        RefreshEndpointCache();
        RefreshHighlights();
    }

    private void EndSelection()
    {
        _selecting = false;
        RefreshEndpointCache();
        if (_endpoints == null)
            return;

        foreach (var endpoint in _endpoints)
        {
            if (!endpoint)
                continue;

            endpoint.SetSelectable(false);
            endpoint.SetSelected(false);
        }
    }

    private void RefreshHighlights()
    {
        RefreshEndpointCache();

        var session = TssTrainingSession.Instance;
        foreach (var endpoint in _endpoints)
        {
            if (!endpoint)
                continue;

            endpoint.SetSelectable(session && session.CanSelectEndpoint(endpoint));
            endpoint.SetSelected(session && session.IsFirstSelectedEndpoint(endpoint));
        }
    }

    private TssPortEndpoint FindClickedEndpoint()
    {
        RefreshEndpointCache();

        if (!selectionCamera)
            selectionCamera = Camera.main;
        if (!selectionCamera)
            return null;

        var ray = selectionCamera.ScreenPointToRay(GetMousePosition());
        var hits = Physics.RaycastAll(ray, Mathf.Infinity, ~0, QueryTriggerInteraction.Collide);
        Array.Sort(hits, (left, right) => left.distance.CompareTo(right.distance));

        foreach (var hit in hits)
        {
            var endpoint = hit.collider ? hit.collider.GetComponentInParent<TssPortEndpoint>() : null;
            if (endpoint && Array.IndexOf(_endpoints, endpoint) >= 0)
                return endpoint;
        }

        return null;
    }

    private void RefreshEndpointCache()
    {
        _endpoints = GetComponentsInChildren<TssPortEndpoint>(true);
    }

    private static Vector3 GetMousePosition()
    {
#if ENABLE_INPUT_SYSTEM
        return Mouse.current != null ? (Vector3)Mouse.current.position.ReadValue() : Vector3.zero;
#else
        return Input.mousePosition;
#endif
    }

    private static bool WasPrimaryClickPressed()
    {
#if ENABLE_INPUT_SYSTEM
        return Mouse.current != null && Mouse.current.leftButton.wasPressedThisFrame;
#else
        return Input.GetMouseButtonDown(0);
#endif
    }
}
