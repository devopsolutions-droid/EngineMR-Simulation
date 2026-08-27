#if UNITY_EDITOR
using System.Collections.Generic;
using System.IO;
using System.Text;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

/// <summary>
/// Step-by-step URP migration for EngineVR.
/// Menu: Tools → URP Migration → Step 1 … Step 4 (run in order).
/// </summary>
public static class UrpMigrationWizard
{
    const string SettingsFolder = "Assets/Settings/URP";
    const string PipelineAssetPath = SettingsFolder + "/EngineVR_URP.asset";
    const string RendererDataPath = SettingsFolder + "/EngineVR_ForwardRenderer.asset";

    // Unity 2022.2+ / URP 14 — old "Edit/Render Pipeline/Universal..." menu was removed.
    static readonly string[] MaterialUpgradeMenuPaths =
    {
        "Edit/Rendering/Materials/Convert All Built-In Materials to Current SRP",
        "Edit/Rendering/Materials/Convert Selected Built-in Materials to URP",
        "Window/Rendering/Render Pipeline Converter",
    };

    static readonly string[] BuiltInShaderNames =
    {
        "Standard",
        "Standard (Specular setup)",
        "Mobile/Diffuse",
        "Mobile/Bumped Diffuse",
        "Mobile/Specular",
        "Legacy Shaders/Diffuse",
        "Legacy Shaders/Specular",
        "Legacy Shaders/Bumped Diffuse",
        "Legacy Shaders/Bumped Specular",
    };

    [MenuItem("Tools/URP Migration/Step 1 - Create URP Assets", false, 1)]
    public static void Step1_CreateUrpAssets()
    {
        EnsureFolder("Assets/Settings");
        EnsureFolder(SettingsFolder);

        var rendererData = AssetDatabase.LoadAssetAtPath<UniversalRendererData>(RendererDataPath);
        if (rendererData == null)
        {
            rendererData = ScriptableObject.CreateInstance<UniversalRendererData>();
            rendererData.name = "EngineVR_ForwardRenderer";
            AssetDatabase.CreateAsset(rendererData, RendererDataPath);
        }

        var pipelineAsset = AssetDatabase.LoadAssetAtPath<UniversalRenderPipelineAsset>(PipelineAssetPath);
        if (pipelineAsset == null)
        {
            pipelineAsset = UniversalRenderPipelineAsset.Create(rendererData);
            pipelineAsset.name = "EngineVR_URP";
            AssetDatabase.CreateAsset(pipelineAsset, PipelineAssetPath);
        }

        ApplyVrFriendlyDefaults(pipelineAsset);

        EditorUtility.SetDirty(rendererData);
        EditorUtility.SetDirty(pipelineAsset);
        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();

        Selection.activeObject = pipelineAsset;
        EditorGUIUtility.PingObject(pipelineAsset);

        EditorUtility.DisplayDialog(
            "Step 1 complete",
            "Created URP assets at:\n" + PipelineAssetPath + "\n\n" +
            "Next: Tools → URP Migration → Step 2",
            "OK");
    }

    [MenuItem("Tools/URP Migration/Step 2 - Assign URP To Project", false, 2)]
    public static void Step2_AssignPipelineToProject()
    {
        var pipelineAsset = AssetDatabase.LoadAssetAtPath<UniversalRenderPipelineAsset>(PipelineAssetPath);
        if (pipelineAsset == null)
        {
            EditorUtility.DisplayDialog(
                "Run Step 1 first",
                "No URP asset found at:\n" + PipelineAssetPath,
                "OK");
            return;
        }

        GraphicsSettings.defaultRenderPipeline = pipelineAsset;
        GraphicsSettings.renderPipelineAsset = pipelineAsset;
        AssignPipelineToAllQualityLevels(pipelineAsset);

        EditorUtility.SetDirty(GraphicsSettings.GetGraphicsSettings());
        AssetDatabase.SaveAssets();

        EditorUtility.DisplayDialog(
            "Step 2 complete",
            "URP is now the active render pipeline.\n\n" +
            "The Scene view may look different immediately.\n\n" +
            "Next: Tools → URP Migration → Step 3",
            "OK");
    }

