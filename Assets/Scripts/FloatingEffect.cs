using UnityEngine;

/// <summary>
/// Attach this script to any GameObject (or a child visual container of a hover panel)
/// to make it bob up and down smoothly with a floating/bouncy effect.
/// 
/// Oscillates on the Y-axis from its initial Y coordinate up to Y + floatRange, and loops.
/// </summary>
public class FloatingEffect : MonoBehaviour
{
    [Header("Floating Settings")]
    [Tooltip("Maximum height of the bobbing motion relative to the starting Y position.")]
    public float floatRange = 0.7f;

    [Tooltip("Speed/frequency of the oscillation.")]
    public float floatSpeed = 1.5f;

    [Tooltip("If true, bobbing is applied in local space. If false, applied in world space.")]
    public bool useLocalSpace = true;

    private Vector3 _startPosition;
    private float _randomOffset;

    void Start()
    {
        // Record the initial starting position
        _startPosition = useLocalSpace ? transform.localPosition : transform.position;
        
        // Add a slight random time offset so multiple floating objects don't bob in perfect unison
        _randomOffset = Random.Range(0f, 100f);
    }

    void Update()
    {
        // Smoothly oscillate between 0 and floatRange using a cosine wave:
        // cos goes from 1 to -1. (1 - cos(x)) / 2 goes from 0 to 1.
        float timeValue = (Time.time + _randomOffset) * floatSpeed;
        float offset = floatRange * (1f - Mathf.Cos(timeValue)) / 2f;

        if (useLocalSpace)
        {
            Vector3 targetLocalPos = _startPosition;
            targetLocalPos.y += offset;
            transform.localPosition = targetLocalPos;
        }
        else
        {
            Vector3 targetWorldPos = _startPosition;
            targetWorldPos.y += offset;
            transform.position = targetWorldPos;
        }
    }
}
