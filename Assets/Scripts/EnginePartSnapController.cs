// This script checks proximity and handles magnetic snapping feedback for engine assembly.
using UnityEngine;
using System.Collections;

/// <summary>
/// Snap-to-Assembly Controller for engine parts in Grab Mode.
///
/// When the user drags a part close enough to its original assembled position,
/// this component triggers a smooth magnetic snap animation, locks the part
/// in place, and provides visual/audio feedback.
///
/// Behaviour:
///   - Reads the target local position from EnginePartExplode.AssembledLocalPos
///   - Every frame while the part is held: checks proximity to the snap target
///   - Within snapDistance (~0.3m): auto-snaps the part into place
///   - Snapped parts cannot be grabbed again until ResetSnap() is called
///   - Fires OnPartSnapped event so EngineGrabManager can auto-release
/// </summary>
[RequireComponent(typeof(EnginePart), typeof(EnginePartExplode))]
public class EnginePartSnapController : MonoBehaviour
{
    [Header("Snap Settings")]
    [Tooltip("World-space distance at which the magnetic pull STARTS (guides the part toward target).")]
    [Range(0.05f, 1.0f)]
    public float magnetRange = 0.4f;

    [Tooltip("Strength of the magnetic pull (0=none, 1=snap instantly to target within magnet range). " +
             "Increased from 0.18 to 0.6 to overcome grab lerp inertia (followSpeed=0.35).")]
    [Range(0f, 1f)]
    public float magnetStrength = 0.6f;

    [Tooltip("World-space distance at which the part finally snaps into place. " +
             "Increased from 0.2m to 0.3m for more forgiving snap triggering.")]
    [Range(0.01f, 0.5f)]
    public float snapDistance = 0.3f;

    [Tooltip("Duration of the snap position tween in seconds.")]
    [Range(0.05f, 0.5f)]
    public float snapDuration = 0.2f;

    [Tooltip("How much the part overshoots and settles for a satisfying magnetic feel (local Y).")]
    [Range(0f, 0.1f)]
    public float snapBounceHeight = 0.02f;

    [Header("Audio")]
    [Tooltip("AudioSource to play the snap sound on. If empty, attempts to find one on this GameObject.")]
    public AudioSource audioSource;
    [Tooltip("Sound played when the part snaps into place.")]
    public AudioClip snapSound;

    [Header("Visual Feedback")]
    [Tooltip("Optional particle system to burst on snap. If assigned, it will be Play()'d.")]
    public ParticleSystem snapParticles;

    [Header("Snap Ghost (Visual Outline)")]
    [Tooltip("Optional SnapGhost component (child of this GameObject) that shows a " +
             "semi-transparent outline of the part at the snap target position.")]
    public SnapGhost snapGhost;

    // ── Events ────────────────────────────────────────────────────────────────
    /// <summary>Fired when the part successfully snaps into place.</summary>
    public event System.Action<EnginePartSnapController> OnPartSnapped;

    // ── Public state ──────────────────────────────────────────────────────────
    /// <summary>Whether this part is currently snapped into its assembled position.</summary>
    public bool IsSnapped { get; private set; } = false;

    /// <summary>The world-space target position where this part snaps to.</summary>
    public Vector3 SnapTargetWorld { get; private set; }

    // ── Internal ──────────────────────────────────────────────────────────────
    private EnginePartExplode _explode;
    private Coroutine _snapCoroutine;
    private Collider[] _colliders;

    // ── Lifecycle ─────────────────────────────────────────────────────────────

    void Awake()
    {
        _explode = GetComponent<EnginePartExplode>();
        if (audioSource == null)
            audioSource = GetComponent<AudioSource>();

        // Auto-discover SnapGhost on this GameObject or children if not assigned
        if (snapGhost == null)
            snapGhost = GetComponentInChildren<SnapGhost>(true);

        _colliders = GetComponentsInChildren<Collider>();
    }

    void Start()
    {
        // Compute the world-space snap target from the assembled local position
        RefreshSnapTarget();
    }

    void OnDestroy()
    {
        if (_snapCoroutine != null)
            StopCoroutine(_snapCoroutine);
    }

