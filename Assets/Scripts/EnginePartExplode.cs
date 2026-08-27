using UnityEngine;

public enum ExplodePosition { Auto, LeftTop, Left, Center, Right, RightTop }

/// <summary>
/// Handles explode target calculation and smooth position animation for an engine part.
/// Requires EnginePart on the same GameObject.
/// </summary>
[RequireComponent(typeof(EnginePart))]
public class EnginePartExplode : MonoBehaviour
{
    [Header("Exploded View")]
    public ExplodePosition explodePosition = ExplodePosition.Auto;
    [Range(0f, 6f)] public float explodeDistance = 6f;
    [Tooltip("Optional override direction. Leave at zero to auto-calculate from engine center.")]
    public Vector3 explodeDirectionOverride = Vector3.zero;

    public Vector3 AssembledLocalPos { get; private set; }
    public Vector3 ExplodedLocalPos  => _explodedLocalPos;
    
    private Vector3   _explodedLocalPos;
    private Coroutine _explodeCoroutine;
    private Coroutine _liftCoroutine;
    private Coroutine _worldAnimateCoroutine;
    private bool      _initialised;

    void Awake() => Initialise();

    void OnDestroy()
    {
        if (_explodeCoroutine != null) StopCoroutine(_explodeCoroutine);
        if (_liftCoroutine != null)    StopCoroutine(_liftCoroutine);
        if (_worldAnimateCoroutine != null) StopCoroutine(_worldAnimateCoroutine);
    }

    void Initialise()
    {
        if (_initialised) return;
        AssembledLocalPos = transform.localPosition;
        _initialised = true;
    }

    // ── Target calculation ────────────────────────────────────────────────────

    public void ComputeExplodeTarget(Vector3 engineWorldCenter)
    {
        if (!_initialised) Initialise();

        if (explodePosition == ExplodePosition.Center)
        {
            _explodedLocalPos = AssembledLocalPos;
            return;
        }

        Vector3 dir;
        if (explodeDirectionOverride.sqrMagnitude > 0.001f)
        {
            dir = explodeDirectionOverride.normalized;
        }
        else if (explodePosition != ExplodePosition.Auto)
        {
            switch (explodePosition)
            {
                case ExplodePosition.LeftTop:  dir = new Vector3(-0.5f, 0.5f, 0).normalized; break;
                case ExplodePosition.Left:     dir = Vector3.left;  break;
                case ExplodePosition.Right:    dir = Vector3.right; break;
                case ExplodePosition.RightTop: dir = new Vector3(0.5f, 0.5f, 0).normalized;  break;
                default:                       dir = (transform.position - engineWorldCenter).normalized; break;
            }
        }
        else
        {
            dir = (transform.position - engineWorldCenter).normalized;
            if (dir.sqrMagnitude < 0.001f) dir = Vector3.up;
        }

        dir.y = Mathf.Abs(dir.y);

        Vector3 worldOffset = dir * explodeDistance;
        Vector3 localOffset = transform.parent != null
            ? transform.parent.InverseTransformDirection(worldOffset)
            : worldOffset;

        _explodedLocalPos = AssembledLocalPos + localOffset;
    }

    public void SetExplodeWorldTarget(Vector3 worldPosition)
    {
        if (!_initialised) Initialise();
        _explodedLocalPos = transform.parent != null
            ? transform.parent.InverseTransformPoint(worldPosition)
            : worldPosition;
    }

    public void SetExplodeLocalTarget(Vector3 localPosition)
    {
        if (!_initialised) Initialise();
        _explodedLocalPos = localPosition;
    }

    // ── Animation ─────────────────────────────────────────────────────────────

    public void AnimateToExploded(float duration)
    {
        if (_explodeCoroutine != null) StopCoroutine(_explodeCoroutine);
        if (_worldAnimateCoroutine != null) StopCoroutine(_worldAnimateCoroutine);
        if (!gameObject.activeInHierarchy || duration <= 0.01f)
        { transform.localPosition = _explodedLocalPos; return; }
        _explodeCoroutine = StartCoroutine(AnimateTo(_explodedLocalPos, duration));
    }

    public void AnimateToAssembled(float duration)
    {
        if (_explodeCoroutine != null) StopCoroutine(_explodeCoroutine);
        if (_worldAnimateCoroutine != null) StopCoroutine(_worldAnimateCoroutine);
        if (!gameObject.activeInHierarchy || duration <= 0.01f)
        { transform.localPosition = AssembledLocalPos; return; }
        _explodeCoroutine = StartCoroutine(AnimateTo(AssembledLocalPos, duration));
    }

