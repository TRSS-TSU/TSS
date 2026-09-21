using UnityEngine;

[ExecuteAlways]
public sealed class MonitorTextureRotator : MonoBehaviour
{
    [SerializeField] private Renderer targetRenderer;
    [SerializeField] private int materialIndex;
    [SerializeField] private Texture2D[] screens;
    [SerializeField] private float secondsPerScreen = 8f;
    [SerializeField] private bool randomStart;

    private MaterialPropertyBlock _block;
    private int _index;
    private float _timer;

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

        if (screens.Length < 2 || secondsPerScreen <= 0f)
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
        if (!targetRenderer || screens.Length == 0 || !screens[_index])
            return;

        _block ??= new MaterialPropertyBlock();
        targetRenderer.GetPropertyBlock(_block, materialIndex);
        _block.SetTexture("_BaseMap", screens[_index]);
        _block.SetTexture("_MainTex", screens[_index]);
        _block.SetTexture("_EmissionMap", screens[_index]);
        targetRenderer.SetPropertyBlock(_block, materialIndex);
    }
}
