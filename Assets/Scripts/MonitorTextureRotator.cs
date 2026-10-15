using UnityEngine;

[ExecuteAlways]
public sealed class MonitorTextureRotator : MonoBehaviour
{
    private static Material _glassMaterial;

    [SerializeField] private Renderer targetRenderer;
    [SerializeField] private int materialIndex;
    [SerializeField] private Vector4 textureScaleOffset = new(1f, 1f, 0f, 0f);
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
    [SerializeField] private Texture2D layer4Connected;
    [SerializeField] private Texture2D layer4Disconnected;

    private MaterialPropertyBlock _block;
    private bool _originalTargetRendererEnabled;
    private bool _hasOriginalRendererState;
    private Mesh _uvMesh;
    private Vector4 _textureScaleOffset = new(1f, 1f, 0f, 0f);
    private bool _hasTextureScaleOffset;
    private int _index;
    private float _timer;
    private Texture2D _connectivityOverride;
    private readonly Renderer[] _layerWindowRenderers = new Renderer[3];
    private Renderer _glassRenderer;
    private Material _windowMaterial;
    private Material _powerOffMaterial;
    private Material _statusMaterial;
    private bool _usingLayerWindows;
    private bool _logicalStateLocked;
    private TssPortEndpoint _logicalEndpoint;
    private TssTrainingSession _logicalSession;

    private void OnEnable()
    {
        Apply();
    }

    private void OnDisable()
    {
        if (_logicalSession)
            _logicalSession.StateChanged -= RefreshLogicalLayerState;
        _logicalSession = null;
        ResetLayerWindows();
    }

    private void Awake()
    {
        if (!targetRenderer)
            targetRenderer = GetComponent<Renderer>();

        if (randomStart && screens != null && screens.Length > 0)
            _index = Random.Range(0, screens.Length);

        Apply();
    }

    private void OnValidate()
    {
        if (!targetRenderer)
            targetRenderer = GetComponent<Renderer>();

        _index = Mathf.Clamp(_index, 0, Mathf.Max(0, (screens?.Length ?? 0) - 1));
        Apply();
    }

    private void Update()
    {
        if (_usingLayerWindows || _logicalStateLocked)
            return;

        if (!Application.isPlaying)
        {
            Apply();
            return;
        }

        if (_connectivityOverride || screens == null || screens.Length < 2 || secondsPerScreen <= 0f)
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
        if (_usingLayerWindows)
        {
            ApplyLayerWindows();
            return;
        }

        var texture = _connectivityOverride ? _connectivityOverride : GetCurrentScreen();
        if (!targetRenderer || !texture)
            return;

        _block ??= new MaterialPropertyBlock();
        targetRenderer.GetPropertyBlock(_block, materialIndex);
        var textureScaleOffset = GetTextureScaleOffset(texture);
        _block.SetTexture("_BaseMap", texture);
        _block.SetTexture("_MainTex", texture);
        _block.SetTexture("_EmissionMap", texture);
        _block.SetColor("_BaseColor", Color.white);
        _block.SetColor("_Color", Color.white);
        _block.SetVector("_BaseMap_ST", textureScaleOffset);
        _block.SetVector("_MainTex_ST", textureScaleOffset);
        _block.SetVector("_EmissionMap_ST", textureScaleOffset);
        targetRenderer.SetPropertyBlock(_block, materialIndex);
    }

    private Vector4 GetTextureScaleOffset(Texture texture)
    {
        var mesh = targetRenderer ? targetRenderer.GetComponent<MeshFilter>()?.sharedMesh : null;
        if (!mesh || !mesh.isReadable)
            return textureScaleOffset;
        if (_hasTextureScaleOffset && _uvMesh == mesh)
            return _textureScaleOffset;

        _uvMesh = mesh;
        _hasTextureScaleOffset = true;
        var uv = mesh.uv;
        if (uv == null || uv.Length == 0)
            return _textureScaleOffset;

        var min = new Vector2(float.MaxValue, float.MaxValue);
        var max = new Vector2(float.MinValue, float.MinValue);
        foreach (var point in uv)
        {
            min = Vector2.Min(min, point);
            max = Vector2.Max(max, point);
        }

        var size = max - min;
        if (size.x <= 0f || size.y <= 0f)
            return textureScaleOffset;

        var scale = new Vector2(1f / size.x, 1f / size.y);
        var offset = new Vector2(-min.x * scale.x, -min.y * scale.y);
        if (texture && texture.width > 0 && texture.height > 0)
        {
            var uvAspect = size.x / size.y;
            var textureAspect = texture.width / (float)texture.height;
            var horizontalCorrection = uvAspect / textureAspect;
            scale.x *= horizontalCorrection;
            offset.x = offset.x * horizontalCorrection + 0.5f * (1f - horizontalCorrection);
        }

        return _textureScaleOffset = textureScaleOffset = new Vector4(scale.x, scale.y, offset.x, offset.y);
    }

