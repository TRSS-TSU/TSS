using System;
using System.Collections;
using StarterAssets;
using UnityEngine;
using UnityEngine.UI;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif
using Unity.Cinemachine;

public sealed class TssObjectInteractable : MonoBehaviour
{
    private static TssObjectInteractable _activeInteractable;
    public event Action EnteredFirstPerson;
    public event Action ReturnedToThirdPerson;
    public bool IsFirstPerson => _isFirstPerson;

    [Header("View")]
    [SerializeField] private Transform firstPersonView;
    [SerializeField] private Camera mainCamera;
    [SerializeField] private float transitionSeconds = 0.75f;
    [SerializeField] private float firstPersonFieldOfView = 50f;

    [Header("Player")]
    [SerializeField] private ThirdPersonController playerController;
    [SerializeField] private StarterAssetsInputs playerInput;
    [SerializeField, Min(0f)] private float fallbackInteractionRange = 2.75f;

    [Header("Avatar")]
    [SerializeField] private bool hideAvatarInFirstPerson = true;
    [SerializeField] private GameObject avatarRoot;

    [Header("Optional UI")]
    [SerializeField] private GameObject canvasPrefab;

    [Header("Hover Highlight")]
    [SerializeField] private bool highlightOnHover = true;
    [SerializeField] private Color highlightTint = new Color(0.2f, 0.75f, 1f, 1f);
    [SerializeField, Range(0f, 1f)] private float highlightBlend = 0.45f;

    private CinemachineBrain _cinemachineBrain;
    private Renderer[] _avatarRenderers;
    private Material[][] _highlightMaterials;
    private Color[][] _highlightBaseColors;
    private GameObject _canvasInstance;
    private Coroutine _transition;
    private Vector3 _thirdPersonPosition;
    private Quaternion _thirdPersonRotation;
    private float _thirdPersonFieldOfView;
    private bool _isFirstPerson;
    private bool _playerInRange;
    private bool _isHovered;

    private void Awake()
    {
        if (!mainCamera)
            mainCamera = Camera.main;

        if (mainCamera)
            _cinemachineBrain = mainCamera.GetComponent<CinemachineBrain>();

        if (!playerController)
            playerController = FindFirstObjectByType<ThirdPersonController>();

        if (!playerInput && playerController)
            playerInput = playerController.GetComponent<StarterAssetsInputs>();

        if (!avatarRoot && playerController)
            avatarRoot = playerController.gameObject;

        if (avatarRoot)
            _avatarRenderers = avatarRoot.GetComponentsInChildren<Renderer>(true);

        CacheHighlightMaterials();
    }

    private void Update()
    {
        UpdateMouseHover();

        if (_isFirstPerson)
        {
            if (WasExitPressed())
                ReturnToThirdPerson();

            return;
        }

        if (IsPlayerInInteractionRange() && _isHovered && WasPrimaryClickPressed())
            EnterFirstPerson();
    }

    private void OnMouseDown()
    {
        if (IsPlayerInInteractionRange() && !_isFirstPerson && _transition == null && !IsGameplayInputBlocked())
            EnterFirstPerson();
    }

    private void OnMouseEnter()
    {
        if (IsPlayerInInteractionRange())
            SetHovered(true);
    }

    private void OnMouseExit()
    {
        SetHovered(false);
    }

    private void OnDisable()
    {
        if (_transition != null)
            StopCoroutine(_transition);

        if (_activeInteractable == this)
            _activeInteractable = null;

        _transition = null;
        _isFirstPerson = false;
        HideCanvas();
        SetAvatarVisible(true);
        SetCinemachine(true);
        SetPlayerControl(true);
        SetHovered(false);
    }

    private void OnTriggerEnter(Collider other)
    {
        if (IsPlayer(other))
            SetPlayerInRange(true);
    }

    private void OnTriggerExit(Collider other)
    {
        if (IsPlayer(other))
            SetPlayerInRange(false);
    }

    private void OnGUI()
    {
        if (!_isFirstPerson || _canvasInstance)
            return;

        // ponytail: OnGUI is enough for the temporary return button until the real canvas prefab exists.
        if (GUI.Button(new Rect(Screen.width - 180f, 20f, 160f, 44f), "Return"))
            ReturnToThirdPerson();
    }

