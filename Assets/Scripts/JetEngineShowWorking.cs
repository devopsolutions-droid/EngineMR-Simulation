// This script drives the airflow visualization tube progress as different parts are shown.
using UnityEngine;

/// <summary>
/// JetEngineShowWorking
/// ─────────────────────
/// Drives the airflow visualisation step by step as the user navigates parts.
///
/// Uses the new progress-based JetEngineAirflowController.SetProgress(float).
///
/// Behaviour (matches the Jet Engine V3 part names exactly):
///   Step 1:  Left Outer Cover / Right Outer Cover → covers slide off, progress = 0 (intake area only)
///   Step 2:  Fan Blades                         → progress 0.03 (fan intake)
///   Step 3:  Frontcap                           → progress 0.10 (intake duct)
///   Step 4:  Innercover1                        → progress 0.20 (compression start)
///   Step 5:  Shaft                              → progress 0.30 (compression deepens)
///   Step 6:  Fuel Cap / Fuel Injector            → progress 0.45 (combustion — white-hot)
///   Step 7:  High pressure Blades                → progress 0.60 (turbine — orange)
///   Step 8:  Highpressure Turbine Shaft          → progress 0.72 (turbine fully active)
///   Step 9:  Cap                                 → progress 0.85 (exhaust entry)
///   Step 10: Wires_set                           → progress 1.00 (full exhaust plume)
///
/// Progress never regresses — pressing Previous keeps the tube filled to the
/// highest progress value reached so far (cumulative, flame-like display).
/// </summary>
public class JetEngineShowWorking : MonoBehaviour
{
    /// <summary>
    /// When true, OnShowWorkingStart() will NOT automatically hide parts (partsToHideOnStart).
    /// Used by ShowWorkingInteractiveController where covers are removed manually by the user.
    /// </summary>
    public bool skipAutoCoverRemoval;

    [Header("References")]
    public JetEngineAirflowController airflowController;
    [Tooltip("Assign the Jet Engine's EngineData asset here to restrict this script to the Jet Engine only.")]
    public EngineData jetEngineData;

    [Header("Parts to Hide on Start")]
    [Tooltip("These GameObjects are deactivated when Show Working starts and restored when it stops. " +
             "Use this for parts like 'Left Outer Cover' and 'Innercover2' that block the view of internal components.")]
    public GameObject[] partsToHideOnStart;

    [Header("Progress Mappings")]
    [Tooltip("Part name fragment → progress value (0..1). The tube fills from intake (0) to exhaust (1).")]
    public ProgressMapping[] progressMappings = new ProgressMapping[]
    {
        // ── Covers (slide off, no progress advancement) ──
        new ProgressMapping { partNameContains = "Left Outer Cover",  progress = -1f },
        new ProgressMapping { partNameContains = "Right Outer Cover", progress = -1f },
        new ProgressMapping { partNameContains = "Outer_cover",       progress = -1f },

        // ── Intake / Fan area (cold blue) ──
        new ProgressMapping { partNameContains = "Fan Blades",        progress = 0.03f },

        // ── Intake duct ──
        new ProgressMapping { partNameContains = "Frontcap",          progress = 0.10f },

        // ── Compression (cyan, air squeezed) ──
        new ProgressMapping { partNameContains = "Innercover1",       progress = 0.20f },
        new ProgressMapping { partNameContains = "Innercover",        progress = 0.20f },
        new ProgressMapping { partNameContains = "Inner Cover",       progress = 0.20f },

        // ── Shaft (compression deepens) ──
        new ProgressMapping { partNameContains = "Shaft",             progress = 0.30f },

        // ── Combustion (white-hot, fuel ignites) ──
        new ProgressMapping { partNameContains = "Fuel Cap",          progress = 0.42f },
        new ProgressMapping { partNameContains = "Fuel Injector",     progress = 0.45f },

        // ── Turbine (orange, energy extracted) ──
        new ProgressMapping { partNameContains = "High pressure Blades",            progress = 0.60f },
        new ProgressMapping { partNameContains = "Highpressure Turbine Shaft",      progress = 0.72f },
        new ProgressMapping { partNameContains = "Highpressure",                   progress = 0.72f },
        new ProgressMapping { partNameContains = "High Pressure",                  progress = 0.60f },

        // ── Exhaust (red plume) ──
        new ProgressMapping { partNameContains = "Cap",               progress = 0.85f },

        // ── Full exhaust plume ──
        new ProgressMapping { partNameContains = "Wires",             progress = 1.00f },

        // ── Skip (individual blades, no visual change) ──
        new ProgressMapping { partNameContains = "Blade",             progress = -2f  },
    };

