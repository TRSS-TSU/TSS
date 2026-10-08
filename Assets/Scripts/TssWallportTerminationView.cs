using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

public sealed class TssWallportTerminationView : MonoBehaviour
{
    [SerializeField] private bool isPatchPanel;
    [SerializeField] private Sprite wallportSprite;
    [SerializeField] private Sprite patchPanelSprite;
    [SerializeField] private Sprite rearModuleSprite;
    [SerializeField] private Sprite punchToolSprite;

    private static readonly TssConductorColor[] Order =
    {
        TssConductorColor.WhiteOrange, TssConductorColor.Orange,
        TssConductorColor.WhiteGreen, TssConductorColor.Blue,
        TssConductorColor.WhiteBlue, TssConductorColor.Green,
        TssConductorColor.WhiteBrown, TssConductorColor.Brown
    };

    private static readonly string[] Names =
    {
        "W/O", "Orange", "W/G", "Blue", "W/Bl", "Green", "W/Br", "Brown"
    };

    private static readonly Color[] Colors =
    {
        new(0.95f, 0.65f, 0.29f), new(0.96f, 0.38f, 0.10f),
        new(0.49f, 0.83f, 0.44f), new(0.19f, 0.43f, 0.85f),
        new(0.48f, 0.71f, 0.94f), new(0.13f, 0.60f, 0.28f),
        new(0.78f, 0.62f, 0.48f), new(0.47f, 0.29f, 0.18f)
    };

    private readonly List<TssPortEndpoint> _ports = new();
    private readonly Dictionary<int, TssTerminationTerminalState> _pins = new();
    private readonly TssTerminationDraggable[] _wires = new TssTerminationDraggable[8];
    private readonly RectTransform[] _slots = new RectTransform[8];
    private readonly Image[] _slotImages = new Image[8];
    private readonly Text[] _tracker = new Text[8];
    private readonly Vector2[] _bankPositions = new Vector2[8];
    private Button[] _portButtons;

    private Action _close;
    private TssPortEndpoint _endpoint;
    private ScenarioPermanentCableConnection _link;
    private RectTransform _board;
    private Text _title;
    private Text _connection;
    private Text _progress;
    private Text _status;
    private Text _modulePortLabel;
    private TssTerminationDraggable _selected;
    private TssTerminationDraggable _tool;
    private Text _toolLabel;
    private bool _toolSelected;
    private int _nextOrder;

    private void Awake()
    {
        BuildUi();
    }

    public void Bind(TssPortEndpoint source, TssPortEndpoint[] availablePorts, Action close)
    {
        if (_portButtons == null || _portButtons.Length == 0)
            BuildUi();

        _close = close;
        _ports.Clear();
        if (availablePorts != null)
        {
            foreach (var port in availablePorts)
            {
                if (port && (isPatchPanel && port.EndpointId.StartsWith("PatchPanel:", StringComparison.Ordinal)
                    || !isPatchPanel && TssTrainingSession.Instance.TryGetTerminationLinks(port.EndpointId, out _)))
                    _ports.Add(port);
            }
        }

        if (_ports.Count == 0 && source)
            _ports.Add(source);
        if (isPatchPanel)
            _ports.Sort((a, b) => string.CompareOrdinal(a.EndpointId, b.EndpointId));
        if (_ports.Count > _portButtons.Length)
            _ports.RemoveRange(_portButtons.Length, _ports.Count - _portButtons.Length);

        for (var i = 0; i < _portButtons.Length; i++)
        {
            _portButtons[i].gameObject.SetActive(i < _ports.Count);
            if (i < _ports.Count)
            {
                _portButtons[i].interactable = TssTrainingSession.Instance.TryGetTerminationLinks(_ports[i].EndpointId, out _);
                _portButtons[i].GetComponentInChildren<Text>().text = isPatchPanel ? (i + 1).ToString("00") : (i + 1).ToString();
            }
        }

        SelectPort(Mathf.Max(0, _ports.IndexOf(source)));
    }

