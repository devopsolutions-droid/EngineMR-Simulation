using UnityEngine;
using TMPro;

public class HoverLabel : MonoBehaviour
{
    public TextMeshProUGUI labelText;

    [Header("Connector Line")]
    public LineRenderer connectorLine;
    public float lineWidth = 0.003f;
    public Color lineColor = new Color(1f, 0.65f, 0.3f, 1f);

    private Transform _cameraTransform;
    private Vector3 _hitPoint;

    void Start()
    {
        _cameraTransform = Camera.main?.transform;

        if (connectorLine != null)
        {
            connectorLine.positionCount = 2;
            connectorLine.startWidth = lineWidth;
            connectorLine.endWidth = lineWidth;
            connectorLine.useWorldSpace = true;
            connectorLine.startColor = lineColor;
            connectorLine.endColor = lineColor;
        }

        gameObject.SetActive(false);
    }

    void LateUpdate()
    {
        if (_cameraTransform != null)
            transform.rotation = Quaternion.LookRotation(transform.position - _cameraTransform.position);

        UpdateLine();
    }

    public void Show(string text, Vector3 worldPosition)
    {
        labelText.text = text;
        _hitPoint = worldPosition;

        Vector3 toCamera = (_cameraTransform != null)
            ? (_cameraTransform.position - worldPosition).normalized
            : Vector3.up;
        transform.position = worldPosition + toCamera * 0.15f + Vector3.up * 0.05f;
        gameObject.SetActive(true);
        UpdateLine();
    }

    void UpdateLine()
    {
        if (connectorLine == null) return;
        connectorLine.SetPosition(0, _hitPoint);
        connectorLine.SetPosition(1, transform.position);
    }

    public void Hide() => gameObject.SetActive(false);
}
