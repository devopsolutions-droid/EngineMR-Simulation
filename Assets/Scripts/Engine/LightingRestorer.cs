using System.Collections;
using System.Text;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// Attach to any active GameObject in the Main Scene.
///
/// ROOT CAUSE (confirmed by logs):
/// ─────────────────────────────────────────────────────────────────────────────
/// The scene file has m_LightingDataAsset: {fileID: 0} — the LightingData.asset
/// exists on disk but was never linked to the scene. Unity auto-discovers it
/// when opening the scene in the Editor, but LoadSceneAsync at runtime sees
/// fileID:0 and loads 0 lightmaps.
///
/// Unity 2022.3 does NOT expose LightmapSettings.lightingDataAsset publicly.
///
/// THE FIX:
/// Manually re-assign LightmapSettings.lightmaps[] and LightmapSettings.lightProbes
/// at runtime using serialized references to the actual lightmap textures.
/// These ARE public APIs in all Unity versions.
///
/// HOW TO SET UP:
///   1. Add this component to any GameObject in the Main Scene
///   2. Click "Auto-Fill From Current LightmapSettings" button in the Inspector
///      (only works when the scene is open and lightmaps are loaded in Editor)
///   3. The lightmap textures and light probes will be serialized into this component
///   4. At runtime after transition, they are re-applied automatically
/// </summary>
public class LightingRestorer : MonoBehaviour
{
    [System.Serializable]
    public class LightmapEntry
    {
        [Tooltip("The color/intensity lightmap texture (Lightmap-X_comp_light.exr)")]
        public Texture2D lightmapColor;
        [Tooltip("The directional lightmap texture (Lightmap-X_comp_dir.exr) — may be null")]
        public Texture2D lightmapDir;
        [Tooltip("The shadow mask texture — may be null")]
        public Texture2D shadowMask;
    }

    [Header("Baked Lightmap Textures (REQUIRED)")]
    [Tooltip("Drag the lightmap textures from Assets/Scenes/Main Scene/ here. " +
             "Or use the Auto-Fill button in the Inspector context menu.")]
    public LightmapEntry[] lightmapEntries = new LightmapEntry[0];

    [Header("Baked Light Probes (Optional)")]
    [Tooltip("Drag the LightProbes asset from Assets/Scenes/Main Scene/ here if your scene uses light probes.")]
    public LightProbes lightProbes;

    [Header("Environment Settings")]
    [Tooltip("The skybox material used in this scene. Drag from Window > Rendering > Lighting > Environment.")]
    public Material skyboxMaterial;

    [Tooltip("Ambient mode to restore.")]
    public UnityEngine.Rendering.AmbientMode ambientMode = UnityEngine.Rendering.AmbientMode.Skybox;

    [Tooltip("Ambient intensity.")]
    [Range(0f, 8f)] public float ambientIntensity = 1f;

    [Header("Overrides — only applied if checked")]
    public bool overrideEnvironment = true;
    public bool overrideAmbient     = true;
    public bool overrideReflections = false;
    [Range(0f, 1f)] public float reflectionIntensity = 1f;

    [Header("Debug Logging")]
    public bool logOnAwake      = true;
    public bool logAfterRestore = true;
    public bool logActiveLights = true;
    public bool logLightmapRenderers = false;

    // ─────────────────────────────────────────────────────────────────────────

    void Awake()
    {
        if (logOnAwake)
            LogSnapshot("AWAKE (before restore)");

        SceneManager.sceneLoaded += OnSceneLoaded;
    }

    void OnDestroy()
    {
        SceneManager.sceneLoaded -= OnSceneLoaded;
    }

    void OnSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        if (scene != gameObject.scene) return;
        SceneManager.sceneLoaded -= OnSceneLoaded;
        StartCoroutine(RestoreAfterFrame());
    }

    IEnumerator RestoreAfterFrame()
    {
        // Let Unity's own post-load pass run first
        yield return null;

        ApplyLightmaps();
        ApplyEnvironment();

        if (logAfterRestore)    LogSnapshot("AFTER-RESTORE");
        if (logActiveLights)    LogActiveLights();
        if (logLightmapRenderers) LogLightmapRenderers();
    }

    // ── Core restore ──────────────────────────────────────────────────────────

    void ApplyLightmaps()
    {
        if (lightmapEntries == null || lightmapEntries.Length == 0)
        {
            Debug.LogWarning("[LightingRestorer] No lightmap entries assigned. " +
                             "Right-click the component → 'Auto-Fill Lightmaps From Scene' to populate them.");
            return;
        }

        // Build LightmapData array from our serialized entries
        var data = new LightmapData[lightmapEntries.Length];
        for (int i = 0; i < lightmapEntries.Length; i++)
        {
            data[i] = new LightmapData
            {
                lightmapColor = lightmapEntries[i].lightmapColor,
                lightmapDir   = lightmapEntries[i].lightmapDir,
                shadowMask    = lightmapEntries[i].shadowMask
            };
        }

        LightmapSettings.lightmaps = data;

        if (lightProbes != null)
            LightmapSettings.lightProbes = lightProbes;

        DynamicGI.UpdateEnvironment();

        Debug.Log($"[LightingRestorer] ✓ Applied {data.Length} lightmap(s). " +
                  $"LightmapSettings.lightmaps.Length = {LightmapSettings.lightmaps.Length} | " +
                  $"Frame {Time.frameCount}");
    }

    void ApplyEnvironment()
    {
        if (overrideEnvironment && skyboxMaterial != null)
            RenderSettings.skybox = skyboxMaterial;

        if (overrideAmbient)
        {
            RenderSettings.ambientMode      = ambientMode;
            RenderSettings.ambientIntensity = ambientIntensity;
        }

        if (overrideReflections)
            RenderSettings.reflectionIntensity = reflectionIntensity;

        // Rebuild ambient probe from the restored skybox/ambient settings
        DynamicGI.UpdateEnvironment();
    }

    public void ForceRefresh()
    {
        StartCoroutine(RestoreAfterFrame());
    }

    // ── Editor helper — auto-fill from current LightmapSettings ──────────────