    private void SelectPort(int index)
    {
        if (index < 0 || index >= _ports.Count)
            return;

        _endpoint = _ports[index];
        _link = null;
        if (TssTrainingSession.Instance.TryGetTerminationLinks(_endpoint.EndpointId, out var links) && links.Count > 0)
            _link = links[0];

        _pins.Clear();
        _nextOrder = 0;
        if (_link != null && TssTrainingSession.Instance.TryGetTerminationModuleState(_link.connectionId, _endpoint.EndpointId, out var saved))
        {
            foreach (var pin in saved.terminals)
            {
                if (pin != null && pin.pin >= 1 && pin.pin <= 8)
                {
                    _pins[pin.pin] = pin.Clone();
                    _nextOrder = Mathf.Max(_nextOrder, pin.operationOrder);
                }
            }
        }

        for (var pin = 1; pin <= 8; pin++)
        {
            if (!_pins.ContainsKey(pin))
                _pins[pin] = new TssTerminationTerminalState { pin = pin };
        }

        _selected = null;
        _toolSelected = false;
        ScrambleBankPositions();
        for (var i = 0; i < _wires.Length; i++)
            _wires[i].SetPlacement(GetStagingPosition(i), WireOrientation(i), false, false);
        _title.text = EndpointLabel(_endpoint.EndpointId);
        var otherEndpoint = _link == null ? null : string.Equals(_endpoint.EndpointId, _link.patchPanelEndpointId, StringComparison.Ordinal)
            ? _link.wallportEndpointId : _link.patchPanelEndpointId;
        _connection.text = otherEndpoint == null ? "No permanent run"
            : $"Distant end: {EndpointLabel(otherEndpoint)}";
        if (_modulePortLabel)
            _modulePortLabel.text = _endpoint.DisplayLabel;
        for (var i = 0; i < _portButtons.Length; i++)
        {
            var wired = _portButtons[i].interactable;
            _portButtons[i].GetComponent<Image>().color = i == index ? new Color(0.10f, 0.70f, 0.39f, 0.57f)
                : isPatchPanel && wired ? new Color(0.15f, 0.31f, 0.32f, 0.90f)
                : isPatchPanel ? new Color(0.08f, 0.11f, 0.12f, 0.25f)
                : new Color(0.04f, 0.08f, 0.10f, 0.32f);
            if (isPatchPanel)
                _portButtons[i].GetComponentInChildren<Text>().color = wired ? Color.white : new Color(0.48f, 0.58f, 0.59f);
        }
        Refresh();
    }

    public void SelectWire(TssTerminationDraggable wire)
    {
        _toolSelected = false;
        _selected = wire;
        Refresh();
    }

    public void ClickWire(TssTerminationDraggable wire)
    {
        if (!_toolSelected)
        {
            SelectWire(wire);
            return;
        }

        for (var pin = 1; pin <= 8; pin++)
        {
            if (_pins[pin].hasConductor && _pins[pin].conductor == wire.Conductor)
            {
                PunchPin(pin);
                return;
            }
        }
    }

    public void ToggleTool()
    {
        _toolSelected = !_toolSelected;
        _selected = null;
        Refresh();
    }

    public void FinishWireDrag(TssTerminationDraggable wire, PointerEventData eventData)
    {
        var pin = FindSlot(eventData);
        if (pin > 0 && !wire.CanEnterBank(IsUpperPin(pin)))
        {
            Refresh();
            _status.text = "Flip the conductor toward this IDC bank";
            return;
        }
        if (pin > 0 && !wire.Terminated && !_pins[pin].isPunched && _link != null)
        {
            for (var i = 1; i <= 8; i++)
            {
                if (i != pin && _pins[i].hasConductor && _pins[i].conductor == wire.Conductor && !_pins[i].isPunched)
                    ClearPin(i);
            }

            var occupant = _pins[pin];
            if (occupant.hasConductor && occupant.conductor != wire.Conductor)
                ClearPin(pin);
            occupant.conductor = wire.Conductor;
            occupant.hasConductor = true;
            occupant.isPunched = false;
            occupant.rotationSteps = wire.RotationSteps;
            occupant.flipped = wire.Flipped;
            occupant.operationOrder = ++_nextOrder;
            Save();
        }
        Refresh();
    }

