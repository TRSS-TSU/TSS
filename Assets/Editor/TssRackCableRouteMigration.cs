using System;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

public static class TssRackCableRouteMigration
{
    [MenuItem("TSS/Cables/Migrate Rack Routes to Profiles")]
    public static void MigrateActiveScene()
    {
        var scene = SceneManager.GetActiveScene();
        var rackNodes = UnityEngine.Object.FindObjectsByType<TssCableRouteNode>(FindObjectsInactive.Include, FindObjectsSortMode.None)
            .Where(node => node && node.gameObject.scene == scene && !string.IsNullOrWhiteSpace(node.rackId))
            .ToArray();
        if (rackNodes.Length == 0)
        {
            Debug.Log("[TSS Cable] No rack route nodes found in the active scene.");
            return;
        }

        if (rackNodes.Any(node => node.transform.childCount != 0 || node.GetComponents<Component>().Length != 2))
        {
            Debug.LogError("[TSS Cable] Rack route migration stopped: a rack route node owns children or extra components.");
            return;
        }

        var controllers = UnityEngine.Object.FindObjectsByType<RackMountController>(FindObjectsInactive.Include, FindObjectsSortMode.None)
            .Where(controller => controller && controller.gameObject.scene == scene)
            .ToDictionary(controller => controller.RackId, StringComparer.OrdinalIgnoreCase);
        var profiles = UnityEngine.Object.FindObjectsByType<RackCableRouteProfile>(FindObjectsInactive.Include, FindObjectsSortMode.None)
            .Where(profile => profile && profile.gameObject.scene == scene && !string.IsNullOrWhiteSpace(profile.RackId))
            .ToDictionary(profile => profile.RackId, StringComparer.OrdinalIgnoreCase);
        var calibrations = new List<(RackMountController controller, RackCableRouteProfile profile, Calibration calibration)>();

        foreach (var group in rackNodes.GroupBy(node => node.rackId, StringComparer.OrdinalIgnoreCase))
        {
            if (!controllers.TryGetValue(group.Key, out var controller))
            {
                Debug.LogError($"[TSS Cable] Rack route migration stopped: no RackMountController for {group.Key}.");
                return;
            }

            profiles.TryGetValue(group.Key, out var profile);
            if (!TryCalibrate(group.ToArray(), controller.MountRoot, out var calibration))
            {
                Debug.LogError($"[TSS Cable] Rack route migration stopped: {group.Key} lacks a complete front/rear/tray/side calibration.");
                return;
            }

            calibrations.Add((controller, profile, calibration));
        }

        foreach (var item in calibrations)
        {
            var profile = item.profile;
            if (!profile)
                profile = Undo.AddComponent<RackCableRouteProfile>(item.controller.gameObject);
            else
                Undo.RecordObject(profile, "Calibrate rack cable route profile");

            profile.Configure(item.controller.RackId, item.calibration.LeftX, item.calibration.RightX, item.calibration.FrontZ, item.calibration.RearZ, item.calibration.TrayY);
            EditorUtility.SetDirty(profile);
        }

        foreach (var node in rackNodes)
            Undo.DestroyObjectImmediate(node.gameObject);

        foreach (var provider in UnityEngine.Object.FindObjectsByType<TssCableRouteProvider>(FindObjectsInactive.Include, FindObjectsSortMode.None)
                     .Where(provider => provider && provider.gameObject.scene == scene))
            provider.RefreshRouteNodes();

        EditorSceneManager.MarkSceneDirty(scene);
        EditorSceneManager.SaveScene(scene);
        Debug.Log($"[TSS Cable] Migrated {rackNodes.Length} rack route nodes to {calibrations.Count} rack profiles.");
    }

    private static bool TryCalibrate(TssCableRouteNode[] nodes, Transform space, out Calibration calibration)
    {
        calibration = default;
        if (!space)
            return false;

        var left = Values(nodes.Where(node => node.side == TssCableRouteSide.Left), space, point => point.x);
        var right = Values(nodes.Where(node => node.side == TssCableRouteSide.Right), space, point => point.x);
        var front = Values(nodes.Where(node => node.role == TssCableRouteNodeRole.RackFront), space, point => point.z);
        var rear = Values(nodes.Where(node => node.role == TssCableRouteNodeRole.RackRear), space, point => point.z);
        if (left.Count == 0 || right.Count == 0 || front.Count == 0 || rear.Count == 0)
            return false;

        // ponytail: the current graph has no Tray role; use its highest authored rack corridor until physical tray anchors are introduced.
        var trayY = nodes.Where(node => node).Max(node => space.InverseTransformPoint(node.transform.position).y);
        calibration = new Calibration(Median(left), Median(right), Median(front), Median(rear), trayY);
        return Mathf.Abs(calibration.LeftX - calibration.RightX) > 0.0001f;
    }

    private static List<float> Values(IEnumerable<TssCableRouteNode> nodes, Transform space, Func<Vector3, float> select)
    {
        return nodes.Where(node => node).Select(node => select(space.InverseTransformPoint(node.transform.position))).ToList();
    }

    private static float Median(List<float> values)
    {
        values.Sort();
        var middle = values.Count / 2;
        return values.Count % 2 == 0 ? (values[middle - 1] + values[middle]) * 0.5f : values[middle];
    }

    private readonly struct Calibration
    {
        public readonly float LeftX;
        public readonly float RightX;
        public readonly float FrontZ;
        public readonly float RearZ;
        public readonly float TrayY;

        public Calibration(float leftX, float rightX, float frontZ, float rearZ, float trayY)
        {
            LeftX = leftX;
            RightX = rightX;
            FrontZ = frontZ;
            RearZ = rearZ;
            TrayY = trayY;
        }
    }
}