    public void EnterFirstPerson()
    {
        if (_isFirstPerson || _transition != null || !mainCamera)
            return;

        if (_activeInteractable && _activeInteractable != this)
            _activeInteractable.ReleaseFirstPersonForHandoff();

        var target = firstPersonView ? firstPersonView : transform;
        _activeInteractable = this;
        _isFirstPerson = true;
        _thirdPersonPosition = mainCamera.transform.position;
        _thirdPersonRotation = mainCamera.transform.rotation;
        _thirdPersonFieldOfView = mainCamera.fieldOfView;

        SetPlayerControl(false);
        SetAvatarVisible(false);
        SetCinemachine(false);
        SetHovered(false);
        ShowCanvas();
        StartMove(target.position, target.rotation, firstPersonFieldOfView, () => EnteredFirstPerson?.Invoke());
    }

    public void ReturnToThirdPerson()
    {
        if (!_isFirstPerson || _transition != null || !mainCamera)
            return;

        if (_activeInteractable == this)
            _activeInteractable = null;

        _isFirstPerson = false;
        HideCanvas();
        SetAvatarVisible(true);
        StartMove(_thirdPersonPosition, _thirdPersonRotation, _thirdPersonFieldOfView, () =>
        {
            SetCinemachine(true);
            SetPlayerControl(true);
            ReturnedToThirdPerson?.Invoke();
        });
    }

    public void SetPlayerInRange(bool inRange)
    {
        _playerInRange = inRange;

        if (!inRange)
            SetHovered(false);
    }

    private void ReleaseFirstPersonForHandoff()
    {
        if (_transition != null)
            StopCoroutine(_transition);

        _transition = null;
        _isFirstPerson = false;
        HideCanvas();
        SetHovered(false);
    }

    private void StartMove(Vector3 position, Quaternion rotation, float fieldOfView, System.Action after = null)
    {
        if (_transition != null)
            StopCoroutine(_transition);

        _transition = StartCoroutine(MoveCamera(position, rotation, fieldOfView, after));
    }

    private IEnumerator MoveCamera(Vector3 position, Quaternion rotation, float fieldOfView, System.Action after)
    {
        var startPosition = mainCamera.transform.position;
        var startRotation = mainCamera.transform.rotation;
        var startFieldOfView = mainCamera.fieldOfView;
        var elapsed = 0f;
        var duration = Mathf.Max(0.01f, transitionSeconds);

        while (elapsed < duration)
        {
            var t = Mathf.SmoothStep(0f, 1f, elapsed / duration);
            mainCamera.transform.SetPositionAndRotation(
                Vector3.Lerp(startPosition, position, t),
                Quaternion.Slerp(startRotation, rotation, t));
            mainCamera.fieldOfView = Mathf.Lerp(startFieldOfView, fieldOfView, t);
            elapsed += Time.deltaTime;
            yield return null;
        }

        mainCamera.transform.SetPositionAndRotation(position, rotation);
        mainCamera.fieldOfView = fieldOfView;
        _transition = null;
        after?.Invoke();
    }

    private void SetPlayerControl(bool enabled)
    {
        if (playerInput)
        {
            playerInput.MoveInput(Vector2.zero);
            playerInput.LookInput(Vector2.zero);
            playerInput.SprintInput(false);
        }

        if (playerController)
            playerController.enabled = enabled;
    }

    private void SetCinemachine(bool enabled)
    {
        if (_cinemachineBrain)
            _cinemachineBrain.enabled = enabled;
    }

    private void SetAvatarVisible(bool visible)
    {
        if (!hideAvatarInFirstPerson || _avatarRenderers == null)
            return;

        foreach (var renderer in _avatarRenderers)
        {
            if (renderer)
                renderer.enabled = visible;
        }
    }

    private void ShowCanvas()
    {
        if (!canvasPrefab)
            return;

        _canvasInstance = Instantiate(canvasPrefab);
        var button = _canvasInstance.GetComponentInChildren<Button>(true);
        if (button)
            button.onClick.AddListener(ReturnToThirdPerson);
    }

    private void HideCanvas()
    {
        if (_canvasInstance)
            Destroy(_canvasInstance);
    }

    private void CacheHighlightMaterials()
    {
        var highlightRenderers = GetComponentsInChildren<Renderer>(true);
        _highlightMaterials = new Material[highlightRenderers.Length][];
        _highlightBaseColors = new Color[highlightRenderers.Length][];

        for (var rendererIndex = 0; rendererIndex < highlightRenderers.Length; rendererIndex++)
        {
            var materials = highlightRenderers[rendererIndex].materials;
            _highlightMaterials[rendererIndex] = materials;
            _highlightBaseColors[rendererIndex] = new Color[materials.Length];

            for (var materialIndex = 0; materialIndex < materials.Length; materialIndex++)
                _highlightBaseColors[rendererIndex][materialIndex] = GetMaterialColor(materials[materialIndex]);
        }
    }