    [System.Serializable]
    public class ProgressMapping
    {
        public string partNameContains;
        public float  progress; // -1 = slide covers (StartAirflow), -2 = no change, 0..1 = tube fill
    }

    // ── Internal ──────────────────────────────────────────────────────────────
    private float _highestProgress;   // tracks the furthest progress reached so far
    private bool  _coversRemoved;
    private bool[] _originalActiveStates;  // saved original states of partsToHideOnStart

    // ── Public API ────────────────────────────────────────────────────────────

    public void OnShowWorkingStart()
    {
        if (!IsActiveEngineJetEngine()) return;

        _highestProgress = 0f;
        _coversRemoved = false;

        // Hide specified parts (e.g. Left Outer Cover, Innercover2) so the
        // airflow tube and internal components are visible
        // If skipAutoCoverRemoval is true (interactive mode), the user removes covers manually
        if (!skipAutoCoverRemoval)
            HidePartsOnStart();
    }

    public void OnShowWorkingStop()
    {
        if (!IsActiveEngineJetEngine()) return;

        airflowController?.StopAirflow();
        _highestProgress = 0f;
        _coversRemoved = false;

        // Restore hidden parts to their original active state
        RestoreHiddenParts();
    }

    // ── Parts hide / restore ──────────────────────────────────────────────────

    private void HidePartsOnStart()
    {
        if (partsToHideOnStart == null || partsToHideOnStart.Length == 0) return;

        // Save original states on first call
        if (_originalActiveStates == null || _originalActiveStates.Length != partsToHideOnStart.Length)
        {
            _originalActiveStates = new bool[partsToHideOnStart.Length];
        }

        for (int i = 0; i < partsToHideOnStart.Length; i++)
        {
            if (partsToHideOnStart[i] == null) continue;
            _originalActiveStates[i] = partsToHideOnStart[i].activeSelf;
            partsToHideOnStart[i].SetActive(false);
            Debug.Log($"[JetEngineShowWorking] Hidden '{partsToHideOnStart[i].name}' (was {( _originalActiveStates[i] ? "active" : "inactive" )})");
        }
    }

    private void RestoreHiddenParts()
    {
        if (partsToHideOnStart == null || _originalActiveStates == null) return;

        for (int i = 0; i < partsToHideOnStart.Length; i++)
        {
            if (partsToHideOnStart[i] == null) continue;
            if (i < _originalActiveStates.Length)
            {
                partsToHideOnStart[i].SetActive(_originalActiveStates[i]);
                Debug.Log($"[JetEngineShowWorking] Restored '{partsToHideOnStart[i].name}' to {_originalActiveStates[i]}");
            }
        }
    }

    /// <summary>Called by SimplePartExplorer every time a new part is shown.</summary>
    public void OnPartShown(string partName)
    {
        if (!IsActiveEngineJetEngine()) return;

        if (airflowController == null) return;

        float progress = GetProgressForPart(partName);

        // -2 = no visual change for this part (e.g. individual blades)
        if (progress <= -1.5f) return;

        // -1 = cover removal step — slide covers off, fill tube to highest progress (or 0)
        if (progress < 0f)
        {
            if (!_coversRemoved)
            {
                _coversRemoved = true;
                airflowController.StartAirflow(); // slides covers off, shows empty tube at progress 0

                // If we already had progress from a previous zone (shouldn't happen, but be safe)
                if (_highestProgress > 0f)
                    airflowController.SetProgress(_highestProgress);
            }
            return;
        }

        // Clamp to valid range
        progress = Mathf.Clamp01(progress);

        // Only advance the tube if this part pushes progress further
        if (progress > _highestProgress)
        {
            _highestProgress = progress;

            // Ensure covers have been removed before showing tube
            if (!_coversRemoved)
            {
                _coversRemoved = true;
                airflowController.StartAirflow();
            }

            airflowController.SetProgress(_highestProgress);
        }
    }

