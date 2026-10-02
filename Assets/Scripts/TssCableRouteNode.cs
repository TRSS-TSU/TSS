using UnityEngine;

public enum TssCableRouteNodeRole
{
    Any,
    RackFront,
    RackRear,
    StationDrop,
    Tray
}

public sealed class TssCableRouteNode : MonoBehaviour
{
    public TssCableRouteNode[] neighbors;
    public string rackId;
    public string stationId;
    public TssCableRouteNodeRole role;

    private void OnDrawGizmos()
    {
        Gizmos.color = new Color(0.15f, 0.85f, 1f, 0.85f);
        Gizmos.DrawSphere(transform.position, 0.08f);

        if (neighbors == null)
            return;

        Gizmos.color = new Color(0.15f, 0.85f, 1f, 0.45f);
        foreach (var neighbor in neighbors)
        {
            if (neighbor)
                Gizmos.DrawLine(transform.position, neighbor.transform.position);
        }
    }

    private void OnDrawGizmosSelected()
    {
        Gizmos.color = Color.yellow;
        Gizmos.DrawWireSphere(transform.position, 0.14f);
    }
}
