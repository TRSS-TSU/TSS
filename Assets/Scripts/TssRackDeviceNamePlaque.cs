using TMPro;
using UnityEngine;

public sealed class TssRackDeviceNamePlaque : MonoBehaviour
{
    [SerializeField] private Transform plaqueRoot;
    [SerializeField] private TextMeshPro plaqueText;

    private TssTrainingSession _session;
    private string _deviceKey;
    private string _fallbackName;

    private void Awake()
    {
        ResolveText();
    }

    private void OnEnable()
    {
        SubscribeSession();
    }

    private void Start()
    {
        SubscribeSession();
    }

    private void OnDisable()
    {
        if (_session)
            _session.StateChanged -= RefreshCurrentName;
        _session = null;
    }

    public void Configure(string deviceKey, string fallbackName)
    {
        _deviceKey = deviceKey;
        _fallbackName = fallbackName;
        ResolveText();
        SubscribeSession();
        RefreshCurrentName();
    }

    public static TssRackDeviceNamePlaque Ensure(GameObject deviceRoot)
    {
        if (!deviceRoot)
            return null;

        var plaque = deviceRoot.GetComponent<TssRackDeviceNamePlaque>()
            ?? deviceRoot.GetComponentInChildren<TssRackDeviceNamePlaque>(true);
        if (!plaque)
            plaque = deviceRoot.AddComponent<TssRackDeviceNamePlaque>();

        plaque.ResolveText();

        return plaque;
    }

    private void SubscribeSession()
    {
        if (_session)
            return;

        _session = TssTrainingSession.Instance;
        if (!_session)
            return;

        _session.StateChanged += RefreshCurrentName;
        RefreshCurrentName();
    }

    private void ResolveText()
    {
        if (!plaqueRoot)
            plaqueRoot = transform.Find("Device Name Plaque");
        if (!plaqueRoot && transform.name == "Device Name Plaque")
            plaqueRoot = transform;
        if (!plaqueText && plaqueRoot)
            plaqueText = plaqueRoot.GetComponentInChildren<TextMeshPro>(true);
    }

    private void CreateRuntimePlaque()
    {
        var hasBounds = false;
        var bounds = new Bounds(transform.position, Vector3.zero);
        foreach (var renderer in GetComponentsInChildren<Renderer>(true))
        {
            if (!hasBounds)
            {
                bounds = renderer.bounds;
                hasBounds = true;
            }
            else
            {
                bounds.Encapsulate(renderer.bounds);
            }
        }

        if (!hasBounds)
            return;

        var scale = transform.lossyScale;
        var width = Mathf.Clamp(bounds.size.x * 0.78f, 0.18f, 0.38f)
            / Mathf.Max(Mathf.Abs(scale.x), 0.001f);
        var height = Mathf.Clamp(bounds.size.y * 0.65f, 0.022f, 0.06f)
            / Mathf.Max(Mathf.Abs(scale.y), 0.001f);
        var depth = 0.012f / Mathf.Max(Mathf.Abs(scale.z), 0.001f);
        var plaque = GameObject.CreatePrimitive(PrimitiveType.Cube);
        plaque.name = "Device Name Plaque";
        plaque.transform.SetParent(transform, false);
        plaque.transform.localPosition = new Vector3(0f, transform.InverseTransformPoint(bounds.center).y, transform.InverseTransformPoint(bounds.min).z - depth * 0.5f);
        plaque.transform.localScale = new Vector3(width, height, depth);
        if (plaque.TryGetComponent<Collider>(out var collider))
        {
            if (Application.isPlaying)
                Destroy(collider);
            else
                DestroyImmediate(collider);
        }
        if (plaque.TryGetComponent<Renderer>(out var plaqueRenderer))
        {
            if (Application.isPlaying)
                plaqueRenderer.material.color = new Color(0.05f, 0.06f, 0.07f, 1f);
        }

        var textObject = new GameObject("Device Name Text", typeof(RectTransform));
        textObject.transform.SetParent(plaque.transform, false);
        var text = textObject.AddComponent<TextMeshPro>();
        text.alignment = TextAlignmentOptions.Center;
        text.fontSize = 0.12f;
        text.color = new Color(0.95f, 0.75f, 0.25f, 1f);
        text.textWrappingMode = TextWrappingModes.NoWrap;
        text.overflowMode = TextOverflowModes.Ellipsis;
        text.rectTransform.sizeDelta = new Vector2(0.92f, 0.8f);
        text.transform.localPosition = new Vector3(0f, 0f, -0.5f - 0.001f);
        plaqueRoot = plaque.transform;
        plaqueText = text;
    }

    public void RefreshCurrentName()
    {
        if (!plaqueText)
            return;

        var deviceName = _fallbackName;
        var session = _session ? _session : TssTrainingSession.Instance;
        var network = session ? session.NetworkState : null;
        if (network != null && !string.IsNullOrWhiteSpace(_deviceKey)
            && network.TryGetDevice(_deviceKey, out var device)
            && !string.IsNullOrWhiteSpace(device.Hostname))
        {
            deviceName = device.Hostname;
        }
        else if (session)
        {
            var installedMatch = false;
            foreach (var record in session.Installed)
            {
                if (!TssTrainingSession.GetRackOwnerPrefix(record.RackId, record.StartingU)
                    .Equals(_deviceKey, System.StringComparison.OrdinalIgnoreCase))
                    continue;

                if (!string.IsNullOrWhiteSpace(record.DeviceName))
                    deviceName = record.DeviceName;
                installedMatch = true;
                break;
            }

            if (!installedMatch)
            {
                foreach (var record in session.EndpointPlacements)
                {
                    if (!TssTrainingSession.GetEndpointOwnerPrefix(record.PlacementId)
                        .Equals(_deviceKey, System.StringComparison.OrdinalIgnoreCase))
                        continue;

                    deviceName = record.DeviceName;
                    break;
                }
            }
        }

        plaqueText.text = string.IsNullOrWhiteSpace(deviceName) ? "UNNAMED" : deviceName.Trim();
    }
}
// runtime rack plaque state binding








