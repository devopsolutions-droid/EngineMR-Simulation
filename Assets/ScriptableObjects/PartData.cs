using UnityEngine;

/// <summary>
/// One asset per engine part.
/// Holds all content data: name, description, audio.
/// Assign to EnginePart.partData at runtime or in the prefab.
/// </summary>
[CreateAssetMenu(fileName = "NewPartData", menuName = "Engine VR/Part Data")]
public class PartData : ScriptableObject
{
    [Header("Identity")]
    public string partName = "Part Name";

    [TextArea(3, 6)]
    public string description = "Part description here.";

    [Header("Audio")]
    [Tooltip("Audio clip that plays when this part is selected.")]
    public AudioClip audioExplanation;
}
