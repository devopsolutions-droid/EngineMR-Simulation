using UnityEngine;
using System.Collections.Generic;

[System.Serializable]
public class EngineInspectionPositionOverride
{
    [Tooltip("The EngineData asset for this engine model.")]
    public EngineData engineData;
    
    [Tooltip("The local position offset to move parts to when inspected (comes closer to the camera/user).")]
    public Vector3 inspectionLocalPosition;
}

/// <summary>
/// Attach this script to a component in your scene (e.g. the EngineInteractor GameObject)
/// to define custom local inspection positions for different engine models.
/// </summary>
public class EngineInspectionSettings : MonoBehaviour
{
    [Header("Inspection Offsets")]
    [Tooltip("List of custom inspection offsets for each engine model.")]
    public List<EngineInspectionPositionOverride> overrides = new List<EngineInspectionPositionOverride>();

    /// <summary>
    /// Gets the custom inspection offset for a given engine model.
    /// Returns null if no override is defined.
    /// </summary>
    public Vector3? GetOverridePosition(EngineData engineData)
    {
        if (engineData == null || overrides == null) return null;
        foreach (var entry in overrides)
        {
            if (entry != null && entry.engineData != null)
            {
                // Match by exact reference OR by engine name to prevent scriptable object reference mismatch
                if (entry.engineData == engineData || 
                    (entry.engineData.engineName != null && engineData.engineName != null && 
                     entry.engineData.engineName.Equals(engineData.engineName, System.StringComparison.OrdinalIgnoreCase)))
                {
                    return entry.inspectionLocalPosition;
                }
            }
        }
        return null;
    }
}
