using UnityEngine;

public sealed class TssCarryArmPose : MonoBehaviour
{
    [SerializeField] private Animator animator;
    [SerializeField] private float handSidePadding = 0.03f;
    [SerializeField] private float handHeightOffset = 0.02f;
    [SerializeField] private float handForwardOffset = -0.02f;
    [SerializeField] private float elbowSideOffset = 0.34f;
    [SerializeField] private float elbowForwardOffset = 0.12f;
    [SerializeField] private float elbowHeightOffset = -0.08f;
    [SerializeField] private float blendSpeed = 12f;

    private float _weight;

    private void Awake()
    {
        if (!animator)
            animator = GetComponentInChildren<Animator>();
    }

    private void OnAnimatorIK(int layerIndex)
    {
        var session = TssTrainingSession.Instance;
        var carrying = session && session.HeldItem && session.CarryVisual && animator && animator.isHuman;
        _weight = Mathf.MoveTowards(_weight, carrying ? 1f : 0f, blendSpeed * Time.deltaTime);

        SetIkWeight(AvatarIKGoal.LeftHand, AvatarIKHint.LeftElbow, _weight);
        SetIkWeight(AvatarIKGoal.RightHand, AvatarIKHint.RightElbow, _weight);

        if (_weight <= 0f || !carrying || !TryGetCarryBounds(session.CarryVisual, out var bounds))
            return;

        var root = animator.transform;
        var side = Mathf.Max(Vector3.Dot(bounds.extents, Abs(root.right)), 0.08f) + handSidePadding;
        var handCenter = bounds.center + root.up * handHeightOffset + root.forward * handForwardOffset;
        var leftHand = handCenter - root.right * side;
        var rightHand = handCenter + root.right * side;

        animator.SetIKPosition(AvatarIKGoal.LeftHand, leftHand);
        animator.SetIKPosition(AvatarIKGoal.RightHand, rightHand);
        animator.SetIKHintPosition(AvatarIKHint.LeftElbow, leftHand - root.right * elbowSideOffset + root.forward * elbowForwardOffset + root.up * elbowHeightOffset);
        animator.SetIKHintPosition(AvatarIKHint.RightElbow, rightHand + root.right * elbowSideOffset + root.forward * elbowForwardOffset + root.up * elbowHeightOffset);
    }

    private void SetIkWeight(AvatarIKGoal hand, AvatarIKHint elbow, float weight)
    {
        if (!animator)
            return;

        animator.SetIKPositionWeight(hand, weight);
        animator.SetIKRotationWeight(hand, 0f);
        animator.SetIKHintPositionWeight(elbow, weight);
    }

    private static bool TryGetCarryBounds(GameObject carryVisual, out Bounds bounds)
    {
        var renderers = carryVisual.GetComponentsInChildren<Renderer>(true);
        if (renderers.Length == 0)
        {
            bounds = default;
            return false;
        }

        bounds = renderers[0].bounds;
        for (var i = 1; i < renderers.Length; i++)
            bounds.Encapsulate(renderers[i].bounds);

        return true;
    }

    private static Vector3 Abs(Vector3 value)
    {
        return new Vector3(Mathf.Abs(value.x), Mathf.Abs(value.y), Mathf.Abs(value.z));
    }
}
