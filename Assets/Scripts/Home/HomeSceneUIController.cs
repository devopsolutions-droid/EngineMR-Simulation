using UnityEngine;
using UnityEngine.UI;
using Unity.XR.CoreUtils;

/// <summary>
/// Controls the two-panel EngineButtons HomeScene flow:
///   1. Start Page  — shown on first load
///   2. Engine Scroll Panel — shown after Start is clicked
///
/// Also handles returning from the Engine scene (skips Start Page).
/// </summary>
public class HomeSceneUIController : MonoBehaviour
{
    [Header("Panels")]
    [Tooltip("The Start Page root GameObject (shown first).")]
    public GameObject startUI;

    [Tooltip("The Engine Selection panel root GameObject (shown after Start is clicked).")]
    public GameObject scrollCanvas;

    [Header("Buttons")]
    [Tooltip("The START button on the Start Page.")]
    public Button startButton;

    [Tooltip("The BACK button on the Engine Selection panel.")]
    public Button backButton;

    [Header("Scroll")]
    [Tooltip("ScrollNavigator on the Engine Selection panel — initialized after panel is shown.")]
    public ScrollNavigator scrollNavigator;

    // ── Set to true by EngineSceneLoader.GoHome() before loading EngineButtons HomeScene ──
    public static bool ReturnToScroll = false;

    void Start()
    {
        // Wire buttons
        if (startButton != null)
            startButton.onClick.AddListener(OnStartButtonClicked);
        else
            Debug.LogError("[HomeSceneUIController] startButton is NOT assigned!");

        if (backButton != null)
            backButton.onClick.AddListener(OnBackButtonClicked);

        // Decide which panel to show
        if (ReturnToScroll)
        {
            ReturnToScroll = false;
            ShowScrollPanel();
        }
        else
        {
            ShowStartPage();
        }
    }

    // ── Public so buttons can also call these directly via Inspector OnClick ──

    public void OnStartButtonClicked()
    {
        ShowScrollPanel();
    }

    public void OnBackButtonClicked()
    {
        ShowStartPage();
    }

    public void OnExitButtonClicked()
    {
#if UNITY_EDITOR
        UnityEditor.EditorApplication.isPlaying = false;
#else
        Application.Quit();
#endif
    }

    // ─────────────────────────────────────────────────────────────────────────

    void ShowStartPage()
    {
        if (startUI != null)     startUI.SetActive(true);
        if (scrollCanvas != null) scrollCanvas.SetActive(false);
    }

    void ShowScrollPanel()
    {
        if (startUI != null)     startUI.SetActive(false);
        if (scrollCanvas != null) scrollCanvas.SetActive(true);

        scrollNavigator?.InitNow();
    }
}
