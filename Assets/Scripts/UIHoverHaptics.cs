using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.XR.Interaction.Toolkit;
using UnityEngine.XR.Interaction.Toolkit.UI;

/// <summary>
/// Attach this to every tablet UI button (Image-based, not Unity Button).
/// Handles both haptics AND sounds on hover and click.
///
/// SOUND SETUP — one-time only:
///   Pick ANY one instance in the Inspector, set Hover Sound + Click Sound.
///   All other instances automatically share the same clips via static fields.
///   You never need to set the clips more than once.
/// </summary>
public class UIHoverHaptics : MonoBehaviour, IPointerEnterHandler, IPointerClickHandler
{
    // ── Haptics ───────────────────────────────────────────────────────────────
    [Header("Hover Haptics")]
    [Range(0f, 1f)] public float hoverAmplitude = 0.15f;
    public float hoverDuration = 0.04f;

    [Header("Click Haptics")]
    [Range(0f, 1f)] public float clickAmplitude = 0.35f;
    public float clickDuration = 0.08f;

    // ── Sounds ────────────────────────────────────────────────────────────────
    [Header("Button Sounds  (set once — shared across ALL tablet buttons)")]
    [Tooltip("Drag your hover SFX here on ANY one button. All buttons share it automatically.")]
    public AudioClip hoverSound;

    [Tooltip("Drag your click SFX here on ANY one button. All buttons share it automatically.")]
    public AudioClip clickSound;

    [Range(0f, 1f)] public float hoverVolume = 0.6f;
    [Range(0f, 1f)] public float clickVolume = 1.0f;

    // Shared across every instance so you only drag-and-drop once
    private static AudioSource _sharedSource;
    private static AudioClip   _sharedHover;
    private static AudioClip   _sharedClick;

    // ── Lifecycle ─────────────────────────────────────────────────────────────

    void Awake()
    {
        // Register any clips assigned to this instance into the shared statics
        if (hoverSound != null) _sharedHover = hoverSound;
        if (clickSound  != null) _sharedClick  = clickSound;

        // Create the shared AudioSource once (on the first instance to wake up)
        if (_sharedSource == null)
        {
            var go = new GameObject("[UIButtonSounds]");
            DontDestroyOnLoad(go);
            _sharedSource = go.AddComponent<AudioSource>();
            _sharedSource.playOnAwake  = false;
            _sharedSource.spatialBlend = 0f; // pure 2D
            _sharedSource.volume       = 1f;
            PreWarm();
        }
    }

    // ── Pointer Events ────────────────────────────────────────────────────────

    public void OnPointerEnter(PointerEventData eventData)
    {
        TriggerHaptic(eventData, hoverAmplitude, hoverDuration);
        PlaySound(_sharedHover, hoverVolume);
    }

    public void OnPointerClick(PointerEventData eventData)
    {
        TriggerHaptic(eventData, clickAmplitude, clickDuration);
        PlaySound(_sharedClick, clickVolume);
    }

    // ── Helpers ───────────────────────────────────────────────────────────────

    private static void PlaySound(AudioClip clip, float volume)
    {
        if (clip != null && _sharedSource != null)
            _sharedSource.PlayOneShot(clip, volume);
    }

    /// Pre-warms the AudioSource to eliminate first-play delay.
    private static void PreWarm()
    {
        var warm = _sharedHover != null ? _sharedHover : _sharedClick;
        if (warm == null || _sharedSource == null) return;
        _sharedSource.clip   = warm;
        _sharedSource.volume = 0f;
        _sharedSource.Play();
        _sharedSource.Stop();
        _sharedSource.volume = 1f;
        _sharedSource.clip   = null;
    }

    private void TriggerHaptic(PointerEventData eventData, float amplitude, float duration)
    {
        if (eventData == null) return;
        if (EventSystem.current != null &&
            EventSystem.current.currentInputModule is XRUIInputModule xrModule)
        {
            var interactor = xrModule.GetInteractor(eventData.pointerId);
            if (interactor is XRBaseControllerInteractor ci && ci.xrController != null)
                ci.xrController.SendHapticImpulse(amplitude, duration);
        }
    }
}