    // ── Public API ────────────────────────────────────────────────────────────

    /// <summary>
    /// Recompute the world-space snap target from the current parent transform.
    /// Call this if the parent has moved since Start() (e.g., engine re-centering).
    /// </summary>
    public void RefreshSnapTarget()
    {
        Transform parent = transform.parent;
        SnapTargetWorld = parent != null
            ? parent.TransformPoint(_explode.AssembledLocalPos)
            : _explode.AssembledLocalPos;
    }

    /// <summary>
    /// If the part is within <see cref="magnetRange"/> (but not yet snapped),
    /// returns a gentle pull vector toward the snap target scaled by <see cref="magnetStrength"/>.
    /// Returns Vector3.zero if outside range, already snapped, or already snapping.
    ///
    /// Call this from EngineGrabManager.MoveGrabbedPart() every frame to create a
    /// magnetic guidance feel — the part gets gradually pulled toward the target
    /// as the user brings it close.
    /// </summary>
    public Vector3 GetMagneticPull(Vector3 currentPosition)
    {
        if (IsSnapped)                      return Vector3.zero;
        if (_snapCoroutine != null)         return Vector3.zero;

        Vector3 toTarget = SnapTargetWorld - currentPosition;
        float   dist     = toTarget.magnitude;

        // Outside magnetic range → no pull
        if (dist > magnetRange) return Vector3.zero;

        // Inside snap distance → don't pull, TrySnap() will handle it
        if (dist <= snapDistance) return Vector3.zero;

        // Compute pull strength: strongest at snapDistance edge, tapers to zero at magnetRange
        // This gives a smooth magnetic gradient, not a sudden jerk
        float t = 1f - Mathf.Clamp01((dist - snapDistance) / (magnetRange - snapDistance));
        float pullStrength = t * magnetStrength;

        return toTarget.normalized * pullStrength;
    }

    /// <summary>
    /// Checks the distance from the part's current position to SnapTargetWorld.
    /// If within snapDistance, triggers the snap animation.
    /// Returns true if a snap was triggered this call; false otherwise.
    /// </summary>
    public bool TrySnap()
    {
        if (IsSnapped) return false;
        if (_snapCoroutine != null) return true; // already snapping

        float dist = Vector3.Distance(transform.position, SnapTargetWorld);
        if (dist <= snapDistance)
        {
            BeginSnap();
            return true;
        }
        return false;
    }

    /// <summary>
    /// Forces the part to snap, bypassing the distance check. 
    /// Useful for grouped parts when the primary part triggers a snap.
    /// </summary>
    public void ForceSnap()
    {
        if (IsSnapped || _snapCoroutine != null) return;
        BeginSnap();
    }

    /// <summary>
    /// Immediately snap to the target position without animation or checks.
    /// Useful when resetting or exiting a mode. Disables the MeshCollider
    /// so the part doesn't interfere with other raycasts.
    /// </summary>
    public void SnapImmediate()
    {
        if (_snapCoroutine != null)
        {
            StopCoroutine(_snapCoroutine);
            _snapCoroutine = null;
        }
        transform.position = SnapTargetWorld;
        IsSnapped = true;

        // Disable colliders — snapped parts shouldn't block raycasts
        if (_colliders != null)
            foreach (var c in _colliders) 
                if (c != null) c.enabled = false;
    }

    /// <summary>
    /// Reset the snap state so the part can be grabbed and snapped again.
    /// Does not move the part — just unlocks it. Re-enables the MeshCollider.
    /// </summary>
    public void ResetSnap()
    {
        if (_snapCoroutine != null)
        {
            StopCoroutine(_snapCoroutine);
            _snapCoroutine = null;
        }
        IsSnapped = false;

        // Re-enable colliders so the part can be raycasted and grabbed again
        if (_colliders != null)
            foreach (var c in _colliders) 
                if (c != null) c.enabled = true;
    }

    // ── SnapGhost forward methods ─────────────────────────────────────────────
    // These delegate to the SnapGhost component so EngineGrabManager can control
    // the ghost without needing a direct reference.

