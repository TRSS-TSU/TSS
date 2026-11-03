using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using UnityEditor;
using UnityEngine;

public sealed class Tss2NetworkDiagnosticsWindow : EditorWindow
{
    private enum Tab { Summary, Cabling, Devices, Endpoints, Probe }
    private const double RefreshInterval = 0.25;

    private TssTrainingSession _session;
    private TssTrainingSession _subscribed;
    private Tab _tab;
    private Vector2 _scroll;
    
    private GUIStyle _detailStyle;
    private GUIStyle _detailSubStyle;
    private GUISkin _windowSkin;
    private Vector2 _detailScroll;
    private bool _autoRefresh = true;
    private bool _eventLogging = true;
    private bool _showControls;
    private string _selectedDevice;
    private string _selectedEndpoint;
    private string _probeDestination;
    private int _probeSource;
    private string _probeText = "No probe run.";
    private readonly List<string> _log = new();
    private double _nextRefresh;
    private DateTime _lastRefresh;
    private DateTime _lastStateChange;
    private bool _stale;

    [MenuItem("TSS/Diagnostics/Network Diagnostics")]
    public static void Open() => GetWindow<Tss2NetworkDiagnosticsWindow>("TSS2 Network Diagnostics");

    private void OnEnable()
    {
        _nextRefresh = 0;
        EditorApplication.update += OnEditorUpdate;
        RefreshSession();
    }

    private void OnDisable()
    {
        EditorApplication.update -= OnEditorUpdate;
        Unsubscribe();
    }

    private void OnEditorUpdate()
    {
        if (!_autoRefresh || EditorApplication.timeSinceStartup < _nextRefresh)
            return;
        _nextRefresh = EditorApplication.timeSinceStartup + RefreshInterval;
        var current = Application.isPlaying ? TssTrainingSession.Instance : null;
        if (current != _session)
            RefreshSession();
        else if (_session == null)
            Repaint();
    }

    private void RefreshSession()
    {
        var current = Application.isPlaying
            ? TssTrainingSession.Instance ?? FindFirstObjectByType<TssTrainingSession>()
            : null;
        if (current != _session)
        {
            Unsubscribe();
            _session = current;
            if (_session)
            {
                _subscribed = _session;
                _subscribed.StateChanged += OnStateChanged;
                AddLog("SessionAttached", "Play Mode session attached.");
            }
            else
            {
                _stale = false;
                AddLog("SessionDetached", "No active Play Mode session.");
            }
        }

        _stale = _session && !Application.isPlaying;
        _lastRefresh = DateTime.Now;
        Repaint();
    }

    private void Unsubscribe()
    {
        if (_subscribed)
            _subscribed.StateChanged -= OnStateChanged;
        _subscribed = null;
    }

    private void OnStateChanged()
    {
        _lastStateChange = DateTime.Now;
        _stale = false;
        AddLog("StateChanged", "Network/session state notification received.");
        Repaint();
    }

