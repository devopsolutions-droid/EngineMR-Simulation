using UnityEngine;
using UnityEditor;
using System.Collections.Generic;
using System.IO;

public class VisualAssetExporter
{
    private static Dictionary<string, string> copiedPathsMap = new Dictionary<string, string>();

    // =================================================================
    //  EXPORT MENUS
    // =================================================================

    [MenuItem("GameObject/Export Clean Prefab (Visuals Only)", false, 49)]
    public static void ExportFromHierarchy()
    {
        GameObject selected = Selection.activeGameObject;
        if (selected == null)
        {
            EditorUtility.DisplayDialog("No Selection", "Select a GameObject in the Hierarchy first.", "OK");
            return;
        }

        DoExport(selected);
    }

    [MenuItem("GameObject/Export Clean Prefab (Visuals Only)", true)]
    public static bool ValidateExportFromHierarchy()
    {
        return Selection.activeGameObject != null;
    }

    [MenuItem("Assets/Export Clean Prefab (Visuals Only)")]
    public static void ExportFromProjectWindow()
    {
        Object sel = Selection.activeObject;
        if (sel == null)
        {
            EditorUtility.DisplayDialog("No Selection", "Select a prefab or model in the Project window first.", "OK");
            return;
        }

        GameObject go = null;
        bool needsCleanup = false;

        if (sel is GameObject)
        {
            go = Object.Instantiate((GameObject)sel);
            go.name = sel.name;
            needsCleanup = true;
        }
        else
        {
            string path = AssetDatabase.GetAssetPath(sel);
            GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(path);
            if (prefab != null)
            {
                go = Object.Instantiate(prefab);
                go.name = prefab.name;
                needsCleanup = true;
            }
        }

        if (go == null)
        {
            EditorUtility.DisplayDialog("Invalid Selection", "Please select a Prefab or 3D Model.", "OK");
            return;
        }

        DoExport(go);

        if (needsCleanup)
            Object.DestroyImmediate(go);
    }

    // =================================================================
    //  URP MATERIAL CONVERTER MENU (Run this in the new project)
    // =================================================================

    [MenuItem("Assets/Convert Folder Materials to URP")]
    public static void ConvertSelectedFolderMaterialsToURP()
    {
        string selectedPath = AssetDatabase.GetAssetPath(Selection.activeObject);
        if (string.IsNullOrEmpty(selectedPath))
        {
            EditorUtility.DisplayDialog("Error", "Please select a folder or material in the Project window.", "OK");
            return;
        }

        // Find all materials in the selected folder/assets
        string[] guids = AssetDatabase.FindAssets("t:Material", new[] { selectedPath });
        List<Material> materials = new List<Material>();
        foreach (string guid in guids)
        {
            string path = AssetDatabase.GUIDToAssetPath(guid);
            Material mat = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (mat != null) materials.Add(mat);
        }

        // Also check if we selected a single material
        Material selectedMat = Selection.activeObject as Material;
        if (selectedMat != null && !materials.Contains(selectedMat))
        {
            materials.Add(selectedMat);
        }

        if (materials.Count == 0)
        {
            EditorUtility.DisplayDialog("No Materials", "No materials found in the selected folder.", "OK");
            return;
        }

        // 1. Fix custom shaders (like AlwaysOnTop) in the selected folder
        FixCustomShadersForURP(selectedPath);

        // 2. Convert Materials to URP Shaders
        int convertedCount = 0;
        foreach (Material mat in materials)
        {
            if (TryConvertToURP(mat))
            {
                convertedCount++;
            }
        }

        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();

        EditorUtility.DisplayDialog(
            "URP Conversion Complete",
            $"Scanned {materials.Count} materials.\nConverted {convertedCount} materials/shaders to URP compatibility.",
            "Awesome"
        );
    }