    private void PunchPin(int pin)
    {
        if (_toolSelected && _link != null && pin > 0 && _pins[pin].hasConductor && !_pins[pin].isPunched)
        {
            _pins[pin].isPunched = true;
            Save();
        }
        Refresh();
    }

    private void ClickSlot(int pin)
    {
        if (_toolSelected)
            PunchPin(pin);
    }

    private static string EndpointLabel(string endpointId)
    {
        if (string.IsNullOrEmpty(endpointId))
            return "Unknown port";

        var patchPanel = endpointId.StartsWith("PatchPanel:", StringComparison.Ordinal);
        foreach (var endpoint in FindObjectsByType<TssPortEndpoint>(FindObjectsInactive.Include, FindObjectsSortMode.None))
        {
            if (endpoint.EndpointId != endpointId)
                continue;

            var label = endpoint.DisplayLabel;
            if (patchPanel)
            {
                var parts = endpointId.Split(':');
                var rack = parts.Length > 1 ? parts[1].Replace("Rack", "Rack ") : "Rack";
                if (label.StartsWith("PP ", StringComparison.Ordinal))
                    label = "Patch Panel " + label.Substring(3);
                return $"{rack} • {label}";
            }

            return $"Wallport • {label}";
        }

        return "Port unavailable";
    }

    private void ClearPin(int pin)
    {
        _pins[pin] = new TssTerminationTerminalState { pin = pin };
    }

    private int FindSlot(PointerEventData eventData)
    {
        for (var i = 0; i < _slots.Length; i++)
        {
            if (RectTransformUtility.RectangleContainsScreenPoint(_slots[i], eventData.position, eventData.pressEventCamera))
                return i + 1;
        }
        return 0;
    }

    private void Save()
    {
        if (_link == null || !_endpoint || !TssTrainingSession.Instance)
            return;

        var terminals = new TssTerminationTerminalState[8];
        for (var i = 0; i < 8; i++)
            terminals[i] = _pins[i + 1].Clone();

        var patch = string.Equals(_endpoint.EndpointId, _link.patchPanelEndpointId, StringComparison.Ordinal);
        var moduleId = patch ? _link.endpointATerminationModuleId : _link.endpointBTerminationModuleId;
        if (string.IsNullOrWhiteSpace(moduleId))
            moduleId = _link.connectionId + (patch ? ":A" : ":B");
        var state = new TssTerminationModuleState
        {
            connectionId = _link.connectionId,
            endpointId = _endpoint.EndpointId,
            moduleId = moduleId,
            terminals = terminals
        };
        if (!TssTrainingSession.Instance.SetTerminationModuleState(state, TssT568BValidator.IsElectricallyCorrect(state), out var message))
            _status.text = message;
    }

    private void ResetPort()
    {
        for (var i = 1; i <= 8; i++)
            ClearPin(i);
        _nextOrder = 0;
        _selected = null;
        _toolSelected = false;
        ScrambleBankPositions();
        for (var i = 0; i < _wires.Length; i++)
            _wires[i].SetPlacement(GetStagingPosition(i), WireOrientation(i), false, false);
        Save();
        Refresh();
    }

    private void ScrambleBankPositions()
    {
        var slots = new int[8];
        for (var i = 0; i < 8; i++)
            slots[i] = i;

        // Fisher-Yates shuffle with a random non-identity permutation
        for (var i = slots.Length - 1; i > 0; i--)
        {
            var j = UnityEngine.Random.Range(0, i + 1);
            (slots[i], slots[j]) = (slots[j], slots[i]);
        }

        for (var i = 0; i < 8; i++)
        {
            var slotIndex = slots[i];
            _bankPositions[i] = new Vector2(370f + slotIndex * 19f, IsUpperPin(i + 1) ? -105f : -305f);
        }
    }

