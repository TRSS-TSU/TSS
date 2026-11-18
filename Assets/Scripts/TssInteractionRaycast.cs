using System;
using UnityEngine;

public static class TssInteractionRaycast
{
    public static bool IsStructuralOccluder(Collider collider)
    {
        var name = collider.gameObject.name;
        return name.Contains("Floor") || name.Contains("Landing") || name.Contains("Ceiling")
            || name.Contains("Wall") || name.Contains("Exterior");
    }

    public static bool TryGetFirstTarget(Ray ray, Predicate<Collider> isTarget, QueryTriggerInteraction queryTriggers, out RaycastHit targetHit, Predicate<Collider> ignoreOcclusion = null)
    {
        targetHit = default;
        var hits = Physics.RaycastAll(ray, Mathf.Infinity, ~0, queryTriggers);
        Array.Sort(hits, (left, right) => left.distance.CompareTo(right.distance));

        foreach (var hit in hits)
        {
            var collider = hit.collider;
            if (!collider)
                continue;

            if (isTarget(collider))
            {
                targetHit = hit;
                return true;
            }

            if (!collider.isTrigger && (ignoreOcclusion == null || !ignoreOcclusion(collider)))
                return false;
        }

        return false;
    }
}