    private void AddLog(string kind, string message)
    {
        if (!_eventLogging)
            return;
        _log.Add($"{DateTime.Now:HH:mm:ss}  {kind,-16} {message}");
        if (_log.Count > 100)
            _log.RemoveAt(0);
    }

private void OnGUI()
    {
        var previousSkin = GUI.skin;
        var labelSize = EditorStyles.label.fontSize;
        var boldSize = EditorStyles.boldLabel.fontSize;
        var miniLabelSize = EditorStyles.miniLabel.fontSize;
        var miniButtonSize = EditorStyles.miniButton.fontSize;
        var toolbarButtonSize = EditorStyles.toolbarButton.fontSize;

        if (!_windowSkin)
        {
            _windowSkin = Instantiate(GUI.skin);
            _windowSkin.label.fontSize = 20;
            _windowSkin.button.fontSize = 18;
            _windowSkin.toggle.fontSize = 18;
            _windowSkin.textField.fontSize = 18;
            _windowSkin.textArea.fontSize = 18;
            _windowSkin.box.fontSize = 15;
        }

        EditorStyles.label.fontSize = 20;
        EditorStyles.boldLabel.fontSize = 20;
        EditorStyles.miniLabel.fontSize = 20;
        EditorStyles.miniButton.fontSize = 18;
        EditorStyles.toolbarButton.fontSize = 18;
        GUI.skin = _windowSkin;
        try
        {
            RefreshSession();
            DrawHeader();
            if (!_session)
            {
                EditorGUILayout.HelpBox(Application.isPlaying
                    ? "No active TssTrainingSession was found."
                    : "No active Play Mode session. Enter Play Mode to inspect live network state.",
                    MessageType.Info);
                DrawLog();
                return;
            }

            _tab = (Tab)GUILayout.Toolbar((int)_tab, new[] { "Summary", "Cabling", "Devices", "Endpoints", "Probe" });
            _scroll = EditorGUILayout.BeginScrollView(_scroll);
            switch (_tab)
            {
                case Tab.Cabling: DrawCabling(); break;
                case Tab.Devices: DrawDevices(); break;
                case Tab.Endpoints: DrawEndpoints(); break;
                case Tab.Probe: DrawProbe(); break;
                default: DrawSummary(); break;
            }
            EditorGUILayout.EndScrollView();
            DrawLog();
        }
        finally
        {
            EditorStyles.label.fontSize = labelSize;
            EditorStyles.boldLabel.fontSize = boldSize;
            EditorStyles.miniLabel.fontSize = miniLabelSize;
            EditorStyles.miniButton.fontSize = miniButtonSize;
            EditorStyles.toolbarButton.fontSize = toolbarButtonSize;
            GUI.skin = previousSkin;
        }
    }

    private void DrawHeader()
    {
        using (new EditorGUILayout.HorizontalScope(EditorStyles.toolbar))
        {
            var state = _session ? (Application.isPlaying ? "PLAY MODE" : "STALE") : "EDIT MODE";
            GUILayout.Label($"TSS2 Network Diagnostics  [{state}]", EditorStyles.boldLabel);
            GUILayout.FlexibleSpace();
            if (GUILayout.Button("Refresh", EditorStyles.toolbarButton, GUILayout.Width(60)))
            {
                AddLog("Refresh", "Manual projection refresh.");
                RefreshSession();
            }
            if (GUILayout.Button("Clear log", EditorStyles.toolbarButton, GUILayout.Width(65)))
                _log.Clear();
        }

        var scenario = _session?.Scenario;
        var name = scenario ? $"{scenario.displayName} ({scenario.scenarioId})" : "none";
        EditorGUILayout.LabelField($"Scenario: {name}   Student: {_session?.StudentId ?? "—"}");
        EditorGUILayout.LabelField($"Last refresh: {_lastRefresh:HH:mm:ss}   Last state change: {(_lastStateChange == default ? "—" : _lastStateChange.ToString("HH:mm:ss"))}");
        if (_stale)
            EditorGUILayout.HelpBox("This is a final snapshot; the live session is no longer available.", MessageType.Warning);
    }

