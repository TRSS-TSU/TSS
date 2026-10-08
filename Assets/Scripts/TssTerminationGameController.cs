using System;
using UnityEngine;

public sealed class TssTerminationGameController : MonoBehaviour
{
    [SerializeField] private TssWallportTerminationView wallportPrefab;
    [SerializeField] private TssWallportTerminationView patchPanelPrefab;

    private TssWallportTerminationView _procedureInstance;
    private bool _isOpen;
    public event Action Closed;

    public bool OpenRearTerminationView(TssPortEndpoint sourceEndpoint, TssPortEndpoint[] availablePorts)
    {
        if (_isOpen || !sourceEndpoint || !TssTrainingSession.Instance)
            return false;

        var patchPanel = sourceEndpoint.EndpointId.StartsWith("PatchPanel:", StringComparison.Ordinal);
        var prefab = patchPanel ? patchPanelPrefab : wallportPrefab;
        if (!prefab)
        {
            Debug.LogWarning("TssTerminationGameController needs a rear termination prefab for this endpoint.");
            return false;
        }

        if (!TssTrainingSession.Instance.TryGetTerminationLinks(sourceEndpoint.EndpointId, out _))
            return false;

        _procedureInstance = Instantiate(prefab);
        _procedureInstance.Bind(sourceEndpoint, availablePorts, CloseProcedure);
        _isOpen = true;
        TssRuntimeUi.Instance?.PushExternalGameplayLock(this);
        return true;
    }

    public void CloseProcedure()
    {
        if (!_isOpen)
            return;

        _isOpen = false;
        if (_procedureInstance)
            Destroy(_procedureInstance.gameObject);
        _procedureInstance = null;
        TssRuntimeUi.Instance?.PopExternalGameplayLock(this);
        Closed?.Invoke();
    }

    private void OnDisable()
    {
        CloseProcedure();
    }

}
