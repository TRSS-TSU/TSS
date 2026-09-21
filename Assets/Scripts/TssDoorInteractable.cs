using UnityEngine;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif

public sealed class TssDoorInteractable : MonoBehaviour
{
    [SerializeField] private Transform doorPanel;
    [SerializeField] private float openAngle = 95f;
    [SerializeField] private float openSpeed = 8f;

    private bool _isOpen;
    private Quaternion _closedRotation;
    private float _targetOpenAngle;

    private void Awake()
    {
        if (doorPanel == null && transform.childCount > 0)
            doorPanel = transform.GetChild(0);

        _targetOpenAngle = openAngle;

        if (doorPanel != null)
            _closedRotation = doorPanel.localRotation;
    }

    private void Update()
    {
        if (doorPanel == null)
            return;

        var target = _closedRotation * Quaternion.Euler(0f, _isOpen ? _targetOpenAngle : 0f, 0f);
        doorPanel.localRotation = Quaternion.Slerp(doorPanel.localRotation, target, Time.deltaTime * openSpeed);
    }

    private void OnMouseDown()
    {
        Toggle(Camera.main ? Camera.main.transform : null);
    }

    private void OnTriggerStay(Collider other)
    {
        if (!other.CompareTag("Player"))
            return;

#if ENABLE_INPUT_SYSTEM
        if (Keyboard.current != null && Keyboard.current.eKey.wasPressedThisFrame)
            Toggle(GetInteractor(other));
#else
        if (Input.GetKeyDown(KeyCode.E))
            Toggle(GetInteractor(other));
#endif
    }

    private void Toggle(Transform interactor)
    {
        if (!_isOpen)
            _targetOpenAngle = GetOpenAngleAwayFrom(interactor);

        _isOpen = !_isOpen;
    }

    private float GetOpenAngleAwayFrom(Transform interactor)
    {
        if (!interactor)
            return openAngle;

        var angle = Mathf.Abs(openAngle);
        var interactorLocalZ = transform.InverseTransformPoint(interactor.position).z;
        return interactorLocalZ > 0f ? angle : -angle;
    }

    private static Transform GetInteractor(Collider other)
    {
        return other.attachedRigidbody ? other.attachedRigidbody.transform : other.transform;
    }
}