    private void DrawSummary()
    {
        var state = _session.NetworkState;
        if (state == null)
        {
            EditorGUILayout.HelpBox("NetworkState is unavailable for this scenario.", MessageType.Warning);
            return;
        }

        var devices = state.Devices.OrderBy(x => x.Key, StringComparer.OrdinalIgnoreCase).ToList();
        var endpoints = state.Endpoints.OrderBy(x => x.Key, StringComparer.OrdinalIgnoreCase).ToList();
        var paths = _session.GetEffectivePhysicalPaths();
        EditorGUILayout.LabelField("Session", EditorStyles.boldLabel);
        EditorGUILayout.LabelField($"Installed equipment: {_session.Installed.Count}   Devices: {devices.Count}   Endpoints: {endpoints.Count}");
        EditorGUILayout.LabelField($"Player connections: {_session.PhysicalConnections.Count(x => !x.IsPermanent)}   Permanent: {_session.PhysicalConnections.Count(x => x.IsPermanent)}   Effective paths: {paths.Count}");
        EditorGUILayout.LabelField($"Evidence: {state.Evidence.Count}   Provider demarc: {Flag(state.ProviderDemarcConnected)}");
        EditorGUILayout.LabelField($"AP uplink: {Flag(state.AccessPoint.UplinkConnected)}   configured: {Flag(state.AccessPoint.Configured)}   association: {state.AccessPoint.AssociatedEndpointKey ?? "none"}");

        EditorGUILayout.Space(6);
        EditorGUILayout.LabelField("Work packages", EditorStyles.boldLabel);
        DrawWorkPackage("WP2 Layer 2", new TssNetworkWorkPackageEvaluator(state).EvaluateLayer2());
        DrawWorkPackage("WP3 Layer 3", new TssNetworkWorkPackageEvaluator(state).EvaluateLayer3());
        DrawWorkPackage("WP4 Layer 4", new TssNetworkWorkPackageEvaluator(state).EvaluateLayer4());

        _showControls = EditorGUILayout.Foldout(_showControls, "Developer controls", true);
        if (_showControls)
        {
            _autoRefresh = EditorGUILayout.Toggle("Auto-refresh", _autoRefresh);
            _eventLogging = EditorGUILayout.Toggle("Event logging", _eventLogging);
            if (GUILayout.Button("Copy current summary"))
                CopyText(BuildSummaryText(state));
        }
    }

