using UnityEngine;

public sealed class TssCarryArmPose : MonoBehaviour
{
    [SerializeField] private Animator animator;
    [SerializeField] private float blendSpeed = 12f;

    private float _weight;

    private void Awake()
    {
        if (!animator)
            animator = GetComponentInChildren<Animator>();
    }

    private void OnAnimatorIK(int layerIndex)
    {
        if (!animator)
            return;

        var session = TssTrainingSession.Instance;
        var carrying = session && session.HeldItem && session.CarryVisual && animator && animator.isHuman;
        _weight = Mathf.MoveTowards(_weight, carrying ? 1f : 0f, blendSpeed * Time.deltaTime);
        animator.SetIKPositionWeight(AvatarIKGoal.LeftHand, _weight);
        animator.SetIKPositionWeight(AvatarIKGoal.RightHand, _weight);
        animator.SetIKRotationWeight(AvatarIKGoal.LeftHand, _weight);
        animator.SetIKRotationWeight(AvatarIKGoal.RightHand, _weight);

        if (_weight <= 0f || !carrying || !TryGetCarryBounds(session.CarryVisual, out var bounds))
            return;

        var visual = session.CarryVisual.transform;
        var center = bounds.center;
        var handOffset = session.HeldItem.carryHandSurfaceOffset;
        var handPositionOffset = session.HeldItem.carryHandPositionOffset;
        animator.SetIKRotation(AvatarIKGoal.LeftHand, visual.rotation * Quaternion.Euler(session.HeldItem.carryLeftHandRotationOffset));
        animator.SetIKRotation(AvatarIKGoal.RightHand, visual.rotation * Quaternion.Euler(session.HeldItem.carryRightHandRotationOffset));
        var leftX = bounds.min.x - handOffset;
        var rightX = bounds.max.x + handOffset;
        if (session.HeldItem.swapCarryHandSides)
            (leftX, rightX) = (rightX, leftX);
        animator.SetIKPosition(AvatarIKGoal.LeftHand, visual.TransformPoint(new Vector3(leftX, center.y, center.z) + handPositionOffset));
        animator.SetIKPosition(AvatarIKGoal.RightHand, visual.TransformPoint(new Vector3(rightX, center.y, center.z) + handPositionOffset));
    }

    private static bool TryGetCarryBounds(GameObject carryVisual, out Bounds bounds)
    {
        var root = carryVisual.transform;
        var hasBounds = false;
        bounds = default;

        foreach (var renderer in carryVisual.GetComponentsInChildren<Renderer>())
        {
            if (!renderer.enabled)
                continue;

            var local = renderer.localBounds;
            for (var x = -1; x <= 1; x += 2)
            for (var y = -1; y <= 1; y += 2)
            for (var z = -1; z <= 1; z += 2)
            {
                var rendererPoint = local.center + Vector3.Scale(local.extents, new Vector3(x, y, z));
                var rootPoint = root.InverseTransformPoint(renderer.transform.TransformPoint(rendererPoint));
                if (hasBounds)
                    bounds.Encapsulate(rootPoint);
                else
                {
                    bounds = new Bounds(rootPoint, Vector3.zero);
                    hasBounds = true;
                }
            }
        }

        return hasBounds;
    }
}