    /// <summary>Create the ghost mesh copies at the snap target position.</summary>
    public void CreateSnapGhost()
    {
        if (snapGhost == null) return;
        snapGhost.CreateGhost(transform, SnapTargetWorld, transform.rotation);
    }

    /// <summary>Show the ghost with idle pulsing animation.</summary>
    public void ShowSnapGhost()
    {
        if (snapGhost == null) return;
        snapGhost.Show();
    }

    /// <summary>Update the ghost's position (call every frame while grabbed).</summary>
    public void UpdateSnapGhost()
    {
        if (snapGhost == null) return;
        snapGhost.UpdateTarget(SnapTargetWorld, transform.rotation);
    }

    /// <summary>Brighten the ghost outline as the part approaches (0=far, 1=at snap distance).</summary>
    public void SetGhostProximity(float t)
    {
        if (snapGhost == null) return;
        snapGhost.SetProximity(t);
    }

    /// <summary>Destroy the ghost meshes.</summary>
    public void ClearSnapGhost()
    {
        if (snapGhost == null) return;
        snapGhost.ClearGhost();
    }

    /// <summary>Flash the ghost on snap.</summary>
    public void FlashSnapGhost()
    {
        if (snapGhost == null) return;
        snapGhost.FlashSnap();
    }

    // ── Snap Execution ────────────────────────────────────────────────────────

    private void BeginSnap()
    {
        IsSnapped = true;

        // Play snap sound
        if (audioSource != null && snapSound != null)
            audioSource.PlayOneShot(snapSound);

        // Burst particles
        if (snapParticles != null)
            snapParticles.Play();

        // Flash the snap ghost (bright flash-and-fade)
        FlashSnapGhost();

        // ── Green success flash ───────────────────────────────────────────────
        GetComponent<EnginePartVisuals>()?.FlashSnapSuccess();

        // Animate to target position
        if (gameObject.activeInHierarchy && snapDuration > 0.01f)
            _snapCoroutine = StartCoroutine(AnimateSnap());
        else
            transform.position = SnapTargetWorld;

        if (_colliders != null)
            foreach (var c in _colliders) 
                if (c != null) c.enabled = false;

        OnPartSnapped?.Invoke(this);
    }


    private IEnumerator AnimateSnap()
    {
        Vector3 start   = transform.position;
        float   elapsed = 0f;

        while (elapsed < snapDuration)
        {
            elapsed += Time.deltaTime;
            transform.position = Vector3.Lerp(start, SnapTargetWorld, Mathf.SmoothStep(0f, 1f, elapsed / snapDuration));
            yield return null;
        }

        transform.position = SnapTargetWorld;
        _snapCoroutine = null;
    }

    // ── Gizmo (Editor visualisation) ──────────────────────────────────────────

    void OnDrawGizmosSelected()
    {
        if (!Application.isPlaying)
        {
            // In-editor: estimate target from current local position
            Transform parent = transform.parent;
            Vector3 target = parent != null
                ? parent.TransformPoint(transform.localPosition)
                : transform.position;
            DrawSnapGizmo(target);
        }
        else
        {
            DrawSnapGizmo(SnapTargetWorld);
        }
    }

    private void DrawSnapGizmo(Vector3 target)
    {
        // ── Magnet range (cyan) ─────────────────────────────────────────────
        Gizmos.color = new Color(0f, 1f, 1f, 0.08f); // very faint cyan
        Gizmos.DrawSphere(target, magnetRange);

        Gizmos.color = new Color(0f, 1f, 1f, 0.4f); // semi-transparent cyan wire
        Gizmos.DrawWireSphere(target, magnetRange);

        // ── Snap range (green) ──────────────────────────────────────────────
        Gizmos.color = Color.green;
        Gizmos.DrawWireSphere(target, snapDistance);

        Gizmos.color = new Color(0f, 1f, 0f, 0.15f);
        Gizmos.DrawSphere(target, snapDistance);

        // ── Connection line ─────────────────────────────────────────────────
        if (Application.isPlaying)
        {
            Gizmos.color = new Color(0f, 1f, 0f, 0.3f);
            Gizmos.DrawLine(transform.position, target);
        }
    }
}