using UnityEngine;

[DisallowMultipleComponent]
public sealed class RackCableRouteProfile : MonoBehaviour
{
    [SerializeField] private string rackId;
    [SerializeField] private float leftDuctLocalX;
    [SerializeField] private float rightDuctLocalX;
    [SerializeField] private float frontFaceLocalZ;
    [SerializeField] private float rearFaceLocalZ;
    [SerializeField] private float trayLocalY;

    public string RackId => rackId;
    public float LeftDuctLocalX => leftDuctLocalX;
    public float RightDuctLocalX => rightDuctLocalX;
    public float FrontFaceLocalZ => frontFaceLocalZ;
    public float RearFaceLocalZ => rearFaceLocalZ;
    public float TrayLocalY => trayLocalY;

    public Transform RouteSpace
    {
        get
        {
            var rack = GetComponent<RackMountController>();
            return rack ? rack.MountRoot : transform;
        }
    }

    public bool IsValid => !string.IsNullOrWhiteSpace(rackId)
        && float.IsFinite(leftDuctLocalX)
        && float.IsFinite(rightDuctLocalX)
        && float.IsFinite(frontFaceLocalZ)
        && float.IsFinite(rearFaceLocalZ)
        && float.IsFinite(trayLocalY)
        && Mathf.Abs(leftDuctLocalX - rightDuctLocalX) > 0.0001f;

    public void Configure(string valueRackId, float leftDuctX, float rightDuctX, float frontFaceZ, float rearFaceZ, float trayY)
    {
        rackId = valueRackId?.Trim() ?? string.Empty;
        leftDuctLocalX = leftDuctX;
        rightDuctLocalX = rightDuctX;
        frontFaceLocalZ = frontFaceZ;
        rearFaceLocalZ = rearFaceZ;
        trayLocalY = trayY;
    }
}