#if UNITY_EDITOR
    [ContextMenu("Auto-Fill Lightmaps From Current Scene")]
    void AutoFillFromScene()
    {
        var current = LightmapSettings.lightmaps;
        if (current == null || current.Length == 0)
        {
            UnityEditor.EditorUtility.DisplayDialog("No Lightmaps",
                "No lightmaps are currently loaded in LightmapSettings.\n\n" +
                "Make sure you have the Main Scene open and lightmaps are baked " +
                "(Window > Rendering > Lighting > Generate Lighting).",
                "OK");
            return;
        }

        lightmapEntries = new LightmapEntry[current.Length];
        for (int i = 0; i < current.Length; i++)
        {
            lightmapEntries[i] = new LightmapEntry
            {
                lightmapColor = current[i].lightmapColor,
                lightmapDir   = current[i].lightmapDir,
                shadowMask    = current[i].shadowMask
            };
        }

        // Also grab light probes
        lightProbes = LightmapSettings.lightProbes;

        // Grab skybox
        if (RenderSettings.skybox != null)
            skyboxMaterial = RenderSettings.skybox;

        // Grab ambient settings
        ambientMode      = RenderSettings.ambientMode;
        ambientIntensity = RenderSettings.ambientIntensity;

        UnityEditor.EditorUtility.SetDirty(this);
        UnityEditor.SceneManagement.EditorSceneManager.MarkSceneDirty(gameObject.scene);

        Debug.Log($"[LightingRestorer] ✓ Auto-filled {current.Length} lightmap(s) from current scene. " +
                  $"Skybox: {(skyboxMaterial != null ? skyboxMaterial.name : "none")} | " +
                  $"AmbientMode: {ambientMode} | Save the scene to persist.");

        UnityEditor.EditorUtility.DisplayDialog("Auto-Fill Complete",
            $"Filled {current.Length} lightmap texture(s).\n" +
            $"Skybox: {(skyboxMaterial != null ? skyboxMaterial.name : "none")}\n" +
            $"Ambient mode: {ambientMode}\n\n" +
            "IMPORTANT: Save the scene now (Ctrl+S) to persist these references.",
            "OK — Saving now");
    }
