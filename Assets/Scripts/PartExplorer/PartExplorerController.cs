using UnityEngine;
using System;

/// <summary>
/// Controls the part exploration system.
/// Shows one part at a time, fading others to ghost/invisible.
/// </summary>
[Obsolete("PartExplorerController is orphaned (zero cross-references). Use SimplePartExplorer instead.")]
public class PartExplorerController : MonoBehaviour
{
    [SerializeField] private PartExplorerData explorerData;
    
    [Header("Fade Settings")]
    [SerializeField] private float fadeDuration = 0.5f;
    [SerializeField] private float ghostAlpha = 0.2f;
    
    private int _currentPartIndex = -1;
    private bool _isExplorerActive = false;
    
    // Events for UI synchronization
    public event Action<int, PartExplorerData.ExplorerPart> OnPartChanged;
    public event Action OnExplorerStarted;
    public event Action OnExplorerEnded;

    private void Start()
    {
        if (explorerData == null)
        {
            Debug.LogWarning("PartExplorerController: No PartExplorerData assigned!");
        }
    }

    /// <summary>
    /// Starts the part explorer from the first part.
    /// </summary>
    public void StartExplorer()
    {
        if (explorerData == null || explorerData.GetPartCount() == 0)
        {
            Debug.LogWarning("PartExplorerController: No parts available!");
            return;
        }
        
        _isExplorerActive = true;
        _currentPartIndex = -1;
        
        OnExplorerStarted?.Invoke();
        
        // Move to first part
        NextPart();
    }

    /// <summary>
    /// Ends the part explorer and restores all parts.
    /// </summary>
    public void EndExplorer()
    {
        _isExplorerActive = false;
        _currentPartIndex = -1;
        
        // Restore all parts to original state
        if (explorerData != null)
        {
            for (int i = 0; i < explorerData.GetPartCount(); i++)
            {
                var part = explorerData.GetPart(i);
                if (part != null && part.enginePart != null)
                {
                    part.enginePart.RestoreOriginal();
                }
            }
        }
        
        OnExplorerEnded?.Invoke();
    }

    /// <summary>
    /// Moves to the next part.
    /// </summary>
    public void NextPart()
    {
        if (!_isExplorerActive || explorerData == null) return;
        
        int nextIndex = _currentPartIndex + 1;
        
        if (nextIndex >= explorerData.GetPartCount())
        {
            // Explorer complete
            EndExplorer();
            return;
        }
        
        GoToPart(nextIndex);
    }

    /// <summary>
    /// Moves to the previous part.
    /// </summary>
    public void PreviousPart()
    {
        if (!_isExplorerActive || explorerData == null) return;
        
        int prevIndex = _currentPartIndex - 1;
        
        if (prevIndex < 0)
        {
            Debug.LogWarning("PartExplorerController: Already at first part!");
            return;
        }
        
        GoToPart(prevIndex);
    }

    /// <summary>
    /// Jumps directly to a specific part.
    /// </summary>
    public void GoToPart(int partIndex)
    {
        if (!_isExplorerActive || explorerData == null) return;
        
        if (partIndex < 0 || partIndex >= explorerData.GetPartCount())
        {
            Debug.LogWarning($"PartExplorerController: Invalid part index {partIndex}");
            return;
        }
        
        // Restore previous part if any
        if (_currentPartIndex >= 0)
        {
            var prevPart = explorerData.GetPart(_currentPartIndex);
            if (prevPart != null && prevPart.enginePart != null)
            {
                prevPart.enginePart.RestoreOriginal();
            }
        }
        
        _currentPartIndex = partIndex;
        
        var currentPart = explorerData.GetPart(partIndex);
        if (currentPart != null && currentPart.enginePart != null)
        {
            // Highlight current part
            currentPart.enginePart.SetHighlight(true);
            
            // Fade all other parts to ghost
            for (int i = 0; i < explorerData.GetPartCount(); i++)
            {
                if (i != partIndex)
                {
                    var otherPart = explorerData.GetPart(i);
                    if (otherPart != null && otherPart.enginePart != null)
                    {
                        otherPart.enginePart.SetGhost();
                    }
                }
            }
        }
        
        // Notify listeners (UI, etc.)
        OnPartChanged?.Invoke(partIndex, currentPart);
    }

    /// <summary>
    /// Gets the current part index.
    /// </summary>
    public int GetCurrentPartIndex() => _currentPartIndex;

    /// <summary>
    /// Gets the current part.
    /// </summary>
    public PartExplorerData.ExplorerPart GetCurrentPart()
    {
        if (_currentPartIndex >= 0 && explorerData != null)
            return explorerData.GetPart(_currentPartIndex);
        return null;
    }

    /// <summary>
    /// Checks if explorer is currently active.
    /// </summary>
    public bool IsExplorerActive() => _isExplorerActive;

    /// <summary>
    /// Gets total number of parts.
    /// </summary>
    public int GetTotalParts() => explorerData != null ? explorerData.GetPartCount() : 0;

    /// <summary>
    /// Checks if we can move to next part.
    /// </summary>
    public bool CanGoNext() => _isExplorerActive && _currentPartIndex < explorerData.GetPartCount() - 1;

    /// <summary>
    /// Checks if we can move to previous part.
    /// </summary>
    public bool CanGoPrevious() => _isExplorerActive && _currentPartIndex > 0;
}
