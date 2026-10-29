using System.Collections.Generic;
using UnityEngine;

public sealed class TssCableVisual : MonoBehaviour
{
    public static TssCableVisualMode RendererMode { get; set; } = TssCableVisualMode.RoundedTube;
    public static CableVisualSettings Settings { get; } = CableVisualSettings.Defaults();

    private CableLaneAllocator _laneAllocator;
    private string _connectionId;
    private GameObject _startConnector;
    private GameObject _endConnector;

    public static TssCableVisual Create(TssCableDefinition cable, TssPortEndpoint endpointA, TssPortEndpoint endpointB)
    {
        return Create(cable, endpointA, endpointB, string.Empty, null);
    }

    public static TssCableVisual Create(TssCableDefinition cable, TssPortEndpoint endpointA, TssPortEndpoint endpointB, string connectionId, CableLaneAllocator laneAllocator, GameObject endpointConnectorPrefab = null)
    {
        if (!cable || !endpointA || !endpointB)
            return null;

        var root = new GameObject($"Cable {endpointA.DisplayLabel} to {endpointB.DisplayLabel}");
        var visual = root.AddComponent<TssCableVisual>();
        var effectiveConnectionId = string.IsNullOrWhiteSpace(connectionId) ? $"Visual-{root.GetInstanceID()}" : connectionId;
        visual._connectionId = effectiveConnectionId;
        visual._laneAllocator = laneAllocator;
        var provider = Object.FindFirstObjectByType<TssCableRouteProvider>();
        var routePoints = provider ? provider.GetRoute(endpointA, endpointB) : new[] { endpointA.CableAnchorPosition, endpointB.CableAnchorPosition };
        visual.CreateEndpointConnectors(cable, endpointConnectorPrefab, endpointA, endpointB, routePoints);

        if (RendererMode == TssCableVisualMode.RoundedTube && provider)
        {
            var route = provider.GetRouteResult(endpointA, endpointB);
            if (route.IsValid && !route.HasRenderContinuityBoundary)
            {
                var allocator = laneAllocator ?? new CableLaneAllocator();
                visual._laneAllocator = allocator;
                var reservation = allocator.Reserve(effectiveConnectionId, ConvertKeys(route.Edges));
                if (visual.BuildRounded(cable, route, reservation))
                    return visual;

                allocator.Release(effectiveConnectionId);
                Debug.LogWarning($"[TSS Cable] Rounded visual failed for {effectiveConnectionId}; using legacy segments.");
            }
            else if (!route.IsValid)
            {
                Debug.LogWarning($"[TSS Cable] Rounded route unavailable for {effectiveConnectionId}: {route.FailureReason}");
            }
        }

        var points = routePoints;
        visual.BuildLegacy(cable, points);
        return visual;
    }

    private void CreateEndpointConnectors(TssCableDefinition cable, GameObject prefab, TssPortEndpoint endpointA, TssPortEndpoint endpointB, Vector3[] routePoints)
    {
        if (!prefab || !cable || routePoints == null || routePoints.Length < 2)
            return;

        var scale = Mathf.Max(0.001f, cable.radiusMeters * 2f);
        _startConnector = CreateEndpointConnector(prefab, endpointA.CableAnchorPosition, ConnectorDirection(endpointA, routePoints[1] - routePoints[0]), scale, "Start Connector");
        _endConnector = CreateEndpointConnector(prefab, endpointB.CableAnchorPosition, -ConnectorDirection(endpointB, routePoints[routePoints.Length - 2] - routePoints[routePoints.Length - 1]), scale, "End Connector");
    }

    private static Vector3 ConnectorDirection(TssPortEndpoint endpoint, Vector3 routeDirection)
    {
        return endpoint.InterfaceFace == EquipmentInterfaceFace.Front
            ? Vector3.back
            : endpoint.InterfaceFace == EquipmentInterfaceFace.Rear
                ? Vector3.forward
                : routeDirection;
    }

