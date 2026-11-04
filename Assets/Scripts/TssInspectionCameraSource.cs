using UnityEngine;

public sealed class TssInspectionCameraSource : MonoBehaviour
{
    [SerializeField] private Camera inspectionCamera;

    public RenderTexture PreviewTexture => inspectionCamera ? inspectionCamera.targetTexture : null;

    private void Awake()
    {
        SetPreviewActive(false);
    }

    private void OnDisable()
    {
        SetPreviewActive(false);
    }

    public void SetPreviewActive(bool active)
    {
        if (inspectionCamera)
            inspectionCamera.enabled = active;
    }
}