    private Vector2 GetStagingPosition(int wireIndex)
    {
        var wire = _wires[wireIndex];
        var staging = _bankPositions[wireIndex];
        if (wire && wire.CanEnterBank(true))
            staging.y = -105f;
        else if (wire && wire.CanEnterBank(false))
            staging.y = -305f;
        return staging;
    }

    private int WireOrientation(int wireIndex)
    {
        return IsUpperPin(wireIndex + 1) ? 3 : 1;
    }

    private void Refresh()
    {
        var terminated = 0;
        for (var i = 0; i < 8; i++)
        {
            var pin = _pins[i + 1];
            if (pin.isPunched)
                terminated++;

            _slotImages[i].color = pin.isPunched ? new Color(0.10f, 0.85f, 0.42f, 0.55f)
                : pin.hasConductor ? new Color(1f, 0.75f, 0.24f, 0.42f)
                : isPatchPanel ? new Color(0.12f, 0.19f, 0.20f, 0.68f) : new Color(0.6f, 0.9f, 1f, 0.12f);
            _tracker[i].text = $"{i + 1}   {Names[i],-6}   {(pin.isPunched ? "TERMINATED" : pin.hasConductor ? "SLOTTED" : "OPEN")}";
            _tracker[i].color = Color.white;
        }

        for (var i = 0; i < 8; i++)
        {
            var wire = _wires[i];
            var found = false;
            for (var pin = 1; pin <= 8; pin++)
            {
                var state = _pins[pin];
                if (!state.hasConductor || state.conductor != wire.Conductor)
                    continue;
                var slot = _slots[pin - 1];
                wire.SetPlacement(slot.anchoredPosition + new Vector2(14f, -23f), state.rotationSteps, state.flipped, state.isPunched);
                found = true;
                break;
            }
            if (!found)
            {
                var staging = _bankPositions[i];
                if (wire.CanEnterBank(true))
                    staging.y = -105f;
                else if (wire.CanEnterBank(false))
                    staging.y = -305f;
                wire.SetPlacement(staging, wire.RotationSteps, wire.Flipped, false);
            }
            wire.SetSelected(wire == _selected);
        }

        _tool.SetToolSelected(_toolSelected);
        _toolLabel.text = _toolSelected ? "TOOL SELECTED" : "PUNCH TOOL";
        _progress.text = $"{terminated}/8 terminated";
        _status.text = terminated == 8 ? "All pins terminated"
            : _toolSelected ? "Punch tool selected" : "Port in progress";
    }

    private void BuildUi()
    {
        var canvas = gameObject.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.overrideSorting = true;
        canvas.sortingOrder = 500;
        var scaler = gameObject.AddComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1280f, 800f);
        scaler.matchWidthOrHeight = 0.5f;
        gameObject.AddComponent<GraphicRaycaster>();

        var root = GetComponent<RectTransform>();
        root.anchorMin = Vector2.zero;
        root.anchorMax = Vector2.one;
        root.offsetMin = Vector2.zero;
        root.offsetMax = Vector2.zero;
        Panel(root, "Backdrop", Vector2.zero, Vector2.zero, new Color(0.01f, 0.02f, 0.025f, 0.91f), true);
        var page = Panel(root, "Termination Procedure", new Vector2(0f, 0f), new Vector2(1200f, 760f), new Color(0.075f, 0.10f, 0.12f), false, true);
        Label(page, "Header", isPatchPanel ? "PATCH PANEL TERMINATION" : "WALLPORT TERMINATION", new Vector2(24f, -13f), new Vector2(700f, 38f), 26, FontStyle.Bold);
        Button(page, "Quit", new Vector2(1055f, -16f), new Vector2(115f, 34f), () => _close?.Invoke());