    public void SetConnectivityOverride(Texture2D offlineScreen)
    {
        ResetLayerWindows();
        _logicalStateLocked = false;
        _connectivityOverride = offlineScreen;
        Apply();
    }

    public void ClearConnectivityOverride()
    {
        _logicalStateLocked = false;
        _connectivityOverride = null;
        Apply();
    }

    public void SetPowerOff()
    {
        ResetLayerWindows();
        _connectivityOverride = null;
        RestorePowerOffMaterial();
        if (targetRenderer)
            targetRenderer.SetPropertyBlock(null, materialIndex);
    }

    public void SetConnectivityScreen(Texture2D screen) => SetConnectivityOverride(screen);

    public void SetLayerState(int layer, bool connected)
    {
        var texture = GetLayerTexture(layer, connected);
        if (!texture)
            return;

        UseStatusMaterial();
        screens = new[] { texture };
        _index = 0;
        _timer = 0f;
        _logicalStateLocked = true;
        _connectivityOverride = null;
        ResetLayerWindows();
        Apply();
    }

    public void SetLayerStates(bool layer1, bool layer2, bool layer3)
    {
        if (!HasCompleteLayerTextureSet())
        {
            ResetLayerWindows();
            ClearConnectivityOverride();
            return;
        }

        UseStatusMaterial();
        screens = new[]
        {
            layer1 ? layer1Connected : layer1Disconnected,
            layer2 ? layer2Connected : layer2Disconnected,
            layer3 ? layer3Connected : layer3Disconnected
        };
        _index = 0;
        _timer = 0f;
        _logicalStateLocked = false;
        _connectivityOverride = null;

        ResetLayerWindows();
        Apply();
    }

    public void SetLogicalLayerStates(bool layer1, bool layer2, bool layer3)
    {
        if (!layer1)
            SetLayerState(1, false);
        else if (!layer2)
            SetLayerState(2, true);
        else
            SetLayerState(3, true);
    }

    public void MonitorLogicalLayerChanges(TssPortEndpoint endpoint)
    {
        if (_logicalSession)
            _logicalSession.StateChanged -= RefreshLogicalLayerState;

        _logicalEndpoint = endpoint;
        _logicalSession = TssTrainingSession.Instance;
        if (_logicalSession)
            _logicalSession.StateChanged += RefreshLogicalLayerState;
        RefreshLogicalLayerState();
    }

    private void RefreshLogicalLayerState()
    {
        if (!_logicalEndpoint || !_logicalSession || _logicalSession.NetworkState == null)
            return;

        var network = _logicalSession.NetworkState;
        var connected = _logicalSession.IsEndpointOnEffectivePhysicalPath(_logicalEndpoint.EndpointId);
        var layer1 = connected && network.IsEndpointLogicalLayer1Available(_logicalSession, _logicalEndpoint.EndpointId);
        var layer2 = layer1 && network.IsEndpointLogicalLayer2Available(_logicalSession, _logicalEndpoint.EndpointId);
        SetLogicalLayerStates(layer1, layer2, layer2);
    }

    private void UseStatusMaterial()
    {
        if (!targetRenderer)
            return;

        var materials = targetRenderer.sharedMaterials;
        if (materialIndex < 0 || materialIndex >= materials.Length || !materials[materialIndex])
            return;

        _powerOffMaterial ??= materials[materialIndex];
        if (!_statusMaterial)
        {
            _statusMaterial = new Material(_powerOffMaterial)
            {
                name = "Monitor Status Runtime",
                hideFlags = HideFlags.HideAndDontSave,
                renderQueue = (int)UnityEngine.Rendering.RenderQueue.Geometry
            };
            _statusMaterial.SetFloat("_Surface", 0f);
            _statusMaterial.SetFloat("_SrcBlend", (float)UnityEngine.Rendering.BlendMode.One);
            _statusMaterial.SetFloat("_DstBlend", (float)UnityEngine.Rendering.BlendMode.Zero);
            _statusMaterial.SetFloat("_ZWrite", 1f);
            _statusMaterial.SetColor("_BaseColor", Color.white);
            _statusMaterial.SetColor("_Color", Color.white);
            _statusMaterial.DisableKeyword("_SURFACE_TYPE_TRANSPARENT");
            _statusMaterial.EnableKeyword("_SURFACE_TYPE_OPAQUE");
            _statusMaterial.SetOverrideTag("RenderType", "Opaque");
        }

        materials[materialIndex] = _statusMaterial;
        targetRenderer.sharedMaterials = materials;
    }

