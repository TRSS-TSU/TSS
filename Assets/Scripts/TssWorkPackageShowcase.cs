using System;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;
using UnityEngine.Video;

[Serializable]
public sealed class TssWorkPackageEntry
{
    public string id = "WP";
    public string label = "WP";
    public VideoClip clip;
    [Tooltip("Optional StreamingAssets or WebGL URL fallback when no VideoClip is assigned.")]
    public string url;
    [TextArea] public string missingMessage = "Video preview will be available when this work package clip is supplied.";
}

public sealed class TssWorkPackageShowcase : MonoBehaviour
{
    [Header("Preview")]
    [SerializeField] private VideoPlayer videoPlayer;
    [SerializeField] private RawImage previewImage;
    [SerializeField] private Text missingVideoText;
    [SerializeField] private GameObject missingVideoMessage;

    [Header("Selectors")]
    [SerializeField] private TssWorkPackageEntry[] workPackages = new TssWorkPackageEntry[4];
    [SerializeField] private Button[] selectorButtons = new Button[4];
    [SerializeField] private Image[] selectorBackgrounds = new Image[4];
    [SerializeField] private Color activeColor = new Color(0.93f, 0.51f, 0.14f, 1f);
    [SerializeField] private Color inactiveColor = new Color(0.12f, 0.18f, 0.22f, 1f);
    [SerializeField, Range(0, 3)] private int defaultWorkPackageIndex;
    [SerializeField] private bool autoplayDefault = true;
    [SerializeField] private bool loop = true;

    private int selectedIndex = -1;
    private int requestedIndex = -1;

    private void Awake()
    {
        if (videoPlayer)
        {
            videoPlayer.playOnAwake = false;
            videoPlayer.isLooping = loop;
            videoPlayer.audioOutputMode = VideoAudioOutputMode.None;
            videoPlayer.prepareCompleted += OnPrepared;
            videoPlayer.errorReceived += OnPlaybackError;
        }

        for (var i = 0; i < selectorButtons.Length; i++)
        {
            var index = i;
            if (selectorButtons[i])
            {
                selectorButtons[i].onClick.AddListener(() => SelectWorkPackage(index));
                var pointer = selectorButtons[i].GetComponent<TssWorkPackageSelectorPointer>();
                if (!pointer) pointer = selectorButtons[i].gameObject.AddComponent<TssWorkPackageSelectorPointer>();
                pointer.Initialize(this, index);
            }
        }
    }

    private void OnEnable()
    {
        if (autoplayDefault)
            SelectWorkPackage(Mathf.Clamp(defaultWorkPackageIndex, 0, Mathf.Max(0, workPackages.Length - 1)));
    }

    private void OnDestroy()
    {
        if (!videoPlayer) return;
        videoPlayer.prepareCompleted -= OnPrepared;
        videoPlayer.errorReceived -= OnPlaybackError;
    }

    public void SelectWorkPackage(int index)
    {
        if (index < 0 || index >= workPackages.Length || index == selectedIndex) return;
        selectedIndex = index;
        requestedIndex = index;
        UpdateSelectorVisuals();

        var entry = workPackages[index];
        if (entry == null || (!entry.clip && string.IsNullOrWhiteSpace(entry.url)))
        {
            StopAndShowMissing(entry != null ? entry.missingMessage : "No video preview is configured for this work package.");
            return;
        }

        if (!videoPlayer)
        {
            ShowMissing("Video preview player is not configured.");
            return;
        }

        videoPlayer.Stop();
        #if UNITY_WEBGL && !UNITY_EDITOR
        videoPlayer.source = VideoSource.Url;
        videoPlayer.url = BuildWebVideoUrl(entry.url);
        #else
        if (entry.clip)
        {
            videoPlayer.source = VideoSource.VideoClip;
            videoPlayer.clip = entry.clip;
        }
        else
        {
            videoPlayer.source = VideoSource.Url;
            videoPlayer.url = BuildWebVideoUrl(entry.url);
        }
        #endif

        HideMissing();
        videoPlayer.Prepare();
    }

    private static string BuildWebVideoUrl(string value)
    {
        if (value.StartsWith("http://", StringComparison.OrdinalIgnoreCase) || value.StartsWith("https://", StringComparison.OrdinalIgnoreCase))
            return value;

        return Application.streamingAssetsPath.TrimEnd('/', '\\') + "/" + value.TrimStart('/', '\\');
    }

    private void OnPrepared(VideoPlayer source)
    {
        if (!videoPlayer || selectedIndex != requestedIndex || selectedIndex < 0) return;
        videoPlayer.Play();
    }

    private void OnPlaybackError(VideoPlayer source, string message)
    {
        if (selectedIndex != requestedIndex) return;
        Debug.LogWarning("[TSS Work Packages] " + GetEntryId(selectedIndex) + " preview failed: " + message);
        StopAndShowMissing("Preview unavailable for " + GetEntryId(selectedIndex) + ".");
    }

    private void UpdateSelectorVisuals()
    {
        for (var i = 0; i < selectorBackgrounds.Length; i++)
            if (selectorBackgrounds[i]) selectorBackgrounds[i].color = i == selectedIndex ? activeColor : inactiveColor;
    }

    private void StopAndShowMissing(string message)
    {
        if (videoPlayer) videoPlayer.Stop();
        ShowMissing(message);
    }

    private void ShowMissing(string message)
    {
        if (missingVideoMessage) missingVideoMessage.SetActive(true);
        if (missingVideoText) missingVideoText.text = message;
        if (previewImage) previewImage.color = new Color(0.06f, 0.09f, 0.12f, 1f);
    }

    private void HideMissing()
    {
        if (missingVideoMessage) missingVideoMessage.SetActive(false);
        if (previewImage) previewImage.color = Color.white;
    }

    private string GetEntryId(int index) =>
        index >= 0 && index < workPackages.Length && workPackages[index] != null && !string.IsNullOrEmpty(workPackages[index].id)
            ? workPackages[index].id
            : "work package";
}
