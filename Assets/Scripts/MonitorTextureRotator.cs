using UnityEngine;

[ExecuteAlways]
public sealed class MonitorTextureRotator : MonoBehaviour
{
    [SerializeField] private Renderer targetRenderer;
    [SerializeField] private int materialIndex;
    [SerializeField] private Texture2D[] screens;
    [SerializeField] private float secondsPerScreen = 8f;
    [SerializeField] private bool randomStart;
    [Header("Network diagnostics")]
    [SerializeField] private Texture2D layer1Connected;
    [SerializeField] private Texture2D layer1Disconnected;
    [SerializeField] private Texture2D layer2Connected;
    [SerializeField] private Texture2D layer2Disconnected;
    [SerializeField] private Texture2D layer3Connected;
    [SerializeField] private Texture2D layer3Disconnected;

    private MaterialPropertyBlock _block;
    private int _index;
    private float _timer;
    private Texture2D _connectivityOverride;

    private void OnEnable()
    {
        Apply();
    }

    private void Awake()
    {
        if (!targetRenderer)
            targetRenderer = GetComponent<Renderer>();

        if (randomStart && screens.Length > 0)
            _index = Random.Range(0, screens.Length);

        Apply();
    }

    private void OnValidate()
    {
        if (!targetRenderer)
            targetRenderer = GetComponent<Renderer>();

        _index = Mathf.Clamp(_index, 0, Mathf.Max(0, screens.Length - 1));
        Apply();
    }

    private void Update()
    {
        if (!Application.isPlaying)
        {
            Apply();
            return;
        }

        if (_connectivityOverride || screens.Length < 2 || secondsPerScreen <= 0f)
            return;

        _timer += Time.deltaTime;
        if (_timer < secondsPerScreen)
            return;

        _timer = 0f;
        _index = (_index + 1) % screens.Length;
        Apply();
    }

    private void Apply()
    {
        var texture = _connectivityOverride ? _connectivityOverride : GetCurrentScreen();
        if (!targetRenderer || !texture)
            return;

        _block ??= new MaterialPropertyBlock();
        targetRenderer.GetPropertyBlock(_block, materialIndex);
        _block.SetTexture("_BaseMap", texture);
        _block.SetTexture("_MainTex", texture);
        _block.SetTexture("_EmissionMap", texture);
        targetRenderer.SetPropertyBlock(_block, materialIndex);
    }

    public void SetConnectivityOverride(Texture2D offlineScreen)
    {
        _connectivityOverride = offlineScreen;
        Apply();
    }

    public void ClearConnectivityOverride()
    {
        _connectivityOverride = null;
        Apply();
    }

    public void SetConnectivityScreen(Texture2D screen) => SetConnectivityOverride(screen);

    public void SetLayerStates(bool layer1, bool layer2, bool layer3)
    {
        screens = new[]
        {
            layer1 ? layer1Connected : layer1Disconnected,
            layer2 ? layer2Connected : layer2Disconnected,
            layer3 ? layer3Connected : layer3Disconnected
        };
        _index = 0;
        _timer = 0f;
        ClearConnectivityOverride();
    }

    private Texture2D GetCurrentScreen()
    {
        if (screens == null || screens.Length == 0)
            return null;

        _index = Mathf.Clamp(_index, 0, screens.Length - 1);
        return screens[_index];
    }
}