    private void DrawWorkPackage(string title, TssWorkPackageResult result)
    {
        var status = result == null ? "NOT AVAILABLE" : result.Complete ? "COMPLETE" : $"INCOMPLETE ({result.UnmetCategories.Count} unmet)";
        EditorGUILayout.LabelField(title, status);
        if (result == null || result.Complete)
            return;
        foreach (var condition in result.UnmetCategories.OrderBy(x => x.Id, StringComparer.OrdinalIgnoreCase))
            EditorGUILayout.LabelField($"  {condition.Id}: {condition.Label}", EditorStyles.miniLabel);
    }

private void DrawCabling()
    {
        EditorGUILayout.LabelField("Connections", EditorStyles.boldLabel);
        foreach (var c in _session.PhysicalConnections.OrderBy(x => x.ConnectionId, StringComparer.OrdinalIgnoreCase))
            EditorGUILayout.LabelField($"{c.ConnectionId}  {(c.IsPermanent ? "PERMANENT" : "PLAYER")}  {EndpointDisplay(c.EndpointAId)} <-> {EndpointDisplay(c.EndpointBId)}  {c.CableType}  visual:{(c.Visual ? "yes" : "no")}");

        EditorGUILayout.Space(6);
        EditorGUILayout.LabelField("Cable requirement status", EditorStyles.boldLabel);
        foreach (var s in _session.GetCableTypeStatuses().OrderBy(x => x.ConnectionId, StringComparer.OrdinalIgnoreCase))
            EditorGUILayout.LabelField($"{s.ConnectionId}  {EndpointDisplay(s.EndpointAId)} <-> {EndpointDisplay(s.EndpointBId)}  {s.ActualCableType} / required {s.RequiredCableType}  connected:{Flag(s.IsConnected)}  correct:{Flag(s.IsCorrect)}");

        EditorGUILayout.Space(6);
        EditorGUILayout.LabelField("Effective paths (evaluated Layer 1)", EditorStyles.boldLabel);
        var paths = _session.GetEffectivePhysicalPaths();
        if (paths.Count == 0) EditorGUILayout.LabelField("No effective paths.");
        foreach (var p in paths.OrderBy(x => x.EndpointAId, StringComparer.OrdinalIgnoreCase).ThenBy(x => x.EndpointBId, StringComparer.OrdinalIgnoreCase))
            EditorGUILayout.LabelField($"{EndpointDisplay(p.EndpointAId)} <-> {EndpointDisplay(p.EndpointBId)}");
    }

private void DrawDevices()
    {
        var state = _session.NetworkState;
        if (state == null) return;
        var devices = state.Devices.OrderBy(x => x.Key, StringComparer.OrdinalIgnoreCase).ToList();
        EditorGUILayout.LabelField("Devices", EditorStyles.boldLabel);
        foreach (var d in devices)
        {
            if (GUILayout.Button($"{d.Key}  {d.Hostname}  [{d.Category}]  role:{d.ExpectedRole}  ifaces:{d.Interfaces.Count} VLANs:{d.Vlans.Count} routes:{d.Routes.Count} evidence:{d.Evidence.Count}", EditorStyles.miniButton))
                _selectedDevice = string.Equals(_selectedDevice, d.Key, StringComparison.OrdinalIgnoreCase) ? null : d.Key;
        }

        if (!state.TryGetDevice(_selectedDevice, out var selected))
            return;
        EditorGUILayout.Space(6);
        EditorGUILayout.LabelField($"Selected: {selected.Key} / {selected.Hostname}", EditorStyles.boldLabel);

        _detailStyle ??= new GUIStyle(EditorStyles.label) { fontSize = 18, wordWrap = false };
        _detailStyle.normal.textColor = Color.white;
        _detailStyle.focused.textColor = Color.white;
        _detailStyle.hover.textColor = Color.white;
        _detailStyle.active.textColor = Color.white;
        _detailSubStyle ??= new GUIStyle(EditorStyles.miniLabel) { fontSize = 14, wordWrap = false };
        _detailSubStyle.normal.textColor = Color.white;
        _detailSubStyle.focused.textColor = Color.white;
        _detailSubStyle.hover.textColor = Color.white;
        _detailSubStyle.active.textColor = Color.white;
        _detailScroll = EditorGUILayout.BeginScrollView(_detailScroll, GUILayout.Height(500f));
        var interfaces = selected.Interfaces.Values.AsEnumerable();
        if (selected.Category == EquipmentCategory.Switch)
            interfaces = interfaces.OrderBy(x => InterfaceNumber(x.Name)).ThenBy(x => x.Name, StringComparer.OrdinalIgnoreCase);
        else
            interfaces = interfaces.OrderBy(x => x.Name, StringComparer.OrdinalIgnoreCase);

        foreach (var iface in interfaces)
        {
            var l1 = iface.AdminUp && iface.CarrierUp;
            var l2 = state.IsEndpointLogicalLayer2Available(_session, iface.EndpointId);
            var l3 = state.IsEndpointIpRoutingAvailable(iface.EndpointId);
            EditorGUILayout.LabelField($"{iface.Name}  endpoint:{EndpointDisplay(iface.EndpointId)}  admin:{Flag(iface.AdminUp)} carrier:{Flag(iface.CarrierUp)}  L1:{Flag(l1)} L2:{Flag(l2)} L3:{Flag(l3)}", _detailStyle);
            EditorGUILayout.LabelField($"  {(iface.IsTrunk ? "trunk" : "access")} accessVlan:{iface.AccessVlan} native:{iface.NativeVlan} allowed:{string.Join(",", (iface.AllowedVlans ?? new HashSet<int>()).OrderBy(x => x))}  addr:{iface.Address}/{iface.Mask} dot1q:{iface.Dot1qVlan}", _detailSubStyle);
            EditorGUILayout.Space(4f);
        }
        EditorGUILayout.EndScrollView();
    }

    private static int InterfaceNumber(string name)
    {
        var slash = (name ?? string.Empty).LastIndexOf('/');
        return slash >= 0 && int.TryParse(name.Substring(slash + 1), out var number) ? number : int.MaxValue;
    }

    private string EndpointDisplay(string endpointId)
    {
        var friendly = TssPhysicalConnectionPanel.EndpointLabel(endpointId, _session);
        if (string.IsNullOrWhiteSpace(friendly) || string.Equals(friendly, endpointId, StringComparison.OrdinalIgnoreCase))
            return endpointId ?? string.Empty;
        return $"{friendly} [{endpointId}]";
    }


