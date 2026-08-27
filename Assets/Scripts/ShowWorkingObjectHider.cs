using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Hides specified GameObjects when Show Working mode is active and restores their
/// original active state when Show Working stops.
///
/// Automatically excludes objects that are already managed by JetEngineAirflowController
/// (such as outer covers) to prevent competing SetActive calls.
///
/// Uses EngineViewManager.OnShowWorkingActiveChanged event instead of per-frame polling.
///
/// Usage:
///   1. Attach this to any GameObject in the scene.
///   2. Drag the objects you want hidden during Show Working into the array.
///   3. Their original active states are saved on Start and restored on stop.
/// </summary>
public class ShowWorkingObjectHider : MonoBehaviour
{
    [Header("These GameObjects are HIDDEN during Show Working")]
    [Tooltip("Objects will be deactivated when Show Working starts and restored on stop. " +
             "Do NOT add outer covers here — JetEngineAirflowController already manages them with slide animation.")]
    public GameObject[] objectsToHide;

    // Objects that are already managed by other systems — we skip these
    private HashSet<GameObject> _managedByOthers;

    private bool[] _originalStates;

    void Start()
    {
        // Detect objects already managed by JetEngineAirflowController so we skip them
        _managedByOthers = new HashSet<GameObject>();
        var airflowControllers = FindObjectsByType<JetEngineAirflowController>(FindObjectsSortMode.None);
        foreach (var ctrl in airflowControllers)
        {
            if (ctrl.outerCovers != null)
                foreach (var cover in ctrl.outerCovers)
                    if (cover != null) _managedByOthers.Add(cover);
        }

        // Save original active states so we can restore them correctly
        _originalStates = new bool[objectsToHide.Length];
        for (int i = 0; i < objectsToHide.Length; i++)
        {
            if (objectsToHide[i] == null) continue;

            // Warn if this object is already managed by the airflow controller
            if (_managedByOthers.Contains(objectsToHide[i]))
            {
                Debug.LogWarning($"[ShowWorkingObjectHider] '{objectsToHide[i].name}' is already managed by " +
                                 "JetEngineAirflowController (outerCovers). Skipping to avoid conflict.", this);
                _originalStates[i] = objectsToHide[i].activeSelf; // still save, but we'll skip applying
                continue;
            }

            _originalStates[i] = objectsToHide[i].activeSelf;
        }

        // Subscribe to event-driven state changes instead of per-frame polling
        EngineViewManager.OnShowWorkingActiveChanged += OnShowWorkingStateChanged;

        // Apply current state immediately in case Show Working is already active when this starts
        if (EngineViewManager.IsShowWorkingActive)
            ApplyHide();
    }

    void OnDestroy()
    {
        EngineViewManager.OnShowWorkingActiveChanged -= OnShowWorkingStateChanged;
    }

    private void OnShowWorkingStateChanged(bool isActive)
    {
        if (isActive)
            ApplyHide();
        else
            ApplyRestore();
    }

    private void ApplyHide()
    {
        for (int i = 0; i < objectsToHide.Length; i++)
        {
            if (objectsToHide[i] == null) continue;
            if (_managedByOthers.Contains(objectsToHide[i])) continue;
            objectsToHide[i].SetActive(false);
        }
    }

    private void ApplyRestore()
    {
        for (int i = 0; i < objectsToHide.Length; i++)
        {
            if (objectsToHide[i] == null) continue;
            if (_managedByOthers.Contains(objectsToHide[i])) continue;
            objectsToHide[i].SetActive(_originalStates[i]);
        }
    }
}