#endif

    // ── Logging ───────────────────────────────────────────────────────────────

    void LogSnapshot(string label)
    {
        var scene = SceneManager.GetActiveScene();
        var sb = new StringBuilder();

        sb.AppendLine($"╔══════════════════════════════════════════════════════════════╗");
        sb.AppendLine($"║  [LightingSnapshot] {label}");
        sb.AppendLine($"╚══════════════════════════════════════════════════════════════╝");

        sb.AppendLine("  ── SCENE ──────────────────────────────────────────────────");
        sb.AppendLine($"  Scene name        : {scene.name}");
        sb.AppendLine($"  isLoaded          : {scene.isLoaded}");
        sb.AppendLine($"  Frame             : {Time.frameCount}");
        sb.AppendLine($"  SceneTransitionMgr: {(FindFirstObjectByType<SceneTransitionManager>() != null ? "EXISTS" : "NULL")}");
        sb.AppendLine($"  Entries assigned  : {(lightmapEntries != null ? lightmapEntries.Length.ToString() : "null")}");

        sb.AppendLine("  ── LIGHTMAPS ───────────────────────────────────────────────");
        var lmaps = LightmapSettings.lightmaps;
        sb.AppendLine($"  Count             : {lmaps.Length}{(lmaps.Length == 0 ? "  ← ZERO — baked GI not loaded" : " ✓")}");
        for (int i = 0; i < lmaps.Length; i++)
        {
            bool colorNull = lmaps[i].lightmapColor == null;
            sb.AppendLine($"  [{i}] color={(colorNull ? "NULL ← MISSING" : lmaps[i].lightmapColor.name)}  " +
                          $"dir={(lmaps[i].lightmapDir   != null ? lmaps[i].lightmapDir.name   : "none")}  " +
                          $"shadow={(lmaps[i].shadowMask != null ? lmaps[i].shadowMask.name    : "none")}");
        }
        sb.AppendLine($"  LightmapsMode     : {LightmapSettings.lightmapsMode}");

        sb.AppendLine("  ── ENVIRONMENT ─────────────────────────────────────────────");
        sb.AppendLine($"  Skybox            : {(RenderSettings.skybox != null ? RenderSettings.skybox.name : "NULL ← Default-Skybox (wrong!)")}");
        sb.AppendLine($"  Ambient mode      : {RenderSettings.ambientMode}");
        sb.AppendLine($"  Ambient intensity : {RenderSettings.ambientIntensity:F4}");
        sb.AppendLine($"  Ambient light     : {ColorStr(RenderSettings.ambientLight)}");
        sb.AppendLine($"  Probe brightness  : {ProbeStr(RenderSettings.ambientProbe)}");

        sb.AppendLine("  ── REFLECTIONS ─────────────────────────────────────────────");
        sb.AppendLine($"  Intensity         : {RenderSettings.reflectionIntensity:F4}");
        sb.AppendLine($"  Mode              : {RenderSettings.defaultReflectionMode}");

        sb.AppendLine("  ── FOG ─────────────────────────────────────────────────────");
        sb.AppendLine($"  Enabled           : {RenderSettings.fog}");
        sb.AppendLine($"  Density           : {RenderSettings.fogDensity:F6}");

        sb.AppendLine("  ── QUALITY ─────────────────────────────────────────────────");
        sb.AppendLine($"  Level             : {QualitySettings.GetQualityLevel()} ({QualitySettings.names[QualitySettings.GetQualityLevel()]})");
        sb.AppendLine($"  Shadow distance   : {QualitySettings.shadowDistance:F1}");
        sb.AppendLine($"  Shadow resolution : {QualitySettings.shadowResolution}");

        Debug.Log(sb.ToString());
    }

    void LogActiveLights()
    {
        var lights = FindObjectsByType<Light>(FindObjectsInactive.Exclude, FindObjectsSortMode.None);
        var sb = new StringBuilder();
        sb.AppendLine($"╔══════════════════════════════════════════════════════════════╗");
        sb.AppendLine($"║  [LightingRestorer] ACTIVE LIGHTS — {lights.Length} found");
        sb.AppendLine($"╚══════════════════════════════════════════════════════════════╝");

        if (lights.Length == 0)
            sb.AppendLine("  ← NO ACTIVE LIGHTS — scene will be dark!");
        else
            foreach (var l in lights)
            {
                sb.AppendLine($"  [{l.type}] \"{GetPath(l.transform)}\"");
                sb.AppendLine($"    intensity={l.intensity:F3}  color={ColorStr(l.color)}");
                sb.AppendLine($"    shadows={l.shadows}  bakeType={l.lightmapBakeType}  enabled={l.enabled}");
            }

        Debug.Log(sb.ToString());
    }

    void LogLightmapRenderers()
    {
        var renderers = FindObjectsByType<MeshRenderer>(FindObjectsInactive.Exclude, FindObjectsSortMode.None);
        int withLM = 0, withoutLM = 0;
        var sb = new StringBuilder();
        sb.AppendLine($"╔══════════════════════════════════════════════════════════════╗");
        sb.AppendLine($"║  [LightingRestorer] LIGHTMAP RENDERERS — {renderers.Length} total");
        sb.AppendLine($"╚══════════════════════════════════════════════════════════════╝");

        foreach (var r in renderers)
        {
            if (r.lightmapIndex >= 0) { withLM++; sb.AppendLine($"  ✓ [{r.lightmapIndex}] \"{r.gameObject.name}\""); }
            else withoutLM++;
        }

        sb.AppendLine($"\n  ✓ With lightmap : {withLM}");
        sb.AppendLine($"  ✗ Without       : {withoutLM}");
        if (withLM == 0)
            sb.AppendLine("  ← ALL lightmapIndex=-1 — lightmaps not applied to any renderer!");

        Debug.Log(sb.ToString());
    }

    static string ColorStr(Color c) =>
        $"({c.r:F3}, {c.g:F3}, {c.b:F3})  #{ColorUtility.ToHtmlStringRGB(c)}";

    static string ProbeStr(UnityEngine.Rendering.SphericalHarmonicsL2 probe)
    {
        var dirs   = new[] { Vector3.up, Vector3.down, Vector3.forward, Vector3.back, Vector3.left, Vector3.right };
        var colors = new Color[6];
        probe.Evaluate(dirs, colors);
        float avg = 0f;
        foreach (var c in colors) avg += c.grayscale;
        avg /= 6f;
        return avg < 0.001f
            ? $"EMPTY (avg={avg:F4}) ← ambient probe has no data!"
            : $"avg={avg:F4}  up={colors[0].grayscale:F3}  fwd={colors[2].grayscale:F3}  dn={colors[1].grayscale:F3}";
    }

    static string GetPath(Transform t)
    {
        var parts = new System.Collections.Generic.List<string>();
        while (t != null) { parts.Insert(0, t.name); t = t.parent; }
        return string.Join("/", parts);
    }
}
