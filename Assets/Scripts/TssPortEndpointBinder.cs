using UnityEngine;

public static class TssPortEndpointBinder
{
    public static void ConfigureEndpoints(GameObject root, string ownerPrefix, EquipmentDefinition equipment, string deviceName = null)
    {
        if (!root || string.IsNullOrWhiteSpace(ownerPrefix) || !equipment || equipment.interfaces == null)
            return;

        var configured = new System.Collections.Generic.List<TssPortEndpoint>();
        foreach (var port in equipment.interfaces)
        {
            if (port == null || string.IsNullOrWhiteSpace(port.name))
                continue;

            var anchor = FindAnchor(root.transform, port);
            if (!anchor)
                continue;

            var endpoint = anchor.GetComponent<TssPortEndpoint>();
            if (!endpoint)
                endpoint = anchor.gameObject.AddComponent<TssPortEndpoint>();

            endpoint.Configure($"{ownerPrefix}:{port.name.Trim()}", port, deviceName);
            configured.Add(endpoint);
        }

        foreach (var consumer in root.GetComponentsInChildren<ITssEndpointBindingConsumer>(true))
            consumer.OnEndpointsBound(configured, ownerPrefix);
    }

    public static TssPortEndpoint FindMatchingEndpoint(System.Collections.Generic.IReadOnlyList<TssPortEndpoint> endpoints, TssPortEndpoint serialized, string endpointId)
    {
        if (serialized)
        {
            if (endpoints == null)
                return serialized;
            foreach (var endpoint in endpoints)
                if (endpoint == serialized)
                    return serialized;
        }

        if (string.IsNullOrWhiteSpace(endpointId) || endpoints == null)
            return null;

        var suffix = endpointId.Trim();
        foreach (var endpoint in endpoints)
            if (endpoint && (endpoint.EndpointId == suffix || endpoint.EndpointId.EndsWith(":" + suffix, System.StringComparison.Ordinal)))
                return endpoint;

        return null;
    }

    private static Transform FindAnchor(Transform root, EquipmentInterface port)
    {
        if (!root)
            return null;

        if (!string.IsNullOrWhiteSpace(port.portAnchorPath))
        {
            var anchor = root.Find(port.portAnchorPath);
            if (anchor)
                return anchor;
        }

        foreach (var endpoint in root.GetComponentsInChildren<TssPortEndpoint>(true))
        {
            if (endpoint.name == port.name || endpoint.DisplayLabel == port.label)
                return endpoint.transform;
        }

        return null;
    }
}

public interface ITssEndpointBindingConsumer
{
    void OnEndpointsBound(System.Collections.Generic.IReadOnlyList<TssPortEndpoint> endpoints, string ownerPrefix);
}
