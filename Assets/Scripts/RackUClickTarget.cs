using UnityEngine;

public sealed class RackUClickTarget : MonoBehaviour
{
    public RackMountController Rack { get; private set; }
    public int U { get; private set; }

    public void Initialize(RackMountController rack, int u)
    {
        Rack = rack;
        U = u;
    }

    public void Click()
    {
        if (TssRuntimeUi.Instance && TssRuntimeUi.Instance.IsGameplayInputBlocked)
            return;

        if (Rack)
            Rack.ClickU(U);
    }

    private void OnMouseDown()
    {
        Click();
    }
}
