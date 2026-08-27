using UnityEngine;

/// <summary>
/// Robust VR background image.
/// Creates a dedicated background camera that renders the image fullscreen.
/// The image stays completely static regardless of head movement.
/// </summary>
public class SkyboxSetup : MonoBehaviour
{
    public Texture2D backgroundTexture;

    void Start()
    {
        if (backgroundTexture == null)
        {
            Debug.LogError("[SkyboxSetup] backgroundTexture is not assigned!");
            return;
        }

        // Step 1 — Main camera renders on top, clears only depth (not color)
        Camera mainCam = Camera.main;
        if (mainCam == null)
        {
            Debug.LogError("[SkyboxSetup] No Main Camera found!");
            return;
        }
        mainCam.clearFlags = CameraClearFlags.Depth;
        mainCam.depth = 1;

        // Step 2 — Create a separate background camera, NOT parented to XR rig
        GameObject bgCamGO = new GameObject("[BackgroundCamera]");
        Camera bgCam = bgCamGO.AddComponent<Camera>();
        bgCam.clearFlags = CameraClearFlags.SolidColor;
        bgCam.backgroundColor = Color.black;
        bgCam.cullingMask = 1 << 31; // only render layer 31 (our bg quad)
        bgCam.depth = 0;             // renders first, behind everything
        bgCam.nearClipPlane = 0.1f;
        bgCam.farClipPlane = 10f;
        bgCam.fieldOfView = 60f;
        bgCam.stereoTargetEye = StereoTargetEyeMask.Both;

        // Fix position — never moves
        bgCamGO.transform.position = Vector3.zero;
        bgCamGO.transform.rotation = Quaternion.identity;

        // Step 3 — Create the quad on layer 31, in front of bg camera
        GameObject quad = GameObject.CreatePrimitive(PrimitiveType.Quad);
        quad.name = "[BackgroundQuad]";
        quad.layer = 31;
        Destroy(quad.GetComponent<MeshCollider>());

        quad.transform.position = new Vector3(0f, 0f, 5f);
        quad.transform.rotation = Quaternion.identity;

        // Scale to fill the bg camera's view at Z=5
        float dist = 5f;
        float h = 2f * dist * Mathf.Tan(bgCam.fieldOfView * 0.5f * Mathf.Deg2Rad);
        float w = h * (16f / 9f);
        quad.transform.localScale = new Vector3(w, h, 1f);

        // Step 4 — Unlit material
        Material mat = new Material(Shader.Find("Unlit/Texture"));
        mat.mainTexture = backgroundTexture;

        Renderer rend = quad.GetComponent<Renderer>();
        rend.material = mat;
        rend.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        rend.receiveShadows = false;
    }
}
