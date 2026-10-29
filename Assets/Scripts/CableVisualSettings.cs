using UnityEngine;

public enum TssCableVisualMode
{
    LegacySegments,
    RoundedTube,
}

[System.Serializable]
public sealed class CableVisualSettings
{
    [Min(0.001f)] public float uniformDiameterMeters = 0.008f;
    [Range(4, 16)] public int radialSegments = 8;
    [Min(8)] public int maximumCenterlineSamples = 256;
    [Min(0f)] public float laneGapMeters = 0.004f;
    public bool adaptiveSampling = true;
    public bool legacyFallback = true;

    public float RadiusMeters => Mathf.Max(0.0005f, uniformDiameterMeters * 0.5f);
    public float LaneSpacingMeters => uniformDiameterMeters + Mathf.Max(0f, laneGapMeters);

    public static CableVisualSettings Defaults()
    {
        return new CableVisualSettings();
    }
}
