using System;
using StarterAssets;
using UnityEngine;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif

public sealed class TssPortSelectionAdapter : MonoBehaviour
{
    [SerializeField] private TssObjectInteractable interactable;
    [SerializeField] private Camera selectionCamera;

    private TssPortEndpoint[] _endpoints;
    private TssPortEndpoint _hoveredEndpoint;
    private bool _selecting;
    private TssTrainingSession _session;

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
        if (!_selecting)
            return;

        if (TssRuntimeUi.Instance && TssRuntimeUi.Instance.IsGameplayInputBlocked)
        {
            if (_hoveredEndpoint)
                _hoveredEndpoint.SetHovered(false);
            _hoveredEndpoint = null;
            return;
        }

        UpdateHoveredEndpoint();

        if (!WasPrimaryClickPressed())
            return;

        var endpoint = _hoveredEndpoint;
        if (!endpoint || !TssTrainingSession.Instance)
            return;

        TssTrainingSession.Instance.TryUsePort(endpoint, out var message);
        TssRuntimeUi.Instance?.ShowHudMessage(message);
        RefreshHighlights();
    }

    private void BeginSelection()
    {
        if (_session)
            _session.StateChanged -= RefreshHighlights;
        _session = TssTrainingSession.Instance;
        if (_session)
            _session.StateChanged += RefreshHighlights;
        _selecting = true;
        RefreshEndpointCache();
        RefreshHighlights();
    }

    private void EndSelection()
    {
        if (_session)
            _session.StateChanged -= RefreshHighlights;
        _session = null;
        _selecting = false;
        RefreshEndpointCache();
        var hoveredEndpoint = _hoveredEndpoint;
        _hoveredEndpoint = null;

        if (_endpoints == null)
            return;

        foreach (var endpoint in _endpoints)
        {
            if (!endpoint)
                continue;

            endpoint.SetHovered(false);
            endpoint.SetSelectable(false);
            endpoint.SetSelected(false);
        }

        if (hoveredEndpoint && System.Array.IndexOf(_endpoints, hoveredEndpoint) < 0)
            hoveredEndpoint.SetHovered(false);
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
        if (_hoveredEndpoint && (!session || !session.CanSelectEndpoint(_hoveredEndpoint)))
        {
            _hoveredEndpoint.SetHovered(false);
            _hoveredEndpoint = null;
        }
    }

    private void UpdateHoveredEndpoint()
    {
        var session = TssTrainingSession.Instance;
        var hoveredEndpoint = FindPointedEndpoint();
        if (hoveredEndpoint && (!session || !session.CanSelectEndpoint(hoveredEndpoint)))
            hoveredEndpoint = null;

        if (_hoveredEndpoint == hoveredEndpoint)
            return;

        if (_hoveredEndpoint)
            _hoveredEndpoint.SetHovered(false);

        _hoveredEndpoint = hoveredEndpoint;

        if (_hoveredEndpoint)
            _hoveredEndpoint.SetHovered(true);
    }

    private TssPortEndpoint FindPointedEndpoint()
    {
        RefreshEndpointCache();

        if (!selectionCamera)
            selectionCamera = Camera.main;
        if (!selectionCamera)
            return null;

        var ray = selectionCamera.ScreenPointToRay(GetMousePosition());
        return TssInteractionRaycast.TryGetFirstTarget(
            ray,
            collider => Array.IndexOf(_endpoints, collider.GetComponentInParent<TssPortEndpoint>()) >= 0,
            QueryTriggerInteraction.Collide,
            out var hit,
            collider => collider.GetComponentInParent<ThirdPersonController>()
                || !TssInteractionRaycast.IsStructuralOccluder(collider))
            ? hit.collider.GetComponentInParent<TssPortEndpoint>()
            : null;
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
