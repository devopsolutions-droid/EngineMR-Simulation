using System.Collections;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

/// <summary>
/// A simple, from-scratch search input script for VR.
/// Blinks a '|' cursor when clicked and filters the engine selection list.
/// </summary>
public class VRSearchInput : MonoBehaviour
{
    [Header("UI Display")]
    public TextMeshProUGUI displayText;

    [Header("Placeholder Settings")]
    public string placeholderText = "Search...";
    [Tooltip("Drag and drop the placeholder text GameObject here so it automatically hides when the search bar is focused.")]
    public GameObject placeholderObject;

    [Header("Blink Settings")]
    public float blinkRate = 0.5f;

    private string _query = "";
    private bool _isFocused = false;
    private bool _showCursor = false;
    private Coroutine _blinkCoroutine;

    public string CurrentQuery => _query;
    public bool IsFocused => _isFocused;

    void Start()
    {
        // Try to find the Text component automatically in children if not assigned
        if (displayText == null)
        {
            displayText = GetComponentInChildren<TextMeshProUGUI>();
        }

        UpdateUI();

        // Listen for Unity standard Button clicks
        var button = GetComponent<Button>();
        if (button != null)
        {
            button.onClick.AddListener(FocusInput);
        }

        // Listen for UltimateClean's CleanButton clicks if present
        var cleanButton = GetComponent<UltimateClean.CleanButton>();
        if (cleanButton != null)
        {
            cleanButton.OnClicked.AddListener(FocusInput);
        }
    }

    public void FocusInput()
    {
        if (_isFocused) return;
        _isFocused = true;
        _showCursor = true;
        Debug.Log("[VRSearchInput] Focused.");

        if (_blinkCoroutine != null) StopCoroutine(_blinkCoroutine);
        _blinkCoroutine = StartCoroutine(BlinkCaret());
    }

    public void UnfocusInput()
    {
        if (!_isFocused) return;
        _isFocused = false;
        Debug.Log($"[VRSearchInput] Unfocused. StackTrace: {System.Environment.StackTrace}");

        if (_blinkCoroutine != null)
        {
            StopCoroutine(_blinkCoroutine);
            _blinkCoroutine = null;
        }

        UpdateUI();
    }

    private IEnumerator BlinkCaret()
    {
        while (_isFocused)
        {
            _showCursor = !_showCursor;
            UpdateUI();
            yield return new WaitForSeconds(blinkRate);
        }
    }

    // ── Input API (Call these from your keyboard keys) ──

    public void InputCharacter(char c)
    {
        _query += c;
        UpdateUI();
        FilterList(_query);
    }

    public void DeleteCharacter()
    {
        if (_query.Length > 0)
        {
            _query = _query.Substring(0, _query.Length - 1);
            UpdateUI();
            FilterList(_query);
        }
    }

    public void ClearInput()
    {
        _query = "";
        UpdateUI();
        FilterList(_query);
    }

    private void UpdateUI()
    {
        if (displayText == null) return;

        if (_isFocused)
        {
            displayText.text = _query + (_showCursor ? "|" : "");
            if (placeholderObject != null)
            {
                placeholderObject.SetActive(false);
            }
        }
        else
        {
            bool queryEmpty = string.IsNullOrEmpty(_query);
            if (placeholderObject != null)
            {
                placeholderObject.SetActive(queryEmpty);
                displayText.text = queryEmpty ? "" : _query;
            }
            else
            {
                displayText.text = queryEmpty ? placeholderText : _query;
            }
        }
    }

    private void FilterList(string searchVal)
    {
        // Remove spaces and convert to lowercase for case-and-space-insensitive comparison
        string cleanQuery = searchVal.Replace(" ", "").ToLower();
        Debug.Log($"[VRSearchInput] FilterList called with query: '{searchVal}' (cleaned: '{cleanQuery}')");

        // Locate the EngineButtonWirer in the scene to find its button container
        var wirer = FindFirstObjectByType<EngineButtonWirer>();
        if (wirer == null)
        {
            Debug.LogError("[VRSearchInput] FilterList failed: EngineButtonWirer not found in scene!");
            return;
        }
        if (wirer.buttonContainer == null)
        {
            Debug.LogError("[VRSearchInput] FilterList failed: wirer.buttonContainer is null!");
            return;
        }

        // Show/hide button children based on the query matching the engine name from the handler
        foreach (Transform child in wirer.buttonContainer)
        {
            // Only filter actual engine buttons (with EngineButtonClickHandler)
            var handler = child.GetComponent<EngineButtonClickHandler>();
            if (handler == null) continue;

            string engineName = (handler.EngineData != null) ? handler.EngineData.engineName : "";
            string cleanEngineName = engineName.Replace(" ", "").ToLower();

            bool matches = string.IsNullOrEmpty(cleanQuery) || 
                           cleanEngineName.Contains(cleanQuery);
            child.gameObject.SetActive(matches);

            Debug.Log($"[VRSearchInput] Child: '{child.name}', engineName='{engineName}', matches={matches} -> SetActive({matches})");
        }

        // Recalculate ScrollRect content width and reset scroll position using ScrollNavigator
        var scrollNav = FindFirstObjectByType<ScrollNavigator>();
        if (scrollNav != null)
        {
            scrollNav.RefreshLayoutAndResetScroll();
        }
    }

    private void PrintComponentsRecursive(Transform t, string indent)
    {
        Component[] components = t.GetComponents<Component>();
        string comps = "";
        foreach (var c in components)
        {
            if (c != null) comps += c.GetType().Name + ", ";
        }
        Debug.Log($"[VRSearchInput] {indent}GameObject: '{t.name}' (Active={t.gameObject.activeSelf}) -> Components: {comps}");
        for (int i = 0; i < t.childCount; i++)
        {
            PrintComponentsRecursive(t.GetChild(i), indent + "  ");
        }
    }
}
