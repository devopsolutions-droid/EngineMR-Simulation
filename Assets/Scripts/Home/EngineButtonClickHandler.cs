using UnityEngine;
using UnityEngine.EventSystems;

/// <summary>
/// Added at runtime by EngineButtonWirer to each engine button.
/// Fires on pointer down — works with mouse, touch, and XR ray interactor.
/// </summary>
public class EngineButtonClickHandler : MonoBehaviour, IPointerClickHandler
{
    private EngineData _data;
    private EngineSessionData _sessionData;
    private string _sceneName;

    public EngineData EngineData => _data;

    public void Init(EngineData data, EngineSessionData sessionData, string sceneName)
    {
        _data      = data;
        _sessionData = sessionData;
        _sceneName = sceneName;
    }

    public void OnPointerClick(PointerEventData eventData)
    {
        if (_data == null) return;

        Debug.Log($"[EngineButtonClickHandler] Clicked: {_data.engineName}");
        _sessionData.Select(_data);

        if (SceneTransitionManager.Instance != null)
            SceneTransitionManager.Instance.LoadScene(_sceneName);
        else
            UnityEngine.SceneManagement.SceneManager.LoadScene(_sceneName);
    }
}
