using UnityEngine;

/// <summary>
/// Runtime messenger between HomeScene and EngineViewScene.
/// HomeScene writes SelectedEngine before loading the engine scene.
/// EngineSceneLoader reads it on Start.
/// No DontDestroyOnLoad needed — ScriptableObject assets persist across scene loads.
/// </summary>
[CreateAssetMenu(fileName = "EngineSessionData", menuName = "Engine VR/Engine Session Data")]
public class EngineSessionData : ScriptableObject
{
    [HideInInspector]
    public EngineData selectedEngine;

    public bool HasSelection => selectedEngine != null;

    public void Select(EngineData engine) => selectedEngine = engine;

    public void Clear() => selectedEngine = null;
}