    private void RestorePowerOffMaterial()
    {
        if (!targetRenderer || !_powerOffMaterial)
            return;

        var materials = targetRenderer.sharedMaterials;
        if (materialIndex < 0 || materialIndex >= materials.Length)
            return;

        materials[materialIndex] = _powerOffMaterial;
        targetRenderer.sharedMaterials = materials;
    }

    private bool EnsureLayerWindows()
    {
        if (_layerWindowRenderers[0] && _layerWindowRenderers[1] && _layerWindowRenderers[2] && _glassRenderer)
            return true;

        DestroyLayerWindowObjects();
        if (_windowMaterial)
        {
            if (Application.isPlaying)
                Destroy(_windowMaterial);
            else
                DestroyImmediate(_windowMaterial);
            _windowMaterial = null;
        }

        var meshFilter = targetRenderer ? targetRenderer.GetComponent<MeshFilter>() : null;
        var mesh = meshFilter ? meshFilter.sharedMesh : null;
        if (!mesh)
            return false;

        var size = mesh.bounds.size;
        var axes = new[] { 0, 1, 2 };
        System.Array.Sort(axes, (a, b) => GetAxis(size, b).CompareTo(GetAxis(size, a)));
        var widthAxis = axes[0];
        var heightAxis = axes[1];
        if (GetAxis(size, widthAxis) <= 0f || GetAxis(size, heightAxis) <= 0f)
            return false;

        var meshNormals = mesh.normals;
        var localNormal = meshNormals != null && meshNormals.Length > 0 ? meshNormals[0] : GetAxisDirection(axes[2]);
        localNormal.Normalize();
        var screenUp = targetRenderer.transform.TransformDirection(GetAxisDirection(heightAxis));
        var screenForward = -targetRenderer.transform.TransformDirection(localNormal);
        var screenRight = Vector3.Cross(screenUp, screenForward).normalized;
        var widthSign = Vector3.Dot(targetRenderer.transform.TransformDirection(GetAxisDirection(widthAxis)), screenRight) < 0f ? -1f : 1f;

        var windowScale = 0.30f;
        var boundsCenter = mesh.bounds.center;
        var materials = targetRenderer.sharedMaterials;
        var windowMaterials = (Material[])materials.Clone();
        if (materialIndex >= 0 && materialIndex < windowMaterials.Length && materials[materialIndex])
        {
            _windowMaterial = new Material(materials[materialIndex])
            {
                name = "Monitor Status Window Runtime",
                hideFlags = HideFlags.HideAndDontSave,
                renderQueue = (int)UnityEngine.Rendering.RenderQueue.Transparent + 1
            };
            windowMaterials[materialIndex] = _windowMaterial;
        }
        for (var i = 0; i < _layerWindowRenderers.Length; i++)
        {
            var window = new GameObject($"Layer {i + 1} Screen Window");
            window.transform.SetParent(targetRenderer.transform, false);
            var scale = Vector3.one;
            SetAxis(ref scale, widthAxis, windowScale);
            SetAxis(ref scale, heightAxis, windowScale);
            window.transform.localScale = scale;
            var position = Vector3.Scale(boundsCenter, Vector3.one - scale);
            SetAxis(ref position, widthAxis, GetAxis(position, widthAxis) + widthSign * (i - 1) * 0.33f * GetAxis(size, widthAxis));
            window.transform.localPosition = position + localNormal * 0.002f;

            window.AddComponent<MeshFilter>().sharedMesh = mesh;
            var renderer = window.AddComponent<MeshRenderer>();
            renderer.sharedMaterials = windowMaterials;
            renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            renderer.receiveShadows = false;
            _layerWindowRenderers[i] = renderer;
        }

        var glass = new GameObject("Monitor Glass Overlay");
        glass.transform.SetParent(targetRenderer.transform, false);
        glass.transform.localPosition = boundsCenter + localNormal * 0.001f;
        glass.AddComponent<MeshFilter>().sharedMesh = mesh;
        var glassRenderer = glass.AddComponent<MeshRenderer>();
        var glassMaterials = (Material[])materials.Clone();
        if (materialIndex >= 0 && materialIndex < glassMaterials.Length)
            glassMaterials[materialIndex] = GetGlassMaterial(glassMaterials[materialIndex]);
        glassRenderer.sharedMaterials = glassMaterials;
        glassRenderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        glassRenderer.receiveShadows = false;
        _glassRenderer = glassRenderer;

        return true;
    }