    private void SetHighlight(bool highlighted)
    {
        if (!highlightOnHover || _highlightMaterials == null)
            return;

        for (var rendererIndex = 0; rendererIndex < _highlightMaterials.Length; rendererIndex++)
        {
            var materials = _highlightMaterials[rendererIndex];
            for (var materialIndex = 0; materialIndex < materials.Length; materialIndex++)
            {
                var material = materials[materialIndex];
                if (!material)
                    continue;

                var baseColor = _highlightBaseColors[rendererIndex][materialIndex];
                SetMaterialColor(material, highlighted ? Color.Lerp(baseColor, highlightTint, highlightBlend) : baseColor);
            }
        }
    }

    private static Color GetMaterialColor(Material material)
    {
        if (!material)
            return Color.white;

        if (material.HasProperty("_BaseColor"))
            return material.GetColor("_BaseColor");

        return material.HasProperty("_Color") ? material.GetColor("_Color") : Color.white;
    }

    private static void SetMaterialColor(Material material, Color color)
    {
        if (material.HasProperty("_BaseColor"))
            material.SetColor("_BaseColor", color);
        else if (material.HasProperty("_Color"))
            material.SetColor("_Color", color);
    }

    private void UpdateMouseHover()
    {
        if (!IsPlayerInInteractionRange() || !mainCamera || _isFirstPerson)
        {
            SetHovered(false);
            return;
        }

        var ray = mainCamera.ScreenPointToRay(GetMousePosition());
        var hits = Physics.RaycastAll(ray, Mathf.Infinity, ~0, QueryTriggerInteraction.Ignore);
        Array.Sort(hits, (left, right) => left.distance.CompareTo(right.distance));

        foreach (var hit in hits)
        {
            if (hit.collider && hit.collider.transform.IsChildOf(transform))
            {
                SetHovered(true);
                return;
            }
        }

        SetHovered(false);
    }

    private void SetHovered(bool hovered)
    {
        if (_isHovered == hovered)
            return;

        _isHovered = hovered;
        SetHighlight(hovered);
    }

    private bool IsPlayerInInteractionRange()
    {
        if (_playerInRange)
            return true;

        if (fallbackInteractionRange <= 0f)
            return false;

        var player = GetPlayerTransform();
        if (player && Vector3.Distance(player.position, transform.position) <= fallbackInteractionRange)
            return true;

        foreach (var taggedPlayer in GameObject.FindGameObjectsWithTag("Player"))
        {
            if (taggedPlayer && Vector3.Distance(taggedPlayer.transform.position, transform.position) <= fallbackInteractionRange)
                return true;
        }

        return false;
    }

    private Transform GetPlayerTransform()
    {
        if (playerController)
            return playerController.transform;

        if (avatarRoot)
            return avatarRoot.transform;

        var player = GameObject.FindGameObjectWithTag("Player");
        return player ? player.transform : null;
    }

    private static Vector3 GetMousePosition()
    {
#if ENABLE_INPUT_SYSTEM
        return Mouse.current != null ? (Vector3)Mouse.current.position.ReadValue() : Vector3.zero;
#else
        return Input.mousePosition;
#endif
    }

    private static bool WasPrimaryClickPressed()
    {
        if (IsGameplayInputBlocked())
            return false;

#if ENABLE_INPUT_SYSTEM
        return Mouse.current != null && Mouse.current.leftButton.wasPressedThisFrame;
#else
        return Input.GetMouseButtonDown(0);
#endif
    }

    private static bool IsPlayer(Collider other)
    {
        return other.CompareTag("Player") || other.GetComponentInParent<ThirdPersonController>();
    }

    private static bool WasExitPressed()
    {
#if ENABLE_INPUT_SYSTEM
        return Keyboard.current != null && Keyboard.current.escapeKey.wasPressedThisFrame;
#else
        return Input.GetKeyDown(KeyCode.Escape);
#endif
    }

    private static bool IsGameplayInputBlocked()
    {
        return TssRuntimeUi.Instance && TssRuntimeUi.Instance.IsGameplayInputBlocked;
    }
}