        var selection = Panel(page, isPatchPanel ? "Patch panel selection" : "Wallport selection", new Vector2(24f, -64f), new Vector2(1152f, 272f), new Color(0.12f, 0.16f, 0.18f));
        var photo = Panel(selection, "Rear reference", new Vector2(13f, -12f), isPatchPanel ? new Vector2(330f, 248f) : new Vector2(175f, 248f), Color.white);
        var photoImage = photo.GetComponent<Image>();
        photoImage.sprite = isPatchPanel ? patchPanelSprite : wallportSprite;
        photoImage.preserveAspect = true;
        _portButtons = new Button[isPatchPanel ? 48 : 4];
        for (var i = 0; i < _portButtons.Length; i++)
        {
            var captured = i;
            var position = isPatchPanel
                ? new Vector2(360f + (23 - i % 24) * 31.5f, i < 24 ? -166f : -212f)
                : new Vector2(49f + i % 2 * 52f, i < 2 ? -87f : -170f);
            _portButtons[i] = Button(selection, "Port " + (i + 1), position, isPatchPanel ? new Vector2(27f, 30f) : new Vector2(36f, 37f), () => SelectPort(captured));
            if (isPatchPanel)
                _portButtons[i].GetComponentInChildren<Text>().fontSize = 12;
        }
        var infoX = isPatchPanel ? 360f : 250f;
        var infoWidth = isPatchPanel ? 760f : 840f;
        Label(selection, "Selected", "SELECTED PORT", new Vector2(infoX, isPatchPanel ? -14f : -22f), new Vector2(infoWidth, 28f), 14, FontStyle.Bold, new Color(0.55f, 0.81f, 0.71f));
        _title = Label(selection, "Port label", "", new Vector2(infoX, isPatchPanel ? -43f : -54f), new Vector2(infoWidth, 48f), isPatchPanel ? 24 : 27, FontStyle.Bold);
        _connection = Label(selection, "Connection", "", new Vector2(infoX, isPatchPanel ? -83f : -113f), new Vector2(infoWidth, isPatchPanel ? 39f : 64f), 17);
        _progress = Label(selection, "Progress", "", new Vector2(infoX, isPatchPanel ? -126f : -201f), new Vector2(infoWidth, 34f), 19, FontStyle.Bold);