    private static Material GetGlassMaterial(Material source)
    {
        if (_glassMaterial || !source)
            return _glassMaterial;

        _glassMaterial = new Material(source)
        {
            name = "Monitor Glass Runtime",
            hideFlags = HideFlags.HideAndDontSave,
            renderQueue = (int)UnityEngine.Rendering.RenderQueue.Transparent
        };
        _glassMaterial.SetTexture("_BaseMap", Texture2D.whiteTexture);
        _glassMaterial.SetTexture("_MainTex", Texture2D.whiteTexture);
        _glassMaterial.SetTexture("_EmissionMap", Texture2D.blackTexture);
        _glassMaterial.SetColor("_BaseColor", new Color(0.70f, 0.88f, 1f, 0.12f));
        _glassMaterial.SetColor("_Color", new Color(0.70f, 0.88f, 1f, 0.12f));
        _glassMaterial.SetColor("_EmissionColor", Color.black);
        _glassMaterial.SetFloat("_Surface", 1f);
        _glassMaterial.SetFloat("_Blend", 0f);
        _glassMaterial.SetFloat("_SrcBlend", (float)UnityEngine.Rendering.BlendMode.SrcAlpha);
        _glassMaterial.SetFloat("_DstBlend", (float)UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha);
        _glassMaterial.SetFloat("_ZWrite", 0f);
        _glassMaterial.SetFloat("_Metallic", 0.12f);
        _glassMaterial.SetFloat("_Smoothness", 0.9f);
        _glassMaterial.DisableKeyword("_ALPHATEST_ON");
        _glassMaterial.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
        _glassMaterial.SetOverrideTag("RenderType", "Transparent");
        return _glassMaterial;
    }

    private void ApplyLayerWindows()
    {
        if (screens == null || screens.Length < _layerWindowRenderers.Length)
            return;

        _block ??= new MaterialPropertyBlock();
        targetRenderer.GetPropertyBlock(_block, materialIndex);
        for (var i = 0; i < _layerWindowRenderers.Length; i++)
        {
            var renderer = _layerWindowRenderers[i];
            if (!renderer)
                continue;

            var textureScaleOffset = GetTextureScaleOffset(screens[i]);
            _block.SetTexture("_BaseMap", screens[i]);
            _block.SetTexture("_MainTex", screens[i]);
            _block.SetTexture("_EmissionMap", screens[i]);
            _block.SetVector("_BaseMap_ST", textureScaleOffset);
            _block.SetVector("_MainTex_ST", textureScaleOffset);
            _block.SetVector("_EmissionMap_ST", textureScaleOffset);
            renderer.SetPropertyBlock(_block, materialIndex);
        }
    }

    private void ResetLayerWindows()
    {
        _usingLayerWindows = false;
        if (targetRenderer)
        {
            if (_hasOriginalRendererState)
            {
                targetRenderer.enabled = _originalTargetRendererEnabled;
                _hasOriginalRendererState = false;
            }
        }

        for (var i = 0; i < _layerWindowRenderers.Length; i++)
            _layerWindowRenderers[i] = null;
        _glassRenderer = null;

        DestroyLayerWindowObjects();
        if (_windowMaterial)
        {
            if (Application.isPlaying)
                Destroy(_windowMaterial);
            else
                DestroyImmediate(_windowMaterial);
            _windowMaterial = null;
        }
    }

    private void DestroyLayerWindowObjects()
    {
        if (!targetRenderer)
            return;

        var parent = targetRenderer.transform;
        for (var i = parent.childCount - 1; i >= 0; i--)
        {
            var child = parent.GetChild(i);
            if (!child.name.StartsWith("Layer 1 Screen Window")
                && !child.name.StartsWith("Layer 2 Screen Window")
                && !child.name.StartsWith("Layer 3 Screen Window")
                && !child.name.StartsWith("Monitor Glass Overlay"))
                continue;

            if (Application.isPlaying)
                Destroy(child.gameObject);
            else
                DestroyImmediate(child.gameObject);
        }
    }

    private static float GetAxis(Vector3 value, int axis) => axis == 0 ? value.x : axis == 1 ? value.y : value.z;

    private static Vector3 GetAxisDirection(int axis) => axis == 0 ? Vector3.right : axis == 1 ? Vector3.up : Vector3.forward;

    private static void SetAxis(ref Vector3 value, int axis, float component)
    {
        if (axis == 0) value.x = component;
        else if (axis == 1) value.y = component;
        else value.z = component;
    }

    private bool HasCompleteLayerTextureSet()
    {
        return layer1Connected && layer1Disconnected && layer2Connected && layer2Disconnected && layer3Connected && layer3Disconnected;
    }

    private Texture2D GetLayerTexture(int layer, bool connected)
    {
        return layer switch
        {
            1 => connected ? layer1Connected : layer1Disconnected,
            2 => connected ? layer2Connected : layer2Disconnected,
            3 => connected ? layer3Connected : layer3Disconnected,
            4 => connected ? layer4Connected : layer4Disconnected,
            _ => null
        };
    }

    private Texture2D GetCurrentScreen()
    {
        if (screens == null || screens.Length == 0)
            return null;

        _index = Mathf.Clamp(_index, 0, screens.Length - 1);
        return screens[_index];
    }
}
