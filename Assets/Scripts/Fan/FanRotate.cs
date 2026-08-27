using UnityEngine;

public class FanRotate : MonoBehaviour
{
    public float rotationSpeed = 100f;

    void Update()
    {
        Vector3 currentRotation = transform.eulerAngles;
        currentRotation.y += rotationSpeed * Time.deltaTime;
        transform.eulerAngles = currentRotation;
    }
}