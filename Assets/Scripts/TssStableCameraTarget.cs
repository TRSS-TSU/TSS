using UnityEngine;

public sealed class TssStableCameraTarget : MonoBehaviour
{
    [SerializeField] private Transform subject;

    private Vector3 _subjectLocalOffset;

    private void Awake()
    {
        if (!subject)
            subject = transform.parent;

        if (!subject)
            return;

        _subjectLocalOffset = transform.localPosition;
        transform.SetParent(null, true);
    }

    private void LateUpdate()
    {
        if (subject)
            transform.position = subject.TransformPoint(_subjectLocalOffset);
    }
}
