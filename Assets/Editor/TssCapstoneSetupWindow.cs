using UnityEditor;
using UnityEngine;

public sealed class TssCapstoneSetupWindow : EditorWindow
{
    private Vector2 scroll;
    private string report = "Click Preview after a network-enabled scenario has loaded in Play Mode.";
    private bool previewReady;

    [MenuItem("TSS/Developer/Prepare CAPSTONE")]
    private static void Open() => GetWindow<TssCapstoneSetupWindow>("CAPSTONE Setup");

    private void OnGUI()
    {
        EditorGUILayout.LabelField("Runtime CAPSTONE preparation", EditorStyles.boldLabel);
        EditorGUILayout.HelpBox("Works on the loaded training session. Router and switch CLI configuration remains manual.", MessageType.Info);
        using (new EditorGUI.DisabledScope(!Application.isPlaying || !TssTrainingSession.Instance))
        {
            if (GUILayout.Button("Preview"))
            {
                report = new TssCapstoneSetup(TssTrainingSession.Instance).Preview(out previewReady);
                scroll = Vector2.zero;
            }
            using (new EditorGUI.DisabledScope(!previewReady))
                if (GUILayout.Button("Apply preview"))
                {
                    report = new TssCapstoneSetup(TssTrainingSession.Instance).Apply();
                    previewReady = false;
                }
        }
        if (!Application.isPlaying || !TssTrainingSession.Instance)
            EditorGUILayout.HelpBox("Enter Play Mode and load a network-enabled training scenario first.", MessageType.Warning);
        scroll = EditorGUILayout.BeginScrollView(scroll);
        EditorGUILayout.TextArea(report, GUILayout.ExpandHeight(true));
        EditorGUILayout.EndScrollView();
    }
}
