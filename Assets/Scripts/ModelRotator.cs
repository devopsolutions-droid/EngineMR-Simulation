using UnityEngine;

public class ModelRotator : MonoBehaviour
{
    public float rotationSpeed = 90f; // degrees per second
    public KeyCode toggleKey1 = KeyCode.F6;
    public KeyCode toggleKey2 = KeyCode.R;
    private bool rotating = false;

    private Vector3 anchorPosition;

    void Start()
    {
        anchorPosition = transform.position;
    }

    void Update()
    {
        if (Input.GetKeyDown(toggleKey1) || Input.GetKeyDown(toggleKey2))
        {
            rotating = !rotating;
        }
        if (rotating)
        {
            transform.Rotate(Vector3.up, rotationSpeed * Time.deltaTime, Space.World);
            // Re-anchor the model to its original position (X,Z)
            transform.position = new Vector3(anchorPosition.x, transform.position.y, anchorPosition.z);
        }
    }
}