using System.Collections;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;
using TMPro;
using UltimateClean;

/// <summary>
/// Plays a 3-step onboarding sequence when the scene loads:
///   Step 1 — Welcome message  (TMP + Audio)
///   Step 2 — Grab Tablet hint (TMP + Audio)
///   Step 3 — Thank You        (TMP + Audio)
///
/// Each step waits for its audio clip to finish before moving to the next.
/// Hall reverb is handled globally by HallReverbManager — no reverb code here.
/// Wire everything in the Inspector, then call nothing — it runs automatically on Start.
/// </summary>
public class OnboardingGuide : MonoBehaviour
{
    [Header("Panel")]
    [Tooltip("The root panel GameObject. Will be shown at start and hidden after the last step.")]
    public GameObject panelRoot;

    [Header("Tutorial Player Integration")]
    [Tooltip("The Tutorial Player screen that should be managed.")]
    public GameObject tutorialPlayerGO;

    [Tooltip("The Explore button GameObject on the Tutorial Player screen. Can be a standard Unity Button, custom CleanButton, or any graphic with a Raycast Target.")]
    public GameObject exploreButton;

    [Header("Step 1 — Welcome")]
    public TextMeshProUGUI welcomeText;
    public AudioClip       welcomeClip;

    [Header("Step 2 — Grab Tablet")]
    public TextMeshProUGUI grabTabletText;
    public AudioClip       grabTabletClip;

    [Header("Step 3 — Thank You")]
    public TextMeshProUGUI thankYouText;
    public AudioClip       thankYouClip;

    [Header("Timing")]
    [Tooltip("Seconds to wait before the sequence begins (gives the scene time to fully load).")]
    public float startDelay = 1f;

    [Tooltip("Seconds to wait between each step (after audio finishes, before next step starts).")]
    public float stepGapDuration = 0.5f;

    [Tooltip("Seconds to keep the last step visible before fading out.")]
    public float endHoldDuration = 1.5f;

    [Tooltip("Duration of the fade-out animation in seconds.")]
    public float fadeOutDuration = 1f;

    private AudioSource _audioSource;
    private CanvasGroup _canvasGroup;
    private bool _hasStarted = false;

    void Awake()
    {
        // ── AudioSource ───────────────────────────────────────────────────────
        _audioSource = GetComponent<AudioSource>();
        if (_audioSource == null)
            _audioSource = gameObject.AddComponent<AudioSource>();

        _audioSource.playOnAwake  = false;
        _audioSource.spatialBlend = 0f; // 2D — UI audio

        // ── CanvasGroup for panel fade ────────────────────────────────────────
        if (panelRoot != null)
        {
            _canvasGroup = panelRoot.GetComponent<CanvasGroup>();
            if (_canvasGroup == null)
                _canvasGroup = panelRoot.AddComponent<CanvasGroup>();
        }
    }

    void Start()
    {
        // Hide onboarding text labels at the start
        SetAllTextsVisible(false);

        // Hide the main onboarding panel initially
        if (panelRoot != null)
            panelRoot.SetActive(false);

        if (_canvasGroup != null)
            _canvasGroup.alpha = 0f;

        // If we have a Tutorial Player screen, wait for the Explore button click
        if (tutorialPlayerGO != null)
        {
            // Make sure the Tutorial Player screen is active (remains at its designed position)
            tutorialPlayerGO.SetActive(true);

            if (exploreButton != null)
            {
                BindExploreButton(exploreButton);
            }
            else
            {
                // Fallback: try to find a button in the children of tutorialPlayerGO
                Transform btnTransform = FindButtonInChildren(tutorialPlayerGO.transform);
                if (btnTransform != null)
                {
                    exploreButton = btnTransform.gameObject;
                    BindExploreButton(exploreButton);
                    Debug.Log($"[OnboardingGuide] Auto-bound Explore button to '{exploreButton.name}' in children of '{tutorialPlayerGO.name}'");
                }
                else
                {
                    // No button found — start onboarding immediately as fallback
                    Debug.LogWarning("[OnboardingGuide] Tutorial Player GO is assigned, but no Explore button was found/assigned. Starting onboarding immediately.");
                    StartOnboardingSequence();
                }
            }
        }
        else
        {
            // Classic behavior if no Tutorial Player is assigned
            StartOnboardingSequence();
        }
    }

    void BindExploreButton(GameObject btnGo)
    {
        bool bound = false;

        // 1. Try standard Unity Button
        Button unityBtn = btnGo.GetComponent<Button>();
        if (unityBtn != null)
        {
            unityBtn.onClick.AddListener(OnExploreButtonClicked);
            bound = true;
            Debug.Log($"[OnboardingGuide] Bound listener to standard Unity Button on '{btnGo.name}'");
        }

        // 2. Try UltimateClean CleanButton
        CleanButton cleanBtn = btnGo.GetComponent<CleanButton>();
        if (cleanBtn != null)
        {
            cleanBtn.OnClicked.AddListener(OnExploreButtonClicked);
            bound = true;
            Debug.Log($"[OnboardingGuide] Bound listener to CleanButton on '{btnGo.name}'");
        }

        // 3. Fallback: Pointer click handler (covers custom meshes, images, etc.)
        if (!bound)
        {
            OnboardingClickHelper helper = btnGo.AddComponent<OnboardingClickHelper>();
            helper.onClick += OnExploreButtonClicked;
            Debug.Log($"[OnboardingGuide] Bound listener via OnboardingClickHelper to '{btnGo.name}'");
        }
    }