    private void DrawEndpoints()
    {
        var state = _session.NetworkState;
        if (state == null) return;
        EditorGUILayout.LabelField("Endpoints (live values)", EditorStyles.boldLabel);
        foreach (var e in state.Endpoints.OrderBy(x => x.Key, StringComparer.OrdinalIgnoreCase))
        {
            if (GUILayout.Button($"{e.Key}  {e.Role}  {e.Address}/{e.Mask}  gateway:{e.Gateway}  L1:{Flag(e.Layer1Connected)} L2:{Flag(e.Layer2Connected)} valid:{Flag(e.HasValidAddress)}", EditorStyles.miniButton))
                _selectedEndpoint = e.Key;
        }

        if (!state.TryGetEndpoint(_selectedEndpoint, out var selected))
            return;
        EditorGUILayout.Space(6);
        EditorGUILayout.LabelField($"Selected: {selected.Key} / {selected.Role}", EditorStyles.boldLabel);
        var expected = state.Profile?.EndpointAddresses.TryGetValue(selected.Role, out var expectedAddress) == true ? expectedAddress : "—";
        EditorGUILayout.LabelField($"Live: {selected.Address}/{selected.Mask} gateway:{selected.Gateway} DNS:{selected.Dns} DHCP:{Flag(selected.UseDhcp)}");
        EditorGUILayout.LabelField($"Expected (scenario only): {expected}");
        EditorGUILayout.LabelField($"Binding: {selected.DeviceKey ?? "—"} / {selected.InterfaceName ?? "—"}  evidence:{selected.Evidence.Count} attempted:{selected.AttemptedEvidence.Count}");
        EditorGUILayout.LabelField($"Print attempts: {selected.PrintAttemptCount} last:{selected.LastPrintDiagnostic ?? "—"}", EditorStyles.miniLabel);
    }

    private void DrawProbe()
    {
        var state = _session.NetworkState;
        if (state == null) return;
        var endpoints = state.Endpoints.OrderBy(x => x.Key, StringComparer.OrdinalIgnoreCase).ToList();
        if (endpoints.Count == 0)
        {
            EditorGUILayout.LabelField("No source endpoints available.");
            return;
        }

        _probeSource = Mathf.Clamp(_probeSource, 0, endpoints.Count - 1);
        _probeSource = EditorGUILayout.Popup("Source endpoint", _probeSource, endpoints.Select(x => x.Key).ToArray());
        _probeDestination = EditorGUILayout.TextField("Destination address", _probeDestination);
        if (GUILayout.Button("Evaluate (read-only)"))
        {
            var result = new TssNetworkReachabilityEvaluator(state).Evaluate(endpoints[_probeSource].Key, _probeDestination);
            _probeText = $"{(result.Success ? "SUCCESS" : "FAIL")}  layer:{result.FailedLayer}  fault:{result.FaultCategory}\n{result.Diagnostic}\nsource:{result.SourceRole} destination:{result.DestinationRole}\nhops: {string.Join(" -> ", result.TraceHops ?? Array.Empty<string>())}";
            AddLog("ReachabilityProbe", result.Success ? "Probe succeeded." : $"Probe failed at layer {result.FailedLayer}.");
        }
        EditorGUILayout.HelpBox(_probeText, MessageType.None);
    }

    private void DrawLog()
    {
        EditorGUILayout.Space(4);
        EditorGUILayout.LabelField("Panel log", EditorStyles.boldLabel);
        foreach (var line in _log.Skip(Mathf.Max(0, _log.Count - 8)))
            EditorGUILayout.LabelField(line, EditorStyles.miniLabel);
    }

    private static string Flag(bool value) => value ? "YES" : "NO";

    private static void CopyText(string value)
    {
        EditorGUIUtility.systemCopyBuffer = value ?? string.Empty;
    }

    private string BuildSummaryText(TssNetworkSessionState state)
    {
        var b = new StringBuilder();
        b.AppendLine("TSS2 Network Diagnostics");
        b.AppendLine($"Scenario: {_session.Scenario?.scenarioId}");
        b.AppendLine($"Devices: {state.Devices.Count()} Endpoints: {state.Endpoints.Count()}");
        b.AppendLine($"Connections: {_session.PhysicalConnections.Count} Effective paths: {_session.GetEffectivePhysicalPaths().Count}");
        foreach (var d in state.Devices.OrderBy(x => x.Key, StringComparer.OrdinalIgnoreCase))
            b.AppendLine($"{d.Key} {d.Hostname} {d.Category} interfaces:{d.Interfaces.Count}");
        return b.ToString();
    }
}
