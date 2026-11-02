using System.Collections.Generic;
using UnityEngine;

public static class CableCurveBuilder
{
    public static bool TryBuild(
        TssCableRouteResult route,
        CableLaneReservation reservation,
        CableVisualSettings settings,
        out Vector3[] samples,
        out string failure)
    {
        samples = null;
        failure = string.Empty;
        settings ??= CableVisualSettings.Defaults();
        if (route == null || !route.IsValid)
        {
            failure = route?.FailureReason ?? "Missing route result.";
            return false;
        }

        var points = CleanPoints(route.RenderPoints);
        if (points.Count < 2)
        {
            failure = "Route contains fewer than two finite points.";
            return false;
        }

        // ponytail: authored route points are already the intended cable centerline;
        // runtime smoothing and lane displacement made cables curve around one another.
        var curve = points;
        if (curve.Count < 2 || curve.Exists(point => !IsFinite(point)))
        {
            failure = "Curve generation produced non-finite samples.";
            return false;
        }

        for (var i = 1; i < curve.Count; i++)
        {
            if ((curve[i] - curve[i - 1]).sqrMagnitude <= 0.000001f)
            {
                failure = $"Curve generation produced a zero-length span at sample {i}.";
                return false;
            }
        }

        samples = curve.ToArray();
        return true;
    }

    private static List<Vector3> CleanPoints(IReadOnlyList<Vector3> source)
    {
        var points = new List<Vector3>();
        if (source == null)
            return points;
        foreach (var point in source)
            if (IsFinite(point))
                AddIfUseful(points, point);
        return points;
    }

    private static void AddIfUseful(List<Vector3> points, Vector3 point)
    {
        if (points.Count == 0 || (points[points.Count - 1] - point).sqrMagnitude > 0.000001f)
            points.Add(point);
    }

    private static bool IsFinite(Vector3 point)
    {
        return float.IsFinite(point.x) && float.IsFinite(point.y) && float.IsFinite(point.z);
    }
}