    Transform FindButtonInChildren(Transform parent)
    {
        foreach (Transform child in parent.GetComponentsInChildren<Transform>(true))
        {
            string nameLower = child.gameObject.name.ToLower();
            if (nameLower.Contains("explore") || nameLower.Contains("button") || nameLower.Contains("exp"))
            {
                // Make sure it has an Image or Graphic component (raycast target)
                if (child.GetComponent<Graphic>() != null)
                {
                    return child;
                }
            }
        }
        return null;
    }

    void OnExploreButtonClicked()
    {
        // Unregister listeners/helpers
        if (exploreButton != null)
        {
            Button unityBtn = exploreButton.GetComponent<Button>();
            if (unityBtn != null)
                unityBtn.onClick.RemoveListener(OnExploreButtonClicked);

            CleanButton cleanBtn = exploreButton.GetComponent<CleanButton>();
            if (cleanBtn != null)
                cleanBtn.OnClicked.RemoveListener(OnExploreButtonClicked);

            OnboardingClickHelper helper = exploreButton.GetComponent<OnboardingClickHelper>();
            if (helper != null)
                Destroy(helper);
        }

        // Trigger the sequence
        StartOnboardingFromButton();
    }

    /// <summary>
    /// Public entry point. Can be wired directly to any Button's click event list in the Unity Inspector.
    /// </summary>
    public void StartOnboardingFromButton()
    {
        if (_hasStarted) return;
        _hasStarted = true;

        // Hide the Tutorial Player screen
        if (tutorialPlayerGO != null)
        {
            tutorialPlayerGO.SetActive(false);
        }

        // Start welcoming audio and onboarding sequence (skip start delay because they clicked button)
        StartOnboardingSequence(true);
    }

    void StartOnboardingSequence(bool skipStartDelay = false)
    {
        if (panelRoot != null)
            panelRoot.SetActive(true);

        if (_canvasGroup != null)
            _canvasGroup.alpha = 1f;

        StartCoroutine(RunSequence(skipStartDelay));
    }

    IEnumerator RunSequence(bool skipStartDelay = false)
    {
        // Wait for scene to settle if not skipped, otherwise wait a short 0.7s delay for UI transition
        if (!skipStartDelay)
        {
            yield return new WaitForSeconds(startDelay);
        }
        else
        {
            yield return new WaitForSeconds(0.7f);
        }

        // ── Step 1: Welcome ───────────────────────────────────────────────────
        yield return PlayStep(welcomeText, welcomeClip);
        yield return new WaitForSeconds(stepGapDuration);

        // ── Step 2: Grab Tablet ───────────────────────────────────────────────
        yield return PlayStep(grabTabletText, grabTabletClip);
        yield return new WaitForSeconds(stepGapDuration);

        // ── Step 3: Thank You ─────────────────────────────────────────────────
        yield return PlayStep(thankYouText, thankYouClip);

        // Hold the last step briefly, then fade out
        yield return new WaitForSeconds(endHoldDuration);

        // Fade out the entire panel (text + background + everything)
        yield return FadeOutPanel();

        // Hide the panel after fade completes
        if (panelRoot != null)
            panelRoot.SetActive(false);
    }

    /// <summary>
    /// Shows the given TMP label, plays the audio clip, waits for it to finish.
    /// </summary>
    IEnumerator PlayStep(TextMeshProUGUI label, AudioClip clip)
    {
        // Hide all, then show only this step's label
        SetAllTextsVisible(false);

        if (label != null)
            label.gameObject.SetActive(true);

        if (clip != null)
        {
            _audioSource.clip = clip;
            _audioSource.Play();
            yield return new WaitForSeconds(clip.length);
        }
        else
        {
            // No clip assigned — hold the text for 2 seconds as fallback
            yield return new WaitForSeconds(2f);
        }
    }

    void SetAllTextsVisible(bool visible)
    {
        if (welcomeText    != null) welcomeText.gameObject.SetActive(visible);
        if (grabTabletText != null) grabTabletText.gameObject.SetActive(visible);
        if (thankYouText   != null) thankYouText.gameObject.SetActive(visible);
    }

    /// <summary>
    /// Fades the entire panel (via CanvasGroup) from alpha 1 to 0.
    /// </summary>
    IEnumerator FadeOutPanel()
    {
        if (_canvasGroup == null) yield break;

        float elapsed = 0f;
        _canvasGroup.alpha = 1f;

        while (elapsed < fadeOutDuration)
        {
            elapsed += Time.deltaTime;
            _canvasGroup.alpha = Mathf.Lerp(1f, 0f, elapsed / fadeOutDuration);
            yield return null;
        }

        _canvasGroup.alpha = 0f;
    }
}

/// <summary>
/// Simple helper to catch pointer clicks (mouse, touch, VR raycasts)
/// on any UI GameObject without requiring a standard Unity Button component.
/// </summary>
public class OnboardingClickHelper : MonoBehaviour, IPointerClickHandler
{
    public System.Action onClick;

    public void OnPointerClick(PointerEventData eventData)
    {
        if (eventData.button == PointerEventData.InputButton.Left)
        {
            onClick?.Invoke();
        }
    }
}
