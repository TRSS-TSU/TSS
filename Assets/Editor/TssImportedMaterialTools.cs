using UnityEditor;
using UnityEngine;

public static class TssImportedMaterialTools
{
    private const string ImportedRoot = "Assets/Imported";
    private const string UrpLitShaderName = "Universal Render Pipeline/Lit";

    [MenuItem("Tools/TSS/Convert Imported Prop Materials To URP")]
    public static void ConvertImportedPropsToUrp()
    {
        var urpLit = Shader.Find(UrpLitShaderName);
        if (!urpLit)
        {
            Debug.LogError($"Could not find shader '{UrpLitShaderName}'. URP may not be loaded.");
            return;
        }

        var converted = 0;
        foreach (var guid in AssetDatabase.FindAssets("t:Material", new[] { ImportedRoot }))
        {
            var path = AssetDatabase.GUIDToAssetPath(guid);
            var material = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (!material || !material.shader || material.shader.name.StartsWith("Universal Render Pipeline/"))
                continue;

            var color = material.HasProperty("_Color") ? material.GetColor("_Color") : Color.white;
            var mainTex = material.HasProperty("_MainTex") ? material.GetTexture("_MainTex") : null;
            var metallic = material.HasProperty("_Metallic") ? material.GetFloat("_Metallic") : 0f;
            var smoothness = material.HasProperty("_Glossiness") ? material.GetFloat("_Glossiness") : 0.5f;
            var emissionColor = material.HasProperty("_EmissionColor") ? material.GetColor("_EmissionColor") : Color.black;
            var emissionTex = material.HasProperty("_EmissionMap") ? material.GetTexture("_EmissionMap") : null;

            Undo.RecordObject(material, "Convert imported material to URP");
            material.shader = urpLit;
            material.SetColor("_BaseColor", color);
            material.SetTexture("_BaseMap", mainTex);
            material.SetFloat("_Metallic", metallic);
            material.SetFloat("_Smoothness", smoothness);

            if (emissionColor.maxColorComponent > 0f || emissionTex)
            {
                material.EnableKeyword("_EMISSION");
                material.SetColor("_EmissionColor", emissionColor);
                material.SetTexture("_EmissionMap", emissionTex);
            }

            EditorUtility.SetDirty(material);
            converted++;
        }

        AssetDatabase.SaveAssets();
        Debug.Log($"Converted {converted} imported prop material(s) to URP/Lit under {ImportedRoot}.");
    }

    [MenuItem("Tools/TSS/Log Imported Prop Material Shader Summary")]
    public static void LogImportedPropMaterialShaderSummary()
    {
        var builtIn = 0;
        var urp = 0;
        var missing = 0;

        foreach (var guid in AssetDatabase.FindAssets("t:Material", new[] { ImportedRoot }))
        {
            var path = AssetDatabase.GUIDToAssetPath(guid);
            var material = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (!material || !material.shader)
            {
                missing++;
                continue;
            }

            if (material.shader.name.StartsWith("Universal Render Pipeline/"))
                urp++;
            else
                builtIn++;
        }

        Debug.Log($"Imported prop materials: URP={urp}, non-URP={builtIn}, missing shader={missing}.");
    }
}