    // ── Helpers ───────────────────────────────────────────────────────────────

    /// <summary>Exposes the highest airflow progress reached so far (0..1).</summary>
    public float CurrentAirflowProgress
    {
        get { return _highestProgress; }
    }

    /// <summary>
    /// Set the airflow progress value, but only if it's higher than the current highest.
    /// Used by ShowWorkingInteractiveController for cumulative airflow advancement (e.g. turbine start).
    /// </summary>
    /// <param name="progress">Target progress 0..1.</param>
    /// <param name="slideCovers">
    /// If true (default), triggers StartAirflow() which auto-slides ALL outerCovers off the engine.
    /// Set false when the user removes covers manually (SimpleShowWorkingController) so the
    /// right cover stays in place while only the left is removed.
    /// </param>
    public void AdvanceAirflowTo(float progress, bool slideCovers = true)
    {
        if (!IsActiveEngineJetEngine()) return;

        progress = Mathf.Clamp01(progress);

        // Make the tube GameObject visible before setting progress.
        if (!_coversRemoved)
        {
            _coversRemoved = true;

            if (airflowController != null)
            {
                if (slideCovers)
                {
                    // Slide ALL outerCovers off (default interactive mode behaviour)
                    airflowController.StartAirflow();
                }
                else
                {
                    // Show tube immediately WITHOUT sliding covers.
                    // User removes covers manually in SimpleShowWorking flow.
                    airflowController.ShowTubeImmediate();
                }
            }
        }

        if (progress > _highestProgress)
        {
            _highestProgress = progress;
            if (airflowController != null)
                airflowController.SetProgress(_highestProgress);
        }
    }

    /// <summary>
    /// Set airflow progress directly. If cumulative is true, only advances if progress > _highestProgress.
    /// If cumulative is false, sets the value directly without updating _highestProgress (used by Previous button rollback).
    /// </summary>
    public void SetAirflowProgressDirect(float progress, bool cumulative)
    {
        if (!IsActiveEngineJetEngine()) return;

        progress = Mathf.Clamp01(progress);

        // In interactive mode, covers are removed manually by the user, so
        // _coversRemoved may still be false. Trigger StartAirflow to make
        // the tube GameObject visible before setting progress.
        if (!_coversRemoved)
        {
            _coversRemoved = true;
            if (airflowController != null)
                airflowController.StartAirflow();
        }

        if (cumulative)
        {
            if (progress > _highestProgress)
            {
                _highestProgress = progress;
                if (airflowController != null)
                    airflowController.SetProgress(_highestProgress);
            }
        }
        else
        {
            // Direct set without updating highest (e.g. rolling back for Previous button)
            if (airflowController != null)
                airflowController.SetProgress(progress);
        }
    }

    public float GetProgressForPart(string partName)
    {
        if (!IsActiveEngineJetEngine()) return -2f;

        if (string.IsNullOrEmpty(partName)) return -2f;
        foreach (var m in progressMappings)
        {
            if (string.IsNullOrEmpty(m.partNameContains)) continue;
            if (partName.IndexOf(m.partNameContains, System.StringComparison.OrdinalIgnoreCase) >= 0)
                return m.progress;
        }
        return -2f;
    }

    private bool IsActiveEngineJetEngine()
    {
        EngineSceneLoader loader = FindFirstObjectByType<EngineSceneLoader>();
        if (loader == null) return true;

        EngineData activeData = loader.ActiveEngineData != null ? loader.ActiveEngineData : loader.fallbackEngine;
        if (activeData == null) return true;

        // If the Jet Engine's EngineData is assigned in the Inspector, use direct object reference comparison.
        if (jetEngineData != null)
        {
            return activeData == jetEngineData;
        }

        // Fallback to name-based check if reference is not assigned in the Inspector.
        return activeData.engineName != null && activeData.engineName.IndexOf("Jet Engine", System.StringComparison.OrdinalIgnoreCase) >= 0;
    }
}
