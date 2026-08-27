using UnityEngine;
using TMPro;

public class PartInfoPanel : MonoBehaviour
{
    [Header("UI References")]
    public TextMeshProUGUI titleText;
    public TextMeshProUGUI descriptionText;

    private string _defaultTitle = "F6 Boxer Engine";
    private string _defaultDescription = "An F6 (Flat-6) boxer engine is a six-cylinder internal combustion engine with horizontally opposed cylinder banks (three on each side) where opposing pistons move in and out simultaneously, acting like sparring boxers. Famous for powering Porsche 911 models and select Subaru vehicles.";

    void Awake() => ResetToDefault();

    /// <summary>Called by EngineSceneLoader to set engine-specific default text.</summary>
    public void SetDefault(string title, string description)
    {
        _defaultTitle = title;
        _defaultDescription = description;
        ResetToDefault();
    }

    public void Show(EnginePart part)
    {
        titleText.text = part.PartName;
        descriptionText.text = part.Description;
    }

    public void Hide() => ResetToDefault();

    private void ResetToDefault()
    {
        titleText.text = _defaultTitle;
        descriptionText.text = _defaultDescription;
    }
}
