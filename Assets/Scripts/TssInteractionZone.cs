using StarterAssets;
using UnityEngine;

public sealed class TssInteractionZone : MonoBehaviour
{
    [SerializeField] private TssObjectInteractable[] targets;

    private void Awake()
    {
        if (targets == null || targets.Length == 0)
            targets = GetComponentsInChildren<TssObjectInteractable>(true);
    }

    private void OnTriggerEnter(Collider other)
    {
        if (!IsPlayer(other))
            return;

        SetTargetsInRange(true);
    }

    private void OnTriggerExit(Collider other)
    {
        if (!IsPlayer(other))
            return;

        SetTargetsInRange(false);
    }

    private void OnDisable()
    {
        SetTargetsInRange(false);
    }

    private void SetTargetsInRange(bool inRange)
    {
        foreach (var target in targets)
        {
            if (target)
                target.SetPlayerInRange(inRange);
        }
    }

    private static bool IsPlayer(Collider other)
    {
        return other.CompareTag("Player") || other.GetComponentInParent<ThirdPersonController>();
    }
}