        _board = Panel(page, "Conductor placement", new Vector2(24f, -354f), new Vector2(790f, 378f), new Color(0.12f, 0.16f, 0.18f));
        Label(_board, "Board title", isPatchPanel ? "PATCH PANEL PUNCH-DOWN" : "WALLPORT PUNCH-DOWN", new Vector2(19f, -12f), new Vector2(400f, 28f), 18, FontStyle.Bold);
        Button(_board, "Flip", new Vector2(585f, -12f), new Vector2(68f, 29f), () => { if (_selected) { _selected.Flip(); RememberSelectedOrientation(); Refresh(); } });
        Panel(_board, "Tool dock", new Vector2(626f, -293f), new Vector2(145f, 66f), new Color(0.28f, 0.37f, 0.39f));
        _tool = Draggable(_board, "Punch tool", new Vector2(635f, -297f), new Vector2(126f, 42f), Color.white, string.Empty, TssConductorColor.None, true);
        if (punchToolSprite)
            _tool.GetComponent<Image>().sprite = punchToolSprite;
        _toolLabel = Label(_board, "Tool label", "PUNCH TOOL", new Vector2(648f, -337f), new Vector2(120f, 20f), 12, FontStyle.Bold);
        var pins = isPatchPanel ? new[] { 8, 7, 2, 1, 6, 3, 4, 5 } : new[] { 8, 7, 2, 1, 4, 5, 3, 6 };
        var module = Panel(_board, "Rear module", new Vector2(8f, -65f), new Vector2(350f, 273f), isPatchPanel ? new Color(0.91f, 0.93f, 0.92f) : Color.white);
        if (isPatchPanel)
        {
            Panel(_board, "Upper IDC rail", new Vector2(32f, -87f), new Vector2(294f, 71f), new Color(0.70f, 0.76f, 0.76f));
            Panel(_board, "Lower IDC rail", new Vector2(32f, -245f), new Vector2(294f, 71f), new Color(0.70f, 0.76f, 0.76f));
            _modulePortLabel = Label(_board, "Module port", "", new Vector2(38f, -175f), new Vector2(280f, 31f), 20, FontStyle.Bold, new Color(0.10f, 0.17f, 0.19f));
            _modulePortLabel.alignment = TextAnchor.MiddleCenter;
            var wiringKey = Label(_board, "Wiring key", "T568B", new Vector2(117f, -211f), new Vector2(120f, 21f), 13, FontStyle.Bold, new Color(0.18f, 0.28f, 0.29f));
            wiringKey.alignment = TextAnchor.MiddleCenter;
        }
        else
            module.GetComponent<Image>().sprite = rearModuleSprite;
        module.GetComponent<Image>().raycastTarget = false;
        var notchX = isPatchPanel ? new[] { 55f, 105f, 205f, 255f } : new[] { 94f, 129f, 208f, 241f };
        for (var i = 0; i < 8; i++)
        {
            var pin = pins[i];
            var x = notchX[i % 4];
            var y = isPatchPanel ? i < 4 ? -95f : -250f : i < 4 ? -106f : -245f;
            _slots[pin - 1] = Panel(_board, "Pin " + pin, new Vector2(x, y), new Vector2(29f, 46f), new Color(0.6f, 0.9f, 1f, 0.12f));
            _slotImages[pin - 1] = _slots[pin - 1].GetComponent<Image>();
            var slotButton = _slots[pin - 1].gameObject.AddComponent<Button>();
            slotButton.targetGraphic = _slotImages[pin - 1];
            slotButton.onClick.AddListener(() => ClickSlot(pin));
            var pinLabel = Label(_slots[pin - 1], "Pin label", pin.ToString(), new Vector2(0f, -2f), new Vector2(29f, 20f), 15, FontStyle.Bold, new Color(0.95f, 1f, 0.77f));
            pinLabel.alignment = TextAnchor.MiddleCenter;
        }
        Panel(_board, "Cable sheath", new Vector2(586f, -145f), new Vector2(185f, 112f), new Color(0.12f, 0.33f, 0.43f));
        Label(_board, "Cable label", "CABLE", new Vector2(625f, -180f), new Vector2(120f, 36f), 18, FontStyle.Bold, new Color(0.73f, 0.87f, 0.90f));
        ScrambleBankPositions();
        for (var i = 0; i < 8; i++)
        {
            _wires[i] = Draggable(_board, Names[i], _bankPositions[i], new Vector2(28f, 28f), Colors[i], string.Empty, Order[i], false);
            _wires[i].SetPlacement(GetStagingPosition(i), WireOrientation(i), false, false);
        }

