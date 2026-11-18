using UnityEngine;

public sealed class RackInstalledEquipmentTarget : MonoBehaviour
{
    public RackMountController Rack { get; private set; }
    public int StartingU { get; private set; }
    public GameObject Visual { get; private set; }
    private bool _hovered;
    private Renderer[] _bodyRenderers;
    private MaterialPropertyBlock[][] _originalBlocks;

    public void Initialize(RackMountController rack, int startingU, GameObject visual = null)
    {
        Rack = rack;
        StartingU = startingU;
        Visual = visual;
        _bodyRenderers = visual ? System.Array.FindAll(visual.GetComponentsInChildren<Renderer>(true),
            renderer => !renderer.GetComponentInParent<TssPortEndpoint>()) : System.Array.Empty<Renderer>();
    }

    public void SetHovered(bool hovered)
    {
        if (_hovered == hovered || _bodyRenderers == null)
            return;
        _hovered = hovered;
        if (hovered)
            _originalBlocks = new MaterialPropertyBlock[_bodyRenderers.Length][];
        for (var r = 0; r < _bodyRenderers.Length; r++)
        {
            var renderer = _bodyRenderers[r];
            if (!renderer) continue;
            var materials = renderer.sharedMaterials;
            if (hovered)
                _originalBlocks[r] = new MaterialPropertyBlock[materials.Length];
            for (var m = 0; m < materials.Length; m++)
            {
                if (!hovered)
                {
                    renderer.SetPropertyBlock(_originalBlocks[r][m], m);
                    continue;
                }
                var original = new MaterialPropertyBlock();
                renderer.GetPropertyBlock(original, m);
                _originalBlocks[r][m] = original;
                var material = materials[m];
                if (!material) continue;
                var property = material.HasProperty("_BaseColor") ? "_BaseColor" : "_Color";
                if (!material.HasProperty(property)) continue;
                var block = new MaterialPropertyBlock();
                renderer.GetPropertyBlock(block, m);
                block.SetColor(property, Color.Lerp(material.GetColor(property), new Color(0.2f, 0.75f, 1f), 0.45f));
                renderer.SetPropertyBlock(block, m);
            }
        }
    }

    private void OnDisable() => SetHovered(false);

    public void Click()
    {
        if (Rack)
            Rack.ClickInstalled(StartingU, Visual);
    }

}