    private GameObject CreateEndpointConnector(GameObject prefab, Vector3 position, Vector3 direction, float scale, string label)
    {
        if (direction.sqrMagnitude <= 0.000001f)
            direction = Vector3.forward;

        var normalizedDirection = direction.normalized;
        var up = Mathf.Abs(Vector3.Dot(normalizedDirection, Vector3.up)) > 0.95f ? Vector3.forward : Vector3.up;
        var connector = Instantiate(prefab, position, Quaternion.LookRotation(normalizedDirection, up), transform);
        connector.name = label;
        connector.transform.localScale = Vector3.one * scale;
        foreach (var collider in connector.GetComponentsInChildren<Collider>(true))
            collider.enabled = false;
        return connector;
    }

    private bool BuildRounded(TssCableDefinition cable, TssCableRouteResult route, CableLaneReservation reservation)
    {
        if (!CableCurveBuilder.TryBuild(route, reservation, Settings, out var samples, out var failure))
        {
            Debug.LogWarning($"[TSS Cable] {failure}");
            return false;
        }

        var mesh = BuildTubeMesh(samples, Settings.RadiusMeters, Settings.radialSegments);
        if (!mesh)
            return false;

        var filter = gameObject.AddComponent<MeshFilter>();
        var renderer = gameObject.AddComponent<MeshRenderer>();
        filter.sharedMesh = mesh;
        renderer.sharedMaterial = cable.visualMaterial;
        return true;
    }

    private void BuildLegacy(TssCableDefinition cable, Vector3[] points)
    {
        if (points == null || points.Length < 2)
            return;

        for (var i = 0; i < points.Length - 1; i++)
        {
            if (!IsRenderable(points[i]) || !IsRenderable(points[i + 1]))
                continue;

            var direction = points[i + 1] - points[i];
            var length = direction.magnitude;
            if (length <= 0.001f)
                continue;

            var segment = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            segment.name = $"Segment {i + 1:00}";
            segment.transform.SetParent(transform, false);
            segment.transform.position = (points[i] + points[i + 1]) * 0.5f;
            segment.transform.rotation = Quaternion.FromToRotation(Vector3.up, direction.normalized);
            segment.transform.localScale = new Vector3(cable.radiusMeters * 2f, length * 0.5f, cable.radiusMeters * 2f);
            if (segment.TryGetComponent<Collider>(out var collider))
                DestroyUnityObject(collider);
            if (cable.visualMaterial && segment.TryGetComponent<Renderer>(out var renderer))
                renderer.sharedMaterial = cable.visualMaterial;
        }
    }

    private void OnDestroy()
    {
        if (_laneAllocator != null && !string.IsNullOrWhiteSpace(_connectionId))
            _laneAllocator.Release(_connectionId);
        if (TryGetComponent<MeshFilter>(out var filter) && filter.sharedMesh)
            DestroyUnityObject(filter.sharedMesh);
        DestroyUnityObject(_startConnector);
        DestroyUnityObject(_endConnector);
        _startConnector = null;
        _endConnector = null;
        _laneAllocator = null;
        _connectionId = string.Empty;
    }