    // =================================================================
    //  CORE EXPORT LOGIC
    // =================================================================
    private static void DoExport(GameObject source)
    {
        copiedPathsMap.Clear();
        string modelName = source.name;

        // Create folders
        string exportFolder = "Assets/_ExportedModels/" + modelName;
        string texFolder = exportFolder + "/Textures";
        string matFolder = exportFolder + "/Materials";
        string meshFolder = exportFolder + "/Meshes";
        string shaderFolder = exportFolder + "/Shaders";

        EnsureFolder(exportFolder);
        EnsureFolder(texFolder);
        EnsureFolder(matFolder);
        EnsureFolder(meshFolder);
        EnsureFolder(shaderFolder);

        // 1) Duplicate the source GameObject
        GameObject clone = Object.Instantiate(source);
        clone.name = modelName;

        // 2) Strip scripts
        StripScripts(clone);

        // 3) Process Meshes (FBX files)
        MeshFilter[] meshFilters = clone.GetComponentsInChildren<MeshFilter>(true);
        foreach (MeshFilter mf in meshFilters)
        {
            if (mf.sharedMesh != null)
            {
                mf.sharedMesh = CopyAndGetMesh(mf.sharedMesh, meshFolder);
            }
        }

        SkinnedMeshRenderer[] skinnedRenderers = clone.GetComponentsInChildren<SkinnedMeshRenderer>(true);
        foreach (SkinnedMeshRenderer smr in skinnedRenderers)
        {
            if (smr.sharedMesh != null)
            {
                smr.sharedMesh = CopyAndGetMesh(smr.sharedMesh, meshFolder);
            }
        }

        // 4) Process Materials, Textures, and Shaders
        Renderer[] renderers = clone.GetComponentsInChildren<Renderer>(true);
        foreach (Renderer rend in renderers)
        {
            Material[] sharedMats = rend.sharedMaterials;
            Material[] newMats = new Material[sharedMats.Length];

            for (int i = 0; i < sharedMats.Length; i++)
            {
                Material mat = sharedMats[i];
                if (mat == null) continue;

                // Copy Material
                Material copiedMat = CopyMaterialAsset(mat, matFolder);
                if (copiedMat != null)
                {
                    // Copy Shader if it's a custom one (not built-in)
                    CopyAndAssignShader(copiedMat, shaderFolder);

                    // Copy Textures referenced by the material
                    CopyTexturesForMaterial(copiedMat, texFolder);

                    newMats[i] = copiedMat;
                }
                else
                {
                    newMats[i] = mat;
                }
            }

            rend.sharedMaterials = newMats;
        }

        // 5) Save the clean self-contained clone as a Prefab
        string prefabPath = exportFolder + "/" + modelName + ".prefab";
        PrefabUtility.SaveAsPrefabAsset(clone, prefabPath);

        // 6) Clean up scene duplicate
        Object.DestroyImmediate(clone);

        AssetDatabase.Refresh();

        bool openFolder = EditorUtility.DisplayDialog(
            "Export Complete!",
            "Created a self-contained prefab in:\n\n" + exportFolder + "\n\n" +
            "Steps for the other project:\n" +
            "1. Copy the entire '" + modelName + "' folder from your computer.\n" +
            "2. Paste it into the new project's Assets folder.\n" +
            "3. Drag the prefab into your scene!\n" +
            "4. Right-click the folder in the new project and click 'Convert Folder Materials to URP' to fix pink colors!\n\n" +
            "Open export folder now?",
            "Open Folder", "Close"
        );

        if (openFolder)
        {
            string fullPath = Path.GetFullPath(exportFolder);
            EditorUtility.RevealInFinder(fullPath);
        }

        Object prefabAsset = AssetDatabase.LoadAssetAtPath<Object>(prefabPath);
        if (prefabAsset != null)
        {
            Selection.activeObject = prefabAsset;
            EditorGUIUtility.PingObject(prefabAsset);
        }
    }

    private static void StripScripts(GameObject go)
    {
        MonoBehaviour[] scripts = go.GetComponents<MonoBehaviour>();
        foreach (MonoBehaviour script in scripts)
        {
            Object.DestroyImmediate(script);
        }

        for (int i = 0; i < go.transform.childCount; i++)
        {
            StripScripts(go.transform.GetChild(i).gameObject);
        }
    }

    private static Mesh CopyAndGetMesh(Mesh originalMesh, string destFolder)
    {
        string srcPath = AssetDatabase.GetAssetPath(originalMesh);
        if (string.IsNullOrEmpty(srcPath) || srcPath.StartsWith("Resources/") || srcPath.StartsWith("Library/"))
            return originalMesh;

        string fileName = Path.GetFileName(srcPath);
        string destPath = destFolder + "/" + fileName;

        if (!copiedPathsMap.ContainsKey(srcPath))
        {
            AssetDatabase.CopyAsset(srcPath, destPath);
            copiedPathsMap.Add(srcPath, destPath);
        }

        Object[] assets = AssetDatabase.LoadAllAssetsAtPath(destPath);
        foreach (Object asset in assets)
        {
            if (asset is Mesh && asset.name == originalMesh.name)
            {
                return (Mesh)asset;
            }
        }

        return originalMesh;
    }

    private static Material CopyMaterialAsset(Material originalMat, string destFolder)
    {
        string srcPath = AssetDatabase.GetAssetPath(originalMat);
        if (string.IsNullOrEmpty(srcPath) || srcPath.StartsWith("Resources/") || srcPath.StartsWith("Library/"))
            return originalMat;

        string fileName = Path.GetFileName(srcPath);
        string destPath = destFolder + "/" + fileName;

        if (!copiedPathsMap.ContainsKey(srcPath))
        {
            AssetDatabase.CopyAsset(srcPath, destPath);
            copiedPathsMap.Add(srcPath, destPath);
        }

        return AssetDatabase.LoadAssetAtPath<Material>(destPath);
    }

