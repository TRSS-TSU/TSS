using System;
using System.Collections.Generic;
using UnityEngine;

public static class RackCableRoutePlanner
{
    public static bool TryBuild(TssPortEndpoint start, TssPortEndpoint end, RackCableRouteProfile profile, out TssCableRouteResult route)
    {
        route = null;
        if (!start || !end || !profile || !profile.IsValid
            || !TryRackId(start.EndpointId, out var startRackId)
            || !TryRackId(end.EndpointId, out var endRackId)
            || !string.Equals(startRackId, endRackId, StringComparison.OrdinalIgnoreCase)
            || !string.Equals(startRackId, profile.RackId, StringComparison.OrdinalIgnoreCase))
            return false;

        var space = profile.RouteSpace;
        if (!space)
            return false;

        var startLocal = space.InverseTransformPoint(start.CableAnchorPosition);
        var endLocal = space.InverseTransformPoint(end.CableAnchorPosition);
        if (!IsFinite(startLocal) || !IsFinite(endLocal))
            return false;

        var startFace = FaceFor(start.InterfaceFace, startLocal.z, profile);
        var endFace = FaceFor(end.InterfaceFace, endLocal.z, profile);
        var side = SharedSideFor(startLocal.x, endLocal.x, profile);
        var points = new List<Vector3>(8);

        Add(points, start.CableAnchorPosition);
        Add(points, World(space, new Vector3(startLocal.x, startLocal.y, FaceZ(startFace, profile))));
        Add(points, World(space, new Vector3(SideX(side, profile), startLocal.y, FaceZ(startFace, profile))));

        if (startFace == endFace)
        {
            // ponytail: same-face patch leads need one side run, not a top-tray detour.
            Add(points, World(space, new Vector3(SideX(side, profile), endLocal.y, FaceZ(endFace, profile))));
        }
        else
        {
            Add(points, World(space, new Vector3(SideX(side, profile), profile.TrayLocalY, FaceZ(startFace, profile))));
            Add(points, World(space, new Vector3(SideX(side, profile), profile.TrayLocalY, FaceZ(endFace, profile))));
            Add(points, World(space, new Vector3(SideX(side, profile), endLocal.y, FaceZ(endFace, profile))));
        }

        Add(points, World(space, new Vector3(endLocal.x, endLocal.y, FaceZ(endFace, profile))));
        Add(points, end.CableAnchorPosition);

        if (points.Count < 2 || points.Exists(point => !IsFinite(point)))
            return false;

        var kind = start.InterfaceFace == end.InterfaceFace ? TssCableRouteKind.SameRack : TssCableRouteKind.FrontToRear;
        var edges = new List<TssCableRouteEdge> { new($"Rack:{profile.RackId}:{startFace}:{side}:Duct") };
        if (startFace != endFace)
        {
            edges.Add(new($"Rack:{profile.RackId}:Tray"));
            edges.Add(new($"Rack:{profile.RackId}:{endFace}:{side}:Duct"));
        }
        route = TssCableRouteResult.Valid(start.EndpointId, end.EndpointId, kind, Array.Empty<TssCableRouteNode>(), edges, points);
        return true;
    }

    public static bool TryRackId(string endpointId, out string rackId)
    {
        rackId = string.Empty;
        if (string.IsNullOrWhiteSpace(endpointId))
            return false;

        const string rackPrefix = "Rack:";
        const string patchPrefix = "PatchPanel:";
        var prefix = endpointId.StartsWith(rackPrefix, StringComparison.Ordinal) ? rackPrefix
            : endpointId.StartsWith(patchPrefix, StringComparison.Ordinal) ? patchPrefix
            : null;
        if (prefix == null)
            return false;

        var start = prefix.Length;
        var end = endpointId.IndexOf(':', start);
        rackId = (end < 0 ? endpointId[start..] : endpointId[start..end]).Trim();
        return !string.IsNullOrWhiteSpace(rackId);
    }

    private static string FaceFor(EquipmentInterfaceFace face, float localZ, RackCableRouteProfile profile)
    {
        if (face == EquipmentInterfaceFace.Rear)
            return "Rear";
        if (face == EquipmentInterfaceFace.Front)
            return "Front";
        return Mathf.Abs(localZ - profile.FrontFaceLocalZ) <= Mathf.Abs(localZ - profile.RearFaceLocalZ) ? "Front" : "Rear";
    }

    private static string SharedSideFor(float startLocalX, float endLocalX, RackCableRouteProfile profile)
    {
        var leftDistance = Mathf.Abs(startLocalX - profile.LeftDuctLocalX) + Mathf.Abs(endLocalX - profile.LeftDuctLocalX);
        var rightDistance = Mathf.Abs(startLocalX - profile.RightDuctLocalX) + Mathf.Abs(endLocalX - profile.RightDuctLocalX);
        return leftDistance <= rightDistance ? "Left" : "Right";
    }

    private static float FaceZ(string face, RackCableRouteProfile profile) => face == "Rear" ? profile.RearFaceLocalZ : profile.FrontFaceLocalZ;
    private static float SideX(string side, RackCableRouteProfile profile) => side == "Left" ? profile.LeftDuctLocalX : profile.RightDuctLocalX;
    private static Vector3 World(Transform space, Vector3 local) => space.TransformPoint(local);

    private static void Add(List<Vector3> points, Vector3 point)
    {
        if (points.Count == 0 || (points[^1] - point).sqrMagnitude > 0.000001f)
            points.Add(point);
    }

    private static bool IsFinite(Vector3 point) => float.IsFinite(point.x) && float.IsFinite(point.y) && float.IsFinite(point.z);
}
