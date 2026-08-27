using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;

/// <summary>
/// Attach this to ANY active GameObject in your scene (e.g. the EngineGrabManager
/// or any always-on manager object). At Start it scans the entire scene for every
/// Button and wires hover + click sounds automatically — no per-button work needed.
/// </summary>
public class UIButtonSoundManager : MonoBehaviour
{
    [Header("Hover Sound")]
    [Tooltip("Plays when the XR ray first enters any UI button in the scene.")]
    public AudioClip hoverSound;

    [Range(0f, 1f)]
    public float hoverVolume = 0.6f;

    [Header("Click Sound")]
    [Tooltip("Plays when any UI button in the scene is clicked.")]
    public AudioClip clickSound;

    [Range(0f, 1f)]
    public float clickVolume = 1f;

    private AudioSource _audioSource;

    void Start()
    {
        // ── Shared AudioSource (2D, no spatial falloff) ───────────────────────
        _audioSource = gameObject.AddComponent<AudioSource>();
        _audioSource.playOnAwake  = false;
        _audioSource.spatialBlend = 0f;
        _audioSource.volume       = 1f;

        // Pre-warm to remove first-play latency
        PreWarm();

        // ── Wire ALL buttons in the entire scene ──────────────────────────────
        // includeInactive: true catches buttons on hidden panels (x-ray reset, etc.)
        Button[] allButtons = FindObjectsByType<Button>(FindObjectsInactive.Include, FindObjectsSortMode.None);

        int wired = 0;
        foreach (Button btn in allButtons)
        {
            // ── Click sound ───────────────────────────────────────────────────
            btn.onClick.AddListener(PlayClick);

            // ── Hover sound via EventTrigger ──────────────────────────────────
            EventTrigger trigger = btn.gameObject.GetComponent<EventTrigger>();
            if (trigger == null)
                trigger = btn.gameObject.AddComponent<EventTrigger>();

            // Guard: don't double-add if script restarts
            bool alreadyHasHover = false;
            foreach (var e in trigger.triggers)
            {
                if (e.eventID == EventTriggerType.PointerEnter)
                { alreadyHasHover = true; break; }
            }

            if (!alreadyHasHover)
            {
                var entry = new EventTrigger.Entry { eventID = EventTriggerType.PointerEnter };
                entry.callback.AddListener(_ => PlayHover());
                trigger.triggers.Add(entry);
            }

            wired++;
        }

        Debug.Log($"[UIButtonSoundManager] Wired hover + click sounds to {wired} buttons across the scene.");
    }

    void PlayHover()
    {
        if (hoverSound != null && _audioSource != null)
            _audioSource.PlayOneShot(hoverSound, hoverVolume);
    }

    void PlayClick()
    {
        if (clickSound != null && _audioSource != null)
            _audioSource.PlayOneShot(clickSound, clickVolume);
    }

    void PreWarm()
    {
        AudioClip warm = hoverSound != null ? hoverSound : clickSound;
        if (warm == null || _audioSource == null) return;
        _audioSource.clip   = warm;
        _audioSource.volume = 0f;
        _audioSource.Play();
        _audioSource.Stop();
        _audioSource.volume = 1f;
        _audioSource.clip   = null;
    }
}
