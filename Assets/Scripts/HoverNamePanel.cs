using UnityEngine;
using TMPro;

/// <summary>
/// Attach this to the static UI panel you design in Unity.
/// The panel starts deactivated and is shown/hidden by EngineInteractor
/// whenever the ray hovers over an engine part.
/// </summary>
public class HoverNamePanel : MonoBehaviour
{
    [Header("References")]
    public TextMeshProUGUI partNameText;

    void Awake()
    {
        // Always start hidden
        gameObject.SetActive(false);
    }

    /// <summary>Called by EngineInteractor when hovering over a part.</summary>
    public void Show(string name)
    {
        if (partNameText != null)
            partNameText.text = name;

        gameObject.SetActive(true);
    }

    /// <summary>Called by EngineInteractor when the ray leaves a part.</summary>
    public void Hide()
    {
        gameObject.SetActive(false);
    }
}
