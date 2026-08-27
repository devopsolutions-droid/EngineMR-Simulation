using System;
using UnityEngine;
using TMPro;
using UnityEngine.UI;

/// <summary>
/// Manages the UI for part exploration.
/// Updates part name, description, and navigation buttons.
/// </summary>
[Obsolete("PartExplorerUIPanel is orphaned (zero cross-references). Use SimplePartExplorer / TabletUIController instead.")]
public class PartExplorerUIPanel : MonoBehaviour
{
    [Header("Part Explorer Controller")]
    [SerializeField] private PartExplorerController explorerController;
    
    [Header("UI Elements")]
    [SerializeField] private TextMeshProUGUI partNameText;
    [SerializeField] private TextMeshProUGUI partDescriptionText;
    [SerializeField] private TextMeshProUGUI partCounterText;
    
    [SerializeField] private Button previousButton;
    [SerializeField] private Button nextButton;
    
    [Header("Settings")]
    [SerializeField] private bool autoHidePanelsWhenInactive = true;

    private void Start()
    {
        if (explorerController == null)
        {
            explorerController = FindFirstObjectByType<PartExplorerController>();
        }
        
        if (explorerController != null)
        {
            explorerController.OnPartChanged += HandlePartChanged;
            explorerController.OnExplorerStarted += HandleExplorerStarted;
            explorerController.OnExplorerEnded += HandleExplorerEnded;
        }
        
        // Wire up buttons
        if (previousButton != null)
            previousButton.onClick.AddListener(OnPreviousClicked);
        
        if (nextButton != null)
            nextButton.onClick.AddListener(OnNextClicked);
    }

    private void OnDestroy()
    {
        if (explorerController != null)
        {
            explorerController.OnPartChanged -= HandlePartChanged;
            explorerController.OnExplorerStarted -= HandleExplorerStarted;
            explorerController.OnExplorerEnded -= HandleExplorerEnded;
        }
        
        if (previousButton != null)
            previousButton.onClick.RemoveListener(OnPreviousClicked);
        
        if (nextButton != null)
            nextButton.onClick.RemoveListener(OnNextClicked);
    }

    private void HandleExplorerStarted()
    {
        // Show UI when explorer starts
        if (partNameText != null) partNameText.gameObject.SetActive(true);
        if (partDescriptionText != null) partDescriptionText.gameObject.SetActive(true);
        if (partCounterText != null) partCounterText.gameObject.SetActive(true);
        if (previousButton != null) previousButton.gameObject.SetActive(true);
        if (nextButton != null) nextButton.gameObject.SetActive(true);
    }

    private void HandleExplorerEnded()
    {
        // Hide UI when explorer ends
        if (autoHidePanelsWhenInactive)
        {
            if (partNameText != null) partNameText.gameObject.SetActive(false);
            if (partDescriptionText != null) partDescriptionText.gameObject.SetActive(false);
            if (partCounterText != null) partCounterText.gameObject.SetActive(false);
            if (previousButton != null) previousButton.gameObject.SetActive(false);
            if (nextButton != null) nextButton.gameObject.SetActive(false);
        }
    }

    private void HandlePartChanged(int partIndex, PartExplorerData.ExplorerPart part)
    {
        if (part == null) return;
        
        // Update part name
        if (partNameText != null)
            partNameText.text = part.partName;
        
        // Update part description
        if (partDescriptionText != null)
            partDescriptionText.text = part.partDescription;
        
        // Update part counter
        if (partCounterText != null)
        {
            int totalParts = explorerController.GetTotalParts();
            partCounterText.text = $"Part {partIndex + 1} / {totalParts}";
        }
        
        // Update button states
        UpdateButtonStates();
    }

    private void UpdateButtonStates()
    {
        if (previousButton != null)
            previousButton.interactable = explorerController.CanGoPrevious();
        
        if (nextButton != null)
            nextButton.interactable = explorerController.CanGoNext();
    }

    private void OnPreviousClicked()
    {
        if (explorerController != null)
            explorerController.PreviousPart();
    }

    private void OnNextClicked()
    {
        if (explorerController != null)
            explorerController.NextPart();
    }

    /// <summary>
    /// Manually show the UI elements.
    /// </summary>
    public void ShowUI()
    {
        if (partNameText != null) partNameText.gameObject.SetActive(true);
        if (partDescriptionText != null) partDescriptionText.gameObject.SetActive(true);
        if (partCounterText != null) partCounterText.gameObject.SetActive(true);
        if (previousButton != null) previousButton.gameObject.SetActive(true);
        if (nextButton != null) nextButton.gameObject.SetActive(true);
    }

    /// <summary>
    /// Manually hide the UI elements.
    /// </summary>
    public void HideUI()
    {
        if (partNameText != null) partNameText.gameObject.SetActive(false);
        if (partDescriptionText != null) partDescriptionText.gameObject.SetActive(false);
        if (partCounterText != null) partCounterText.gameObject.SetActive(false);
        if (previousButton != null) previousButton.gameObject.SetActive(false);
        if (nextButton != null) nextButton.gameObject.SetActive(false);
    }
}
