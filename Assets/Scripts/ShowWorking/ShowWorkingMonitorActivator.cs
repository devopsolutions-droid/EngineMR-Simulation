using UnityEngine;
using System.Collections;

/// <summary>
/// Activates the Show Working Monitor GameObject when Show Working mode starts,
/// and deactivates it when Show Working mode stops.
///
/// Uses CanvasGroup alpha fade for smooth appear/disappear transitions.
/// The monitor GameObject (and its TMP children like monitorPartName /
/// monitorPartDescription) should be inactive by default in the scene hierarchy.
///
/// This script automatically hooks into EngineViewManager.OnShowWorkingActiveChanged
/// so no manual wiring is needed beyond assigning the monitor reference.
/// </summary>
public class ShowWorkingMonitorActivator : MonoBehaviour
{
    [Header("Show Working Monitor")]
    [Tooltip("The root GameObject of the wall monitor / screen display area.\n" +
             "This GameObject must be inactive by default in the scene so that\n" +
             "it stays hidden until Show Working mode activates it.\n" +
             "Its TMP children (monitorPartName, monitorPartDescription, etc.)\n" +
             "are expected to be active under it and will be shown/hidden as a group.\n" +
             "A CanvasGroup component must exist on this GameObject (or a child Canvas)\n" +
             "for the fade effect to work.")]
    [SerializeField] private GameObject showWorkingMonitor;

    [Header("Fade Animation")]
    [SerializeField] private float fadeInDuration  = 0.35f;
    [SerializeField] private float fadeOutDuration = 0.3f;

    private CanvasGroup _canvasGroup;
    private Coroutine   _fadeCoroutine;

    private void OnEnable()
    {
        EngineViewManager.OnShowWorkingActiveChanged += OnShowWorkingStateChanged;
    }

    private void OnDisable()
    {
        EngineViewManager.OnShowWorkingActiveChanged -= OnShowWorkingStateChanged;
    }

    private void Awake()
    {
        if (showWorkingMonitor != null)
            _canvasGroup = showWorkingMonitor.GetComponent<CanvasGroup>();
    }

    private void OnShowWorkingStateChanged(bool isActive)
    {
        if (showWorkingMonitor == null)
        {
            Debug.LogWarning("[ShowWorkingMonitorActivator] showWorkingMonitor reference is not assigned — cannot fade.", this);
            return;
        }

        // Stop any in-progress fade
        if (_fadeCoroutine != null)
        {
            StopCoroutine(_fadeCoroutine);
            _fadeCoroutine = null;
        }

        if (isActive)
        {
            // Activate the GameObject first, then fade alpha in
            showWorkingMonitor.SetActive(true);
            _fadeCoroutine = StartCoroutine(FadeAlpha(0f, 1f, fadeInDuration));
        }
        else
        {
            // Fade alpha out, then deactivate
            _fadeCoroutine = StartCoroutine(FadeOutThenDeactivate());
        }
    }

    private IEnumerator FadeAlpha(float from, float to, float duration)
    {
        if (_canvasGroup == null) yield break;

        float elapsed = 0f;
        while (elapsed < duration)
        {
            elapsed += Time.deltaTime;
            _canvasGroup.alpha = Mathf.Lerp(from, to, elapsed / duration);
            yield return null;
        }
        _canvasGroup.alpha = to;
        _fadeCoroutine = null;
    }

    private IEnumerator FadeOutThenDeactivate()
    {
        if (_canvasGroup != null)
        {
            float elapsed = 0f;
            while (elapsed < fadeOutDuration)
            {
                elapsed += Time.deltaTime;
                _canvasGroup.alpha = Mathf.Lerp(1f, 0f, elapsed / fadeOutDuration);
                yield return null;
            }
            _canvasGroup.alpha = 0f;
        }
        _fadeCoroutine = null;
        showWorkingMonitor.SetActive(false);
    }
}