    [MenuItem("Tools/URP Migration/Step 3 - Upgrade Built-in Materials", false, 3)]
    public static void Step3_UpgradeMaterials()
    {
        if (GraphicsSettings.currentRenderPipeline == null ||
            GraphicsSettings.currentRenderPipeline.GetType().Name != "UniversalRenderPipelineAsset")
        {
            EditorUtility.DisplayDialog(
                "Run Step 2 first",
                "URP must be assigned before upgrading materials.",
                "OK");
            return;
        }

        if (!EditorUtility.DisplayDialog(
                "Upgrade materials?",
                "This converts Built-in Standard materials in the project to URP.\n\n" +
                "Recommended: save the project / commit first.\n\nContinue?",
                "Upgrade",
                "Cancel"))
            return;

        var menuUsed = TryRunMaterialUpgradeMenu(out var menuPath);
        if (menuUsed)
        {
            EditorUtility.DisplayDialog(
                "Step 3 — Unity converter",
                "Opened:\n" + menuPath + "\n\n" +
                "If the Render Pipeline Converter window opened, enable " +
                "\"Material Upgrade\" and click Initialize Converters → Convert Assets.\n\n" +
                "When finished, run Step 4.",
                "OK");
            return;
        }

        var upgradedCount = UpgradeBuiltInStandardMaterials();
        if (upgradedCount < 0)
        {
            EditorUtility.DisplayDialog(
                "Step 3 failed",
                "Could not find URP Lit shader.\n\n" +
                "Try manually:\n" +
                "• Edit → Rendering → Materials → Convert All Built-In Materials to Current SRP\n" +
                "• Window → Rendering → Render Pipeline Converter",
                "OK");
            return;
        }

        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();

        EditorUtility.DisplayDialog(
            "Step 3 complete (built-in converter)",
            "Unity's upgrade menu was not available, so this tool converted " +
            upgradedCount + " Built-in Standard-style material(s) to URP/Lit.\n\n" +
            "Run Step 4 to check for any materials still pink.",
            "OK");
    }

    [MenuItem("Tools/URP Migration/Open Render Pipeline Converter", false, 35)]
    public static void OpenRenderPipelineConverter()
    {
        if (!EditorApplication.ExecuteMenuItem("Window/Rendering/Render Pipeline Converter"))
        {
            EditorUtility.DisplayDialog(
                "Not found",
                "Open: Window → Rendering → Render Pipeline Converter",
                "OK");
        }
    }

    [MenuItem("Tools/URP Migration/Step 4 - Report Broken Materials", false, 4)]
    public static void Step4_ReportBrokenMaterials()
    {
        var broken = FindBrokenMaterials();
        if (broken.Count == 0)
        {
            EditorUtility.DisplayDialog(
                "Step 4 complete",
                "No materials using the error (pink) shader were found.",
                "OK");
            return;
        }

        var logPath = "Assets/Settings/URP/broken-materials-report.txt";
        EnsureFolder(SettingsFolder);
        var sb = new StringBuilder();
        sb.AppendLine("Materials still using a missing/error shader (show pink in scene):");
        sb.AppendLine();
        foreach (var path in broken)
            sb.AppendLine(path);

        File.WriteAllText(logPath, sb.ToString());
        AssetDatabase.Refresh();

        Debug.LogWarning(
            "[URP Migration] " + broken.Count + " material(s) still broken. See:\n" + logPath,
            AssetDatabase.LoadMainAssetAtPath(logPath));

        EditorUtility.DisplayDialog(
            "Step 4 — review needed",
            broken.Count + " material(s) still need manual shader fixes.\n\n" +
            "Report saved to:\n" + logPath,
            "OK");
    }

    static bool TryRunMaterialUpgradeMenu(out string usedPath)
    {
        foreach (var path in MaterialUpgradeMenuPaths)
        {
            if (EditorApplication.ExecuteMenuItem(path))
            {
                usedPath = path;
                return true;
            }
        }

        usedPath = null;
        return false;
    }

    static int UpgradeBuiltInStandardMaterials()
    {
        var urpLit = Shader.Find("Universal Render Pipeline/Lit");
        if (urpLit == null)
            return -1;

        var upgraded = 0;
        var guids = AssetDatabase.FindAssets("t:Material");

        try
        {
            AssetDatabase.StartAssetEditing();

            foreach (var guid in guids)
            {
                var path = AssetDatabase.GUIDToAssetPath(guid);
                var mat = AssetDatabase.LoadAssetAtPath<Material>(path);
                if (mat == null || mat.shader == null)
                    continue;

                if (!IsBuiltInShader(mat.shader.name))
                    continue;

                Undo.RecordObject(mat, "URP Material Upgrade");
                CopyBuiltInPropertiesToUrp(mat);
                mat.shader = urpLit;
                EditorUtility.SetDirty(mat);
                upgraded++;
            }
        }
        finally
        {
            AssetDatabase.StopAssetEditing();
        }

        return upgraded;
    }