        var tracker = Panel(page, "Tracker", new Vector2(830f, -354f), new Vector2(346f, 378f), new Color(0.12f, 0.16f, 0.18f));
        Label(tracker, "Tracker title", "PIN TRACKER  ·  T568B", new Vector2(19f, -12f), new Vector2(320f, 28f), 18, FontStyle.Bold);
        for (var i = 0; i < 8; i++)
            _tracker[i] = Label(tracker, "Pin " + (i + 1), "", new Vector2(20f, -55f - i * 30f), new Vector2(310f, 28f), 15);
        Button(tracker, "Reset port", new Vector2(19f, -326f), new Vector2(138f, 34f), ResetPort);
        _status = Label(tracker, "Status", "", new Vector2(20f, -293f), new Vector2(307f, 31f), 13);
    }

    private TssTerminationDraggable Draggable(RectTransform parent, string name, Vector2 pos, Vector2 size, Color color, string label, TssConductorColor conductor, bool tool)
    {
        var rect = Panel(parent, name, pos, size, tool ? color : new Color(0f, 0f, 0f, 0f));
        if (!tool)
            rect.pivot = new Vector2(0.5f, 0.5f);
        var drag = rect.gameObject.AddComponent<TssTerminationDraggable>();
        var wireIndex = (int)conductor - 1;
        var lane = wireIndex < 2 ? wireIndex : wireIndex < 6 ? wireIndex - 2 : wireIndex - 4;
        var cableY = IsUpperPin(wireIndex + 1) ? -155f - lane * 10f : -205f - lane * 10f;
        drag.Bind(this, _board, pos, conductor, tool, color, new Vector2(586f, cableY));
        if (tool && !punchToolSprite)
        {
            var text = Label(rect, "Label", label, new Vector2(3f, -2f), size - new Vector2(6f, 4f), 14, FontStyle.Bold, new Color(0.06f, 0.08f, 0.09f));
            text.raycastTarget = false;
        }
        return drag;
    }

    private void RememberSelectedOrientation()
    {
        for (var pin = 1; pin <= 8; pin++)
        {
            var state = _pins[pin];
            if (!state.hasConductor || state.conductor != _selected.Conductor || state.isPunched)
                continue;
            if (!_selected.CanEnterBank(IsUpperPin(pin)))
                ClearPin(pin);
            else
            {
                state.rotationSteps = _selected.RotationSteps;
                state.flipped = _selected.Flipped;
            }
            Save();
            break;
        }
    }

    private bool IsUpperPin(int pin)
    {
        return pin <= 2 || pin >= 7;
    }

    private static RectTransform Panel(RectTransform parent, string name, Vector2 pos, Vector2 size, Color color, bool stretch = false, bool center = false)
    {
        var go = new GameObject(name, typeof(RectTransform), typeof(Image));
        var rect = go.GetComponent<RectTransform>();
        rect.SetParent(parent, false);
        rect.anchorMin = stretch ? Vector2.zero : center ? new Vector2(0.5f, 0.5f) : new Vector2(0f, 1f);
        rect.anchorMax = stretch ? Vector2.one : center ? new Vector2(0.5f, 0.5f) : new Vector2(0f, 1f);
        rect.pivot = center ? new Vector2(0.5f, 0.5f) : new Vector2(0f, 1f);
        rect.anchoredPosition = pos;
        rect.sizeDelta = stretch ? Vector2.zero : size;
        go.GetComponent<Image>().color = color;
        return rect;
    }

    private static Text Label(RectTransform parent, string name, string value, Vector2 pos, Vector2 size, int fontSize, FontStyle style = FontStyle.Normal, Color? color = null)
    {
        var go = new GameObject(name, typeof(RectTransform), typeof(Text));
        var rect = go.GetComponent<RectTransform>();
        rect.SetParent(parent, false);
        rect.anchorMin = rect.anchorMax = new Vector2(0f, 1f);
        rect.pivot = new Vector2(0f, 1f);
        rect.anchoredPosition = pos;
        rect.sizeDelta = size;
        var text = go.GetComponent<Text>();
        text.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        text.text = value;
        text.fontSize = fontSize;
        text.fontStyle = style;
        text.alignment = TextAnchor.MiddleLeft;
        text.color = color ?? Color.white;
        text.horizontalOverflow = HorizontalWrapMode.Wrap;
        return text;
    }

    private static Button Button(RectTransform parent, string title, Vector2 pos, Vector2 size, Action click)
    {
        var rect = Panel(parent, title, pos, size, new Color(0.28f, 0.36f, 0.39f));
        var button = rect.gameObject.AddComponent<Button>();
        button.targetGraphic = rect.GetComponent<Image>();
        button.onClick.AddListener(() => click());
        var text = Label(rect, "Text", title, Vector2.zero, size, 16, FontStyle.Bold);
        text.alignment = TextAnchor.MiddleCenter;
        text.raycastTarget = false;
        return button;
    }
}
