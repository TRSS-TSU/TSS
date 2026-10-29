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

        var curve = new List<Vector3>(Mathf.Min(settings.maximumCenterlineSamples, points.Count * 4));
        curve.Add(points[0]);
        for (var i = 1; i < points.Count - 1; i++)
        {
            var previous = points[i - 1];
            var current = points[i];
            var next = points[i + 1];
            var incoming = current - previous;
            var outgoing = next - current;
            var incomingLength = incoming.magnitude;
            var outgoingLength = outgoing.magnitude;
            if (incomingLength <= 0.001f || outgoingLength <= 0.001f)
                continue;

            var incomingDirection = incoming / incomingLength;
            var outgoingDirection = outgoing / outgoingLength;
            if (Vector3.Dot(incomingDirection, outgoingDirection) > 0.999f)
            {
                AddIfUseful(curve, current);
                continue;
            }

            var bendRadius = Mathf.Min(incomingLength, outgoingLength) * 0.10f;
            var entry = current - incomingDirection * bendRadius;
            var exit = current + outgoingDirection * bendRadius;
            AddIfUseful(curve, entry);
            var bendSamples = Mathf.Clamp(Mathf.CeilToInt(bendRadius / Mathf.Max(settings.RadiusMeters, 0.001f)), 2, 6);
            for (var sample = 1; sample < bendSamples; sample++)
            {
                var t = sample / (float)bendSamples;
                var oneMinus = 1f - t;
                AddIfUseful(curve, oneMinus * oneMinus * entry + 2f * oneMinus * t * current + t * t * exit);
            }
            AddIfUseful(curve, exit);
        }
        AddIfUseful(curve, points[points.Count - 1]);

        if (curve.Count > settings.maximumCenterlineSamples)
            curve = ReduceSamples(curve, settings.maximumCenterlineSamples);

        ApplyLaneOffset(curve, reservation.MaximumSlot, settings);
        curve[0] = points[0];
        curve[curve.Count - 1] = points[points.Count - 1];
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

    private static List<Vector3> ReduceSamples(List<Vector3> source, int maximum)
    {
        var result = new List<Vector3>(maximum);
        if (maximum <= 2)
            return new List<Vector3> { source[0], source[source.Count - 1] };
        for (var i = 0; i < maximum; i++)
        {
            var sourceIndex = Mathf.RoundToInt(i * (source.Count - 1f) / (maximum - 1f));
            AddIfUseful(result, source[sourceIndex]);
        }
        return result;
    }

    private static void ApplyLaneOffset(List<Vector3> points, int slot, CableVisualSettings settings)
    {
        if (slot <= 0 || points.Count < 2)
            return;

        var offsetDistance = slot * settings.LaneSpacingMeters;
        for (var i = 1; i < points.Count - 1; i++)
        {
            var tangent = (points[Mathf.Min(i + 1, points.Count - 1)] - points[Mathf.Max(i - 1, 0)]).normalized;
            if (tangent.sqrMagnitude <= 0.000001f)
                continue;
            var normal = Vector3.Cross(tangent, Vector3.up);
            if (normal.sqrMagnitude <= 0.000001f)
                normal = Vector3.Cross(tangent, Vector3.right);
            normal.Normalize();
            var endpointTaper = Mathf.Min(i / 3f, (points.Count - 1 - i) / 3f, 1f);
            points[i] += normal * offsetDistance * endpointTaper;
        }
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
