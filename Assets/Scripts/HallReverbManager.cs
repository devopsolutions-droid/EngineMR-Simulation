using UnityEngine;
using System.Collections.Generic;

/// <summary>
/// Attach this to any active GameObject in the Main Scene.
/// On Awake it finds every AudioSource in the scene and adds an
/// AudioReverbFilter to its GameObject, giving all audio the feel
/// of a large empty hall.
///
/// Any AudioSource created AFTER Awake (e.g. dynamically spawned) can
/// be registered manually via RegisterSource(audioSource).
///
/// Tweak the reverb settings in the Inspector — changes apply immediately
/// via the [ContextMenu] "Refresh All Reverbs" option while in Play mode.
/// </summary>
public class HallReverbManager : MonoBehaviour
{
    [Header("Hall Reverb Settings")]
    [Tooltip("Uncheck to disable the effect entirely without removing the component.")]
    public bool enableReverb = true;

    [Tooltip("How long the reverb tail rings out. Higher = bigger hall.")]
    [Range(0.1f, 5f)]
    public float decayTime = 2.5f;

    [Tooltip("First reflection delay in milliseconds — the first slap off the walls.")]
    [Range(0f, 300f)]
    public float reflectionsDelayMs = 20f;

    [Tooltip("Dry/wet mix. 0 = no reverb, 1 = fully drenched.")]
    [Range(0f, 1f)]
    public float reverbMix = 0.45f;

    [Tooltip("High-frequency damping — lower values make walls sound more absorptive (carpet/wood). " +
             "Higher values keep highs alive (concrete/tile).")]
    [Range(0.1f, 2f)]
    public float decayHFRatio = 0.5f;

    [Tooltip("Diffusion — how spread out the reflections are. 100 = fully diffuse.")]
    [Range(0f, 100f)]
    public float diffusion = 80f;

    [Tooltip("Density of the reverb tail. Higher = smoother, more hall-like.")]
    [Range(0f, 100f)]
    public float density = 80f;

    // Tracks every filter we've applied so we can refresh them
    private readonly List<AudioReverbFilter> _filters = new List<AudioReverbFilter>();

    void Awake()
    {
        ApplyToAllSources();
    }

    /// <summary>
    /// Scans the entire scene for AudioSources and applies the reverb filter to each.
    /// Safe to call multiple times — won't double-add filters.
    /// </summary>
    [ContextMenu("Refresh All Reverbs")]
    public void ApplyToAllSources()
    {
        _filters.Clear();

        // FindObjectsByType includes inactive objects so sources that activate later
        // (e.g. the engine root that starts inactive) are still covered.
        var sources = FindObjectsByType<AudioSource>(FindObjectsInactive.Include, FindObjectsSortMode.None);

        foreach (var src in sources)
            RegisterSource(src);

        Debug.Log($"[HallReverbManager] Applied reverb to {_filters.Count} AudioSource(s).");
    }

    /// <summary>
    /// Adds (or refreshes) an AudioReverbFilter on the same GameObject as the given AudioSource.
    /// Call this for any AudioSource created at runtime after Awake.
    /// </summary>
    public void RegisterSource(AudioSource src)
    {
        if (src == null) return;

        var filter = src.GetComponent<AudioReverbFilter>();
        if (filter == null)
            filter = src.gameObject.AddComponent<AudioReverbFilter>();

        ApplySettings(filter);

        if (!_filters.Contains(filter))
            _filters.Add(filter);
    }

    /// <summary>Pushes the current Inspector values to every tracked filter.</summary>
    void ApplySettings(AudioReverbFilter filter)
    {
        if (filter == null) return;

        filter.enabled      = enableReverb;
        filter.reverbPreset = AudioReverbPreset.User;

        // Decay
        filter.decayTime      = decayTime;
        filter.decayHFRatio   = decayHFRatio;

        // Early reflections
        filter.reflectionsDelay  = reflectionsDelayMs / 1000f; // ms → seconds
        filter.reflectionsLevel  = -1000;

        // Late reverb tail
        filter.reverbDelay  = 0.04f;
        filter.reverbLevel  = Mathf.RoundToInt(Mathf.Lerp(-10000f, 2000f, reverbMix));

        // Room character
        filter.room         = -1000;
        filter.roomHF       = -500;
        filter.roomLF       = 0;
        filter.diffusion    = diffusion;
        filter.density      = density;
        filter.hfReference  = 5000f;
        filter.lfReference  = 250f;
        filter.dryLevel     = 0;
    }

    // ── Runtime toggle helpers ────────────────────────────────────────────────

    /// <summary>Turn the hall effect on or off at runtime without destroying filters.</summary>
    public void SetEnabled(bool on)
    {
        enableReverb = on;
        foreach (var f in _filters)
            if (f != null) f.enabled = on;
    }
}
