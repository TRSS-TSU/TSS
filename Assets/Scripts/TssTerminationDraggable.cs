using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

public sealed class TssTerminationDraggable : MonoBehaviour, IBeginDragHandler, IDragHandler, IEndDragHandler, IPointerClickHandler
{
    private TssWallportTerminationView _view;
    private RectTransform _board;
    private RectTransform _rect;
    private Image[] _strand;
    private Image[] _stripe;
    private Image _end;
    private Vector2 _home;
    private Vector2 _cableEnd;
    private bool _tool;
    private bool _terminated;
    private Color _color;

    public TssConductorColor Conductor { get; private set; }
    public int RotationSteps { get; private set; }
    public bool Flipped { get; private set; }
    public bool Terminated => _terminated;
    public bool CanEnterBank(bool upper) => RotationSteps % 2 == 1 && ((RotationSteps == 3) != Flipped) == upper;

    public void Bind(TssWallportTerminationView view, RectTransform board, Vector2 home, TssConductorColor conductor, bool tool, Color color, Vector2 cableEnd)
    {
        _view = view;
        _board = board;
        _home = home;
        _cableEnd = cableEnd;
        _color = color;
        Conductor = conductor;
        _tool = tool;
        _rect = GetComponent<RectTransform>();
        if (_tool)
            return;

        _strand = new Image[2];
        _stripe = new Image[2];
        var striped = (int)conductor % 2 == 1;
        for (var i = 0; i < 2; i++)
        {
            _strand[i] = Segment("Strand " + i, striped ? new Color(0.95f, 0.96f, 0.93f) : color);
            if (striped)
                _stripe[i] = Segment("Stripe " + i, color);
        }
        _end = Segment("Free end", color);
        UpdateStrand();
    }

    public void OnPointerClick(PointerEventData eventData)
    {
        if (_tool)
            _view.ToggleTool();
        else if (!_terminated)
            _view.ClickWire(this);
    }

    public void OnBeginDrag(PointerEventData eventData)
    {
        if (_terminated)
            return;
        if (_tool)
            return;
        _view.SelectWire(this);
        transform.SetAsLastSibling();
    }

    public void OnDrag(PointerEventData eventData)
    {
        if (_terminated || _tool)
            return;
        if (RectTransformUtility.ScreenPointToLocalPointInRectangle(_board, eventData.position, eventData.pressEventCamera, out var local))
        {
            _rect.anchoredPosition = local;
            UpdateStrand();
        }
    }

    public void OnEndDrag(PointerEventData eventData)
    {
        if (_terminated || _tool)
            return;
        _view.FinishWireDrag(this, eventData);
    }

    public void Flip()
    {
        if (_terminated)
            return;
        Flipped = !Flipped;
        UpdateStrand();
    }

    public void SetPlacement(Vector2 position, int rotationSteps, bool flipped, bool terminated)
    {
        _terminated = terminated;
        RotationSteps = rotationSteps;
        Flipped = flipped;
        _rect.anchoredPosition = position;
        UpdateStrand();
    }

    public void SetSelected(bool selected)
    {
        if (_tool)
            return;
        _end.color = selected ? new Color(1f, 0.92f, 0.45f) : _color;
    }

    public void SetToolSelected(bool selected)
    {
        if (_tool)
            GetComponent<Image>().color = selected ? new Color(0.55f, 1f, 0.73f) : Color.white;
    }

    private Image Segment(string name, Color color)
    {
        var go = new GameObject(name, typeof(RectTransform), typeof(Image));
        var rect = go.GetComponent<RectTransform>();
        rect.SetParent(_board, false);
        rect.anchorMin = rect.anchorMax = new Vector2(0f, 1f);
        rect.pivot = new Vector2(0f, 0.5f);
        var image = go.GetComponent<Image>();
        image.color = color;
        image.raycastTarget = false;
        return image;
    }

    private void UpdateStrand()
    {
        if (_tool || _strand == null)
            return;

        var tip = _rect.anchoredPosition;
        var points = new[] { _cableEnd, new Vector2(tip.x, _cableEnd.y), tip };
        for (var i = 0; i < 2; i++)
        {
            Draw(_strand[i], points[i], points[i + 1], 7f);
            if (_stripe[i])
                Draw(_stripe[i], points[i], points[i + 1], 3f);
        }

        var direction = (Vector2)(Quaternion.Euler(0f, 0f, RotationSteps * 90f) * Vector2.left) * (Flipped ? -1f : 1f);
        Draw(_end, tip, tip + direction * (_terminated ? 4f : 56f), 7f);
    }

    private static void Draw(Image image, Vector2 from, Vector2 to, float thickness)
    {
        var rect = image.rectTransform;
        rect.anchoredPosition = from;
        rect.sizeDelta = new Vector2(Vector2.Distance(from, to), thickness);
        rect.localRotation = Quaternion.Euler(0f, 0f, Mathf.Atan2(to.y - from.y, to.x - from.x) * Mathf.Rad2Deg);
    }
}