    private static Mesh BuildTubeMesh(Vector3[] samples, float radius, int radialSegments)
    {
        if (samples == null || samples.Length < 2)
            return null;

        radialSegments = Mathf.Clamp(radialSegments, 4, 16);
        if (!float.IsFinite(radius) || radius <= 0f || samples.Length > 256)
            return null;
        for (var i = 0; i < samples.Length; i++)
        {
            if (!IsRenderable(samples[i]))
                return null;
            if (i > 0 && (samples[i] - samples[i - 1]).sqrMagnitude <= 0.000001f)
                return null;
        }

        var sideVertexCount = samples.Length * radialSegments;
        var startCap = sideVertexCount;
        var endCap = sideVertexCount + 1;
        var vertices = new Vector3[sideVertexCount + 2];
        var normals = new Vector3[vertices.Length];
        var uvs = new Vector2[vertices.Length];
        var triangles = new int[((samples.Length - 1) * radialSegments + radialSegments) * 6];
        var previousNormal = Vector3.zero;
        var distance = 0f;
        for (var i = 0; i < samples.Length; i++)
        {
            var tangent = Tangent(samples, i);
            var normal = TransportedNormal(tangent, previousNormal);
            previousNormal = normal;
            var binormal = Vector3.Cross(tangent, normal).normalized;
            if (i > 0)
                distance += Vector3.Distance(samples[i - 1], samples[i]);

            for (var ring = 0; ring < radialSegments; ring++)
            {
                var angle = ring * Mathf.PI * 2f / radialSegments;
                var direction = Mathf.Cos(angle) * normal + Mathf.Sin(angle) * binormal;
                var vertexIndex = i * radialSegments + ring;
                vertices[vertexIndex] = samples[i] + direction * radius;
                normals[vertexIndex] = direction;
                uvs[vertexIndex] = new Vector2(ring / (float)radialSegments, distance);
            }
        }

        vertices[startCap] = samples[0];
        normals[startCap] = -Tangent(samples, 0);
        uvs[startCap] = new Vector2(0.5f, 0.5f);
        vertices[endCap] = samples[samples.Length - 1];
        normals[endCap] = Tangent(samples, samples.Length - 1);
        uvs[endCap] = new Vector2(0.5f, 0.5f);

        var triangleIndex = 0;
        for (var i = 0; i < samples.Length - 1; i++)
            for (var ring = 0; ring < radialSegments; ring++)
            {
                var nextRing = (ring + 1) % radialSegments;
                var a = i * radialSegments + ring;
                var b = i * radialSegments + nextRing;
                var c = (i + 1) * radialSegments + nextRing;
                var d = (i + 1) * radialSegments + ring;
                triangles[triangleIndex++] = a;
                triangles[triangleIndex++] = c;
                triangles[triangleIndex++] = b;
                triangles[triangleIndex++] = a;
                triangles[triangleIndex++] = d;
                triangles[triangleIndex++] = c;
            }

        for (var ring = 0; ring < radialSegments; ring++)
        {
            var nextRing = (ring + 1) % radialSegments;
            var startVertex = ring;
            var endVertex = (samples.Length - 1) * radialSegments + ring;
            var endNextVertex = (samples.Length - 1) * radialSegments + nextRing;
            triangles[triangleIndex++] = startCap;
            triangles[triangleIndex++] = nextRing;
            triangles[triangleIndex++] = startVertex;
            triangles[triangleIndex++] = endCap;
            triangles[triangleIndex++] = endVertex;
            triangles[triangleIndex++] = endNextVertex;
        }

        var mesh = new Mesh { name = "TSS Rounded Cable Mesh" };
        mesh.vertices = vertices;
        mesh.normals = normals;
        mesh.uv = uvs;
        mesh.triangles = triangles;
        mesh.RecalculateBounds();
        return mesh;
    }

    private static Vector3 Tangent(Vector3[] samples, int index)
    {
        var previous = samples[Mathf.Max(0, index - 1)];
        var next = samples[Mathf.Min(samples.Length - 1, index + 1)];
        var tangent = (next - previous).normalized;
        return tangent.sqrMagnitude > 0.000001f ? tangent : Vector3.forward;
    }

    private static Vector3 TransportedNormal(Vector3 tangent, Vector3 previousNormal)
    {
        var normal = previousNormal - tangent * Vector3.Dot(previousNormal, tangent);
        if (normal.sqrMagnitude <= 0.000001f)
            normal = Vector3.Cross(tangent, Vector3.up);
        if (normal.sqrMagnitude <= 0.000001f)
            normal = Vector3.Cross(tangent, Vector3.right);
        return normal.normalized;
    }

    private static IEnumerable<string> ConvertKeys(IReadOnlyList<TssCableRouteEdge> edges)
    {
        if (edges == null)
            yield break;
        foreach (var edge in edges)
            yield return edge.Key;
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

    private static bool IsRenderable(Vector3 point)
    {
        return float.IsFinite(point.x) && float.IsFinite(point.y) && float.IsFinite(point.z);
    }
}
