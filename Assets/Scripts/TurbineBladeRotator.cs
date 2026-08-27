using System.Collections;
using UnityEngine;

/// <summary>
/// TurbineBladeRotator
/// ─────────────────────
/// Rotates one or more blade GameObjects around a configurable axis
/// at a configurable speed. Supports smooth acceleration via AnimationCurve.
///
/// Usage:
///   - Attach to any GameObject (e.g. a manager or the blade parent)
///   - Drag individual blade GameObjects into the "Blades" array in the Inspector
///   - Call StartRotation() to begin spinning (e.g. from ShowWorkingInteractiveController
///     when the user presses "Start Turbine")
///   - Call StopRotation() to stop
///
/// If the Blades array is empty, falls back to rotating this.transform
/// for backwards compatibility.
/// </summary>
public class TurbineBladeRotator : MonoBehaviour
{
    [Header("Blade Targets")]
    [Tooltip("Drag individual blade GameObjects here. Each one will rotate independently.\n"
        + "Leave empty to rotate this.transform (legacy behaviour).")]
    public GameObject[] blades = new GameObject[0];

    [Header("Rotation Settings")]
    [Tooltip("Axis of rotation in local space (default: Z = fan blade spin axis).")]
    public Vector3 rotationAxis = Vector3.forward;

    [Tooltip("Target rotation speed in degrees per second.")]
    public float rotationSpeed = 45f;

    [Header("Acceleration")]
    [Tooltip("Animation curve for smooth acceleration over time. X=normalized time(0..1), Y=normalized speed(0..1).")]
    public AnimationCurve accelerationCurve = AnimationCurve.EaseInOut(0f, 0f, 1f, 1f);

    [Tooltip("How many seconds it takes to reach full speed.")]
    public float accelerationDuration = 2f;

    [Tooltip("Optional: play this audio clip once when rotation starts.")]
    public AudioClip startAudio;

    public bool IsRotating { get; private set; } = false;

    // ── Private fields ─────────────────────────────────────────────────────────
    private float _currentSpeed = 0f;
    private Coroutine _accelCoroutine;
    private AudioSource _audioSource;

    // Cached Transforms of blades (resolved once at start)
    private Transform[] _bladeTransforms;

    // Original local rotations stored on Awake so we can hard-reset on stop
    private Quaternion[] _originalRotations;

    // ── Unity Lifecycle ──────────────────────────────────────────────────────

    void Awake()
    {
        _audioSource = GetComponent<AudioSource>();
        if (_audioSource == null)
            _audioSource = gameObject.AddComponent<AudioSource>();
        _audioSource.playOnAwake = false;

        // Cache Transforms from the blades array
        if (blades != null && blades.Length > 0)
        {
            _bladeTransforms = new Transform[blades.Length];
            _originalRotations = new Quaternion[blades.Length];
            for (int i = 0; i < blades.Length; i++)
            {
                if (blades[i] != null)
                {
                    _bladeTransforms[i] = blades[i].transform;
                    _originalRotations[i] = blades[i].transform.localRotation;
                }
            }
        }
    }

    void Update()
    {
        if (!IsRotating) return;
        float delta = _currentSpeed * Time.deltaTime;

        if (_bladeTransforms != null && _bladeTransforms.Length > 0)
        {
            // Rotate each individual blade Transform
            for (int i = 0; i < _bladeTransforms.Length; i++)
            {
                if (_bladeTransforms[i] != null)
                    _bladeTransforms[i].Rotate(rotationAxis, delta, Space.Self);
            }
        }
        else
        {
            // Fallback: rotate this.transform (legacy behaviour)
            transform.Rotate(rotationAxis, delta, Space.Self);
        }
    }

    // ── Public API ───────────────────────────────────────────────────────────

    /// <summary>Start spinning the blades with smooth acceleration.</summary>
    public void StartRotation()
    {
        if (IsRotating) return;

        IsRotating = true;

        // Play start audio once
        if (_audioSource != null && startAudio != null)
        {
            _audioSource.clip = startAudio;
            _audioSource.loop = false;
            _audioSource.Play();
        }

        // Begin acceleration
        if (_accelCoroutine != null) StopCoroutine(_accelCoroutine);
        _accelCoroutine = StartCoroutine(Accelerate());
    }

    /// <summary>Stop spinning immediately (no deceleration).</summary>
    public void StopRotation()
    {
        if (!IsRotating) return;

        IsRotating = false;
        _currentSpeed = 0f;

        if (_accelCoroutine != null)
        {
            StopCoroutine(_accelCoroutine);
            _accelCoroutine = null;
        }

        if (_audioSource != null && _audioSource.isPlaying)
            _audioSource.Stop();
    }

    /// <summary>
    /// Hard‑reset every blade back to its original local rotation.
    /// Call this when leaving Show Working so blades snap back to
    /// the position they had when the scene first loaded.
    /// </summary>
    public void ResetRotation()
    {
        // Ensure rotation is fully stopped first
        if (IsRotating) StopRotation();

        if (_bladeTransforms != null && _originalRotations != null)
        {
            for (int i = 0; i < _bladeTransforms.Length; i++)
            {
                if (_bladeTransforms[i] != null && i < _originalRotations.Length)
                    _bladeTransforms[i].localRotation = _originalRotations[i];
            }
        }
        else
        {
            // Fallback: reset this.transform if no blades array was configured
            transform.localRotation = Quaternion.identity;
        }
    }

    // ── Coroutine ───────────────────────────────────────────────────────────

    private IEnumerator Accelerate()
    {
        float elapsed = 0f;
        while (elapsed < accelerationDuration)
        {
            elapsed += Time.deltaTime;
            float t = Mathf.Clamp01(elapsed / accelerationDuration);
            _currentSpeed = rotationSpeed * accelerationCurve.Evaluate(t);
            yield return null;
        }
        _currentSpeed = rotationSpeed;
        _accelCoroutine = null;
    }
}