    static bool IsBuiltInShader(string shaderName)
    {
        foreach (var builtIn in BuiltInShaderNames)
        {
            if (shaderName == builtIn)
                return true;
        }

        return false;
    }

    static void CopyBuiltInPropertiesToUrp(Material mat)
    {
        if (mat.HasProperty("_MainTex") && mat.HasProperty("_BaseMap"))
            mat.SetTexture("_BaseMap", mat.GetTexture("_MainTex"));

        if (mat.HasProperty("_Color") && mat.HasProperty("_BaseColor"))
            mat.SetColor("_BaseColor", mat.GetColor("_Color"));

        if (mat.HasProperty("_Glossiness") && mat.HasProperty("_Smoothness"))
            mat.SetFloat("_Smoothness", mat.GetFloat("_Glossiness"));

        if (mat.HasProperty("_GlossMapScale") && mat.HasProperty("_Smoothness"))
            mat.SetFloat("_Smoothness", mat.GetFloat("_GlossMapScale"));

        if (mat.HasProperty("_Metallic") && mat.HasProperty("_Metallic"))
            mat.SetFloat("_Metallic", mat.GetFloat("_Metallic"));

        if (mat.HasProperty("_BumpMap") && mat.HasProperty("_BumpMap"))
            mat.SetTexture("_BumpMap", mat.GetTexture("_BumpMap"));

        if (mat.HasProperty("_BumpScale") && mat.HasProperty("_BumpScale"))
            mat.SetFloat("_BumpScale", mat.GetFloat("_BumpScale"));

        if (mat.HasProperty("_EmissionMap") && mat.HasProperty("_EmissionMap"))
            mat.SetTexture("_EmissionMap", mat.GetTexture("_EmissionMap"));

        if (mat.HasProperty("_EmissionColor") && mat.HasProperty("_EmissionColor"))
        {
            mat.SetColor("_EmissionColor", mat.GetColor("_EmissionColor"));
            mat.EnableKeyword("_EMISSION");
        }

        if (mat.HasProperty("_OcclusionMap") && mat.HasProperty("_OcclusionMap"))
            mat.SetTexture("_OcclusionMap", mat.GetTexture("_OcclusionMap"));
    }

    static void ApplyVrFriendlyDefaults(UniversalRenderPipelineAsset asset)
    {
        asset.supportsHDR = true;
        asset.msaaSampleCount = 4;
        asset.renderScale = 1f;
        asset.shadowDistance = 40f;
    }

    static void AssignPipelineToAllQualityLevels(RenderPipelineAsset pipelineAsset)
    {
        var qualitySettingsAssets = AssetDatabase.LoadAllAssetsAtPath("ProjectSettings/QualitySettings.asset");
        if (qualitySettingsAssets == null || qualitySettingsAssets.Length == 0)
            return;

        var so = new SerializedObject(qualitySettingsAssets[0]);
        var qualityArray = so.FindProperty("m_QualitySettings");
        if (qualityArray == null)
            return;

        for (var i = 0; i < qualityArray.arraySize; i++)
        {
            var element = qualityArray.GetArrayElementAtIndex(i);
            var pipelineProp = element.FindPropertyRelative("customRenderPipeline");
            if (pipelineProp != null)
                pipelineProp.objectReferenceValue = pipelineAsset;
        }

        so.ApplyModifiedPropertiesWithoutUndo();
    }

    static List<string> FindBrokenMaterials()
    {
        var results = new List<string>();
        var guids = AssetDatabase.FindAssets("t:Material");
        foreach (var guid in guids)
        {
            var path = AssetDatabase.GUIDToAssetPath(guid);
            var mat = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (mat == null || mat.shader == null)
                continue;

            if (mat.shader.name == "Hidden/InternalErrorShader")
                results.Add(path);
        }

        return results;
    }

    static void EnsureFolder(string path)
    {
        if (AssetDatabase.IsValidFolder(path))
            return;

        var parent = Path.GetDirectoryName(path)?.Replace('\\', '/');
        var name = Path.GetFileName(path);
        if (!string.IsNullOrEmpty(parent) && !string.IsNullOrEmpty(name))
            AssetDatabase.CreateFolder(parent, name);
    }
}
#endif
