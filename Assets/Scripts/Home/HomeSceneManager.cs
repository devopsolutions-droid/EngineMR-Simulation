using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

/// <summary>
/// Drives the home screen panel.
/// Reads EngineRegistry, fills a 3x3 grid of EngineCardUI prefabs, handles pagination.
/// </summary>
public class HomeSceneManager : MonoBehaviour
{
    [Header("Data")]
    public EngineRegistry engineRegistry;
    public EngineSessionData sessionData;

    [Header("Scene")]
    [Tooltip("Exact name of the engine view scene in Build Settings.")]
    public string engineSceneName = "Main Scene";

    [Header("Card Grid")]
    [Tooltip("Prefab with EngineCardUI component.")]
    public GameObject engineCardPrefab;
    [Tooltip("Parent transform (Grid Layout Group) where cards are spawned.")]
    public Transform cardGridParent;

    [Header("Pagination")]
    public Button prevButton;
    public Button nextButton;
    public TextMeshProUGUI pageIndicatorText;

    [Header("Transition")]
    public GameObject sceneTransitionPrefab; // drag SceneTransitionManager prefab here

    private const int CardsPerPage = 9; // 3x3
    private int _currentPage = 0;
    private int _totalPages = 1;
    private readonly List<GameObject> _activeCards = new();

    void Start()
    {
        // Ensure transition manager exists
        if (SceneTransitionManager.Instance == null && sceneTransitionPrefab != null)
            Instantiate(sceneTransitionPrefab);

        if (engineRegistry == null || engineRegistry.Count == 0)
        {
            Debug.LogError("[HomeSceneManager] EngineRegistry is empty or not assigned!");
            return;
        }

        _totalPages = Mathf.CeilToInt((float)engineRegistry.Count / CardsPerPage);
        _currentPage = 0;

        prevButton?.onClick.AddListener(PrevPage);
        nextButton?.onClick.AddListener(NextPage);

        RenderPage(_currentPage);
    }

    void RenderPage(int page)
    {
        // Clear existing cards
        foreach (var card in _activeCards)
            Destroy(card);
        _activeCards.Clear();

        int startIndex = page * CardsPerPage;
        int endIndex = Mathf.Min(startIndex + CardsPerPage, engineRegistry.Count);

        for (int i = startIndex; i < endIndex; i++)
        {
            var data = engineRegistry.Get(i);
            if (data == null) continue;

            var cardGO = Instantiate(engineCardPrefab, cardGridParent);
            var cardUI = cardGO.GetComponent<EngineCardUI>();
            if (cardUI != null)
                cardUI.Init(data, sessionData, engineSceneName);

            _activeCards.Add(cardGO);
        }

        UpdatePaginationUI();
    }

    void UpdatePaginationUI()
    {
        if (pageIndicatorText != null)
            pageIndicatorText.text = $"{_currentPage + 1} / {_totalPages}";

        if (prevButton != null) prevButton.interactable = _currentPage > 0;
        if (nextButton != null) nextButton.interactable = _currentPage < _totalPages - 1;
    }

    void PrevPage()
    {
        if (_currentPage <= 0) return;
        _currentPage--;
        RenderPage(_currentPage);
    }

    void NextPage()
    {
        if (_currentPage >= _totalPages - 1) return;
        _currentPage++;
        RenderPage(_currentPage);
    }
}
