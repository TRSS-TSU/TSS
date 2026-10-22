using UnityEngine.EventSystems;

public sealed class TssWorkPackageSelectorPointer : UIBehaviour, IPointerEnterHandler
{
    private TssWorkPackageShowcase showcase;
    private int index;

    public void Initialize(TssWorkPackageShowcase owner, int workPackageIndex)
    {
        showcase = owner;
        index = workPackageIndex;
    }

    public void OnPointerEnter(PointerEventData eventData)
    {
        if (showcase) showcase.SelectWorkPackage(index);
    }
}