    private static void CopyAndAssignShader(Material mat, string destFolder)
    {
        Shader shader = mat.shader;
        if (shader == null) return;

        string srcPath = AssetDatabase.GetAssetPath(shader);
        if (string.IsNullOrEmpty(srcPath) || srcPath.StartsWith("Resources/") || srcPath.StartsWith("Library/"))
            return;

        string fileName = Path.GetFileName(srcPath);
        string destPath = destFolder + "/" + fileName;

        if (!copiedPathsMap.ContainsKey(srcPath))
        {
            AssetDatabase.CopyAsset(srcPath, destPath);
            copiedPathsMap.Add(srcPath, destPath);
        }

        Shader copiedShader = AssetDatabase.LoadAssetAtPath<Shader>(destPath);
        if (copiedShader != null)
        {
            mat.shader = copiedShader;
        }
    }

    private static void CopyTexturesForMaterial(Material mat, string destFolder)
    {
        Shader shader = mat.shader;
        if (shader == null) return;

        int propCount = ShaderUtil.GetPropertyCount(shader);
        for (int i = 0; i < propCount; i++)
        {
            if (ShaderUtil.GetPropertyType(shader, i) == ShaderUtil.ShaderPropertyType.TexEnv)
            {
                string propName = ShaderUtil.GetPropertyName(shader, i);
                Texture tex = mat.GetTexture(propName);
                if (tex == null) continue;

                string srcPath = AssetDatabase.GetAssetPath(tex);
                if (string.IsNullOrEmpty(srcPath) || srcPath.StartsWith("Resources/") || srcPath.StartsWith("Library/"))
                    continue;

                string fileName = Path.GetFileName(srcPath);
                string destPath = destFolder + "/" + fileName;

                if (!copiedPathsMap.ContainsKey(srcPath))
                {
                    AssetDatabase.CopyAsset(srcPath, destPath);
                    copiedPathsMap.Add(srcPath, destPath);
                }

                Texture copiedTex = AssetDatabase.LoadAssetAtPath<Texture>(destPath);
                if (copiedTex != null)
                {
                    mat.SetTexture(propName, copiedTex);
                }
            }
        }
    }

    private static void EnsureFolder(string folderPath)
    {
        if (AssetDatabase.IsValidFolder(folderPath)) return;

        string parent = Path.GetDirectoryName(folderPath).Replace("\\", "/");
        string newFolder = Path.GetFileName(folderPath);

        if (!AssetDatabase.IsValidFolder(parent))
            EnsureFolder(parent);

        AssetDatabase.CreateFolder(parent, newFolder);
    }

    // =================================================================
    //  URP CONVERSION LOGIC
    // =================================================================

