using UnityEngine;

public sealed class TssCableVisual : MonoBehaviour
{
    public static TssCableVisual Create(TssCableDefinition cable, TssPortEndpoint endpointA, TssPortEndpoint endpointB)
    {
        if (!cable || !endpointA || !endpointB)
            return null;

        var root = new GameObject($"Cable {endpointA.DisplayLabel} to {endpointB.DisplayLabel}");
        var visual = root.AddComponent<TssCableVisual>();
        var provider = FindFirstObjectByType<TssCableRouteProvider>();
        var points = provider ? provider.GetRoute(endpointA, endpointB) : new[] { endpointA.CableAnchorPosition, endpointB.CableAnchorPosition };
        visual.Build(cable, points);
        return visual;
    }

    private void Build(TssCableDefinition cable, Vector3[] points)
    {
        if (points == null || points.Length < 2)
            return;

        for (var i = 0; i < points.Length - 1; i++)
            CreateSegment(cable, points[i], points[i + 1], i);
    }

    private void CreateSegment(TssCableDefinition cable, Vector3 start, Vector3 end, int index)
    {
        var direction = end - start;
        var length = direction.magnitude;
        if (length <= 0.001f)
            return;

        var segment = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
        segment.name = $"Segment {index + 1:00}";
        segment.transform.SetParent(transform, false);
        segment.transform.position = (start + end) * 0.5f;
        segment.transform.rotation = Quaternion.FromToRotation(Vector3.up, direction.normalized);
        segment.transform.localScale = new Vector3(cable.radiusMeters * 2f, length * 0.5f, cable.radiusMeters * 2f);

        if (segment.TryGetComponent<Collider>(out var collider))
            DestroyUnityObject(collider);

        if (cable.visualMaterial && segment.TryGetComponent<Renderer>(out var renderer))
            renderer.sharedMaterial = cable.visualMaterial;
    }

    private static void DestroyUnityObject(Object target)
    {
        if (!target)
            return;

        if (Application.isPlaying)
            Destroy(target);
        else
            DestroyImmediate(target);
    }
}
