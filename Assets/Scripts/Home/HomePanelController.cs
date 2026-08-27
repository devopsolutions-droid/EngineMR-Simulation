using UnityEngine;

/// <summary>
/// Positions the home panel World Space Canvas in front of the player on Start,
/// and adds a subtle idle float animation for a holographic feel.
/// Attach to the root Canvas GameObject of the home panel.
/// </summary>
public class HomePanelController : MonoBehaviour
{
    [Header("Placement")]
    [Tooltip("Distance in front of the player camera.")]
    public float distanceFromPlayer = 2.5f;
    [Tooltip("Height offset relative to camera.")]
    public float heightOffset = -0.1f;

    [Header("Float Animation")]
    public bool enableFloat = true;
    [Range(0.01f, 0.1f)] public float floatAmplitude = 0.015f;
    [Range(0.3f, 2f)]    public float floatFrequency = 0.6f;

    private Vector3 _basePosition;
    private bool _placed;

    void Start()
    {
        PlacePanel();
    }

    void PlacePanel()
    {
        var cam = Camera.main;
        if (cam == null) { Debug.LogWarning("[HomePanelController] No main camera found."); return; }

        Vector3 forward = cam.transform.forward;
        forward.y = 0f;
        if (forward.sqrMagnitude < 0.001f) forward = Vector3.forward;
        forward.Normalize();

        Vector3 targetPos = cam.transform.position
                          + forward * distanceFromPlayer
                          + Vector3.up * heightOffset;

        transform.position = targetPos;
        transform.rotation = Quaternion.LookRotation(forward);

        _basePosition = transform.position;
        _placed = true;
    }

    void Update()
    {
        if (!_placed || !enableFloat) return;
        float y = _basePosition.y + Mathf.Sin(Time.time * floatFrequency * Mathf.PI * 2f) * floatAmplitude;
        transform.position = new Vector3(_basePosition.x, y, _basePosition.z);
    }
}