    private static bool TryConvertToURP(Material mat)
    {
        Shader currentShader = mat.shader;
        if (currentShader == null) return false;

        string shaderName = currentShader.name;

        // If the shader is already URP or Unlit, or compiled fine, we might not need to touch it
        // unless it's the standard legacy Standard shader.
        bool isStandard = shaderName == "Standard" || shaderName == "Standard (Specular setup)";
        bool isErrorShader = shaderName == "Hidden/InternalErrorShader";

        if (isStandard || isErrorShader)
        {
            // Map legacy Standard properties to URP Lit properties
            Color color = mat.HasProperty("_Color") ? mat.GetColor("_Color") : Color.white;
            Texture mainTex = mat.HasProperty("_MainTex") ? mat.GetTexture("_MainTex") : null;
            Vector2 mainTexScale = mat.HasProperty("_MainTex") ? mat.GetTextureScale("_MainTex") : Vector2.one;
            Vector2 mainTexOffset = mat.HasProperty("_MainTex") ? mat.GetTextureOffset("_MainTex") : Vector2.zero;

            Texture bumpMap = mat.HasProperty("_BumpMap") ? mat.GetTexture("_BumpMap") : null;
            float bumpScale = mat.HasProperty("_BumpScale") ? mat.GetFloat("_BumpScale") : 1.0f;

            float glossiness = mat.HasProperty("_Glossiness") ? mat.GetFloat("_Glossiness") : 0.5f;
            float metallic = mat.HasProperty("_Metallic") ? mat.GetFloat("_Metallic") : 0.0f;
            Texture metallicMap = mat.HasProperty("_MetallicGlossMap") ? mat.GetTexture("_MetallicGlossMap") : null;

            Color emissionColor = mat.HasProperty("_EmissionColor") ? mat.GetColor("_EmissionColor") : Color.black;
            Texture emissionMap = mat.HasProperty("_EmissionMap") ? mat.GetTexture("_EmissionMap") : null;

            // Change to URP Lit
            Shader urpShader = Shader.Find("Universal Render Pipeline/Lit");
            if (urpShader != null)
            {
                mat.shader = urpShader;

                // Re-apply properties
                mat.SetColor("_BaseColor", color);
                if (mainTex != null)
                {
                    mat.SetTexture("_BaseMap", mainTex);
                    mat.SetTextureScale("_BaseMap", mainTexScale);
                    mat.SetTextureOffset("_BaseMap", mainTexOffset);
                }

                if (bumpMap != null)
                {
                    mat.SetTexture("_BumpMap", bumpMap);
                    mat.SetFloat("_BumpScale", bumpScale);
                    mat.EnableKeyword("_NORMALMAP");
                }

                mat.SetFloat("_Metallic", metallic);
                mat.SetFloat("_Smoothness", glossiness);
                if (metallicMap != null)
                {
                    mat.SetTexture("_MetallicGlossMap", metallicMap);
                    mat.EnableKeyword("_METALLICSPECGLOSSMAP");
                }

                if (emissionColor != Color.black)
                {
                    mat.SetColor("_EmissionColor", emissionColor);
                    if (emissionMap != null)
                    {
                        mat.SetTexture("_EmissionMap", emissionMap);
                    }
                    mat.EnableKeyword("_EMISSION");
                }

                return true;
            }
        }
        else if (shaderName.Contains("AlwaysOnTop"))
        {
            // If it's the Custom/AlwaysOnTop or similar, let's make sure it uses the URP version
            Shader urpAlwaysOnTop = Shader.Find("Custom/AlwaysOnTop");
            if (urpAlwaysOnTop != null && currentShader != urpAlwaysOnTop)
            {
                mat.shader = urpAlwaysOnTop;
                return true;
            }
        }

        return false;
    }

    private static void FixCustomShadersForURP(string folderPath)
    {
        // Find any .shader files in the selected folder
        string[] shaderGuids = AssetDatabase.FindAssets("t:Shader", new[] { folderPath });
        foreach (string guid in shaderGuids)
        {
            string path = AssetDatabase.GUIDToAssetPath(guid);
            string content = File.ReadAllText(path);

            // If it's our AlwaysOnTop shader and contains legacy CGPROGRAM code, overwrite with URP code
            if (content.Contains("Shader \"Custom/AlwaysOnTop\"") && content.Contains("CGPROGRAM"))
            {
                string urpAlwaysOnTopCode = 
@"Shader ""Custom/AlwaysOnTop""
{
    Properties
    {
        _BaseColor (""Color"", Color) = (1,1,1,1)
        _BaseMap (""Albedo (RGB)"", 2D) = ""white"" {}
        [HideInInspector] _Color (""Legacy Color"", Color) = (1,1,1,1)
        [HideInInspector] _MainTex (""Legacy Albedo"", 2D) = ""white"" {}
    }
    SubShader
    {
        Tags 
        { 
            ""RenderType""=""Transparent"" 
            ""Queue""=""Overlay""
            ""RenderPipeline""=""UniversalPipeline""
        }
        LOD 100

        Pass
        {
            Name ""AlwaysOnTopPass""
            ZTest Always
            ZWrite Off
            Blend SrcAlpha OneMinusSrcAlpha

            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag

            #include ""Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl""

            struct Attributes
            {
                float4 positionOS   : POSITION;
                float2 uv           : TEXCOORD0;
            };

            struct Varyings
            {
                float4 positionCS   : SV_POSITION;
                float2 uv           : TEXCOORD0;
            };

            TEXTURE2D(_BaseMap);
            SAMPLER(sampler_BaseMap);

            CBUFFER_START(UnityPerMaterial)
                float4 _BaseMap_ST;
                float4 _BaseColor;
                float4 _Color;
            CBUFFER_END

            Varyings vert(Attributes input)
            {
                Varyings output;
                output.positionCS = TransformObjectToHClip(input.positionOS.xyz);
                output.uv = TRANSFORM_TEX(input.uv, _BaseMap);
                return output;
            }

            half4 frag(Varyings input) : SV_Target
            {
                half4 col = SAMPLE_TEXTURE2D(_BaseMap, sampler_BaseMap, input.uv) * _BaseColor;
                return col;
            }
            ENDHLSL
        }
    }
}";
                File.WriteAllText(path, urpAlwaysOnTopCode);
                Debug.Log("Rewrote " + path + " to URP-compatible AlwaysOnTop shader.");
            }
        }
    }
}
