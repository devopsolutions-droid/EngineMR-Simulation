using UnityEngine;
using UnityEngine.UI;
using TMPro;

/// <summary>
/// Attached to each engine card prefab in the home screen grid.
/// Populated at runtime by HomeSceneManager.
/// </summary>
public class EngineCardUI : MonoBehaviour
{
    [Header("Card UI Elements")]
    public Image thumbnailImage;
    public TextMeshProUGUI titleText;
    public TextMeshProUGUI categoryText;
    public Button selectButton;

    [Header("Hover Visuals")]
    public GameObject glowBorder;          // optional neon border GameObject
    public CanvasGroup cardCanvasGroup;

    private EngineData _data;
    private EngineSessionData _session;
    private string _engineSceneName;

    /// <summary>Called by HomeSceneManager to bind data to this card.</summary>
    public void Init(EngineData data, EngineSessionData session, string engineSceneName)
    {
        _data = data;
        _session = session;
        _engineSceneName = engineSceneName;

        titleText.text = data.engineName;

        if (categoryText != null)
            categoryText.text = data.engineCategory;

        if (thumbnailImage != null)
            thumbnailImage.sprite = data.thumbnail;

        selectButton.onClick.RemoveAllListeners();
        selectButton.onClick.AddListener(OnCardSelected);

        if (glowBorder != null) glowBorder.SetActive(false);
    }

    void OnCardSelected()
    {
        if (_data == null || _session == null) return;
        _session.Select(_data);

        if (SceneTransitionManager.Instance != null)
            SceneTransitionManager.Instance.LoadScene(_engineSceneName);
        else
            UnityEngine.SceneManagement.SceneManager.LoadScene(_engineSceneName);
    }

    // ── Hover feedback (called by EventTrigger or XR UI events) ──────────────
    public void OnHoverEnter()
    {
        if (glowBorder != null) glowBorder.SetActive(true);
        if (cardCanvasGroup != null)
            cardCanvasGroup.alpha = 1f;
    }

    public void OnHoverExit()
    {
        if (glowBorder != null) glowBorder.SetActive(false);
    }
}
