using UnityEngine;

public sealed class RackInstalledEquipmentTarget : MonoBehaviour
{
    public RackMountController Rack { get; private set; }
    public int StartingU { get; private set; }

    public void Initialize(RackMountController rack, int startingU)
    {
        Rack = rack;
        StartingU = startingU;
    }

    public void Click()
    {
        if (Rack)
            Rack.ClickInstalled(StartingU);
    }

}
