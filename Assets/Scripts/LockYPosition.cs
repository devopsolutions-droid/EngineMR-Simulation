using UnityEngine;

public class LockYPosition : MonoBehaviour
{
    public float lockedY = -0.24f;

    void LateUpdate()
    {
        var pos = transform.position;
        pos.y = lockedY;
        transform.position = pos;
    }
}
