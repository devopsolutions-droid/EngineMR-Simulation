using UnityEngine;

public class FaceEngineOnStart : MonoBehaviour
{
    public Transform engineTarget;

    void Start()
    {
        if (engineTarget == null) return;
        Vector3 dir = engineTarget.position - transform.position;
        dir.y = 0f;
        if (dir.sqrMagnitude > 0.001f)
            transform.rotation = Quaternion.LookRotation(dir);
    }
}