    public void AnimateToCustomPos(Vector3 localPos, float duration)
    {
        if (_explodeCoroutine != null) StopCoroutine(_explodeCoroutine);
        if (_worldAnimateCoroutine != null) StopCoroutine(_worldAnimateCoroutine);
        if (!gameObject.activeInHierarchy || duration <= 0.01f)
        { transform.localPosition = localPos; return; }
        _explodeCoroutine = StartCoroutine(AnimateTo(localPos, duration));
    }

    public void AnimateToWorldPos(Vector3 worldPos, float duration)
    {
        if (_explodeCoroutine != null) StopCoroutine(_explodeCoroutine);
        if (_worldAnimateCoroutine != null) StopCoroutine(_worldAnimateCoroutine);
        if (!gameObject.activeInHierarchy || duration <= 0.01f)
        { transform.position = worldPos; return; }
        _worldAnimateCoroutine = StartCoroutine(AnimateToWorld(worldPos, duration));
    }

    System.Collections.IEnumerator AnimateToWorld(Vector3 targetWorld, float duration)
    {
        Vector3 start = transform.position;
        float elapsed = 0f;

        while (elapsed < duration)
        {
            elapsed += Time.deltaTime;
            transform.position = Vector3.Lerp(start, targetWorld, Mathf.SmoothStep(0f, 1f, elapsed / duration));
            yield return null;
        }

        transform.position = targetWorld;
        _worldAnimateCoroutine = null;
    }

    System.Collections.IEnumerator AnimateTo(Vector3 target, float duration)
    {
        Vector3 start   = transform.localPosition;
        float   elapsed = 0f;

        while (elapsed < duration)
        {
            elapsed += Time.deltaTime;
            transform.localPosition = Vector3.Lerp(start, target, Mathf.SmoothStep(0f, 1f, elapsed / duration));
            yield return null;
        }

        transform.localPosition = target;
        _explodeCoroutine = null;
    }

    // ── Lift / Lower (used by SimplePartExplorer Show Working mode) ───────────

    public void LiftUp(float amount, float duration)
    {
        if (!_initialised) Initialise();
        if (_liftCoroutine != null)    StopCoroutine(_liftCoroutine);
        if (_explodeCoroutine != null) { StopCoroutine(_explodeCoroutine); _explodeCoroutine = null; }

        Vector3 target = AssembledLocalPos + new Vector3(0f, amount, 0f);
        if (!gameObject.activeInHierarchy || duration <= 0.01f)
        { transform.localPosition = target; return; }
        _liftCoroutine = StartCoroutine(AnimateLift(target, duration));
    }

    public void LowerDown(float duration)
    {
        if (!_initialised) Initialise();
        if (_liftCoroutine != null)    StopCoroutine(_liftCoroutine);
        if (_explodeCoroutine != null) { StopCoroutine(_explodeCoroutine); _explodeCoroutine = null; }

        if (!gameObject.activeInHierarchy || duration <= 0.01f)
        { transform.localPosition = AssembledLocalPos; return; }
        _liftCoroutine = StartCoroutine(AnimateLift(AssembledLocalPos, duration));
    }

    System.Collections.IEnumerator AnimateLift(Vector3 target, float duration)
    {
        Vector3 start = transform.localPosition;
        float elapsed = 0f;

        while (elapsed < duration)
        {
            elapsed += Time.deltaTime;
            transform.localPosition = Vector3.Lerp(start, target, Mathf.SmoothStep(0f, 1f, elapsed / duration));
            yield return null;
        }

        transform.localPosition = target;
        _liftCoroutine = null;
    }

    /// <summary>
    /// Enables manual explode mode where parts are not animated but can be individually grabbed.
    /// Call this to let users move engine parts manually one after another.
    /// </summary>
    public void EnableManualExplodeMode()
    {
        // Ensure the part stays at its assembled position (no automatic offset)
        _explodedLocalPos = AssembledLocalPos;
        // Optionally, stop any running explode animation
        if (_explodeCoroutine != null) { StopCoroutine(_explodeCoroutine); _explodeCoroutine = null; }
        // The EngineGrabManager already allows grabbing; ensure it permits manual grabs
        var grabManager = FindObjectOfType<EngineGrabManager>();
        if (grabManager != null) grabManager.allowGrabbing = true;
    }


}
