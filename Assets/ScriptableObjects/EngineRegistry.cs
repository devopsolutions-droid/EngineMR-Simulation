using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Single source of truth for all available engines.
/// Add a new EngineData asset here to make it appear on the home screen automatically.
/// </summary>
[CreateAssetMenu(fileName = "EngineRegistry", menuName = "Engine VR/Engine Registry")]
public class EngineRegistry : ScriptableObject
{
    [Tooltip("All available engine models. Order determines display order on home screen.")]
    public List<EngineData> engines = new();

    public int Count => engines?.Count ?? 0;

    public EngineData Get(int index)
    {
        if (engines == null || index < 0 || index >= engines.Count) return null;
        return engines[index];
    }
}
