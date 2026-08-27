using UnityEngine;
using UnityEngine.Video;

/// <summary>
/// Plays a video as a fullscreen VR background.
/// Replaces SkyboxSetup — uses the same dedicated background camera approach
/// so the video stays fixed regardless of head movement.
///
/// Setup:
///   1. Add this component to any active GameObject in the scene
///   2. Assign a VideoClip (or set a URL) in the Inspector
///   3. Remove or disable the old SkyboxSetup component
/// </summary>
public class VideoBackground : MonoBehaviour
{
    [Header("Video Source — assign ONE")]
    [Tooltip("Drag a video clip asset here (mp4, webm, etc.)")]
    public VideoClip videoClip;

    [Tooltip("Or use a URL/path instead of a clip asset. Leave empty if using VideoClip.")]
    public string videoUrl = "";

    [Header("Playback")]
    public bool loop = true;
    public bool muteAudio = true;
    [Range(0f, 1f)]
    public float volume = 0.5f;

    [Header("Render Texture")]
    [Tooltip("Resolution of the render texture. Match your video resolution for best quality.")]
    public int renderTextureWidth  = 1920;
    public int renderTextureHeight = 1080;

    private RenderTexture _renderTexture;
    private VideoPlayer   _videoPlayer;

    void Start()
    {
        if (videoClip == null && string.IsNullOrEmpty(videoUrl))
        {
            Debug.LogError("[VideoBackground] No VideoClip or URL assigned!");
            return;
        }

        Camera mainCam = Camera.main;
        if (mainCam == null)
        {
            Debug.LogError("[VideoBackground] No Main Camera found!");
            return;
        }

        // ── Step 1: Main camera renders on top, clears only depth ────────────
        mainCam.clearFlags = CameraClearFlags.Depth;
        mainCam.depth = 1;

        // ── Step 2: Create RenderTexture the video will render into ──────────
        _renderTexture = new RenderTexture(renderTextureWidth, renderTextureHeight, 0);
        _renderTexture.name = "[VideoBackgroundRT]";

        // ── Step 3: Set up VideoPlayer ────────────────────────────────────────
        GameObject vpGO = new GameObject("[VideoPlayer]");
        _videoPlayer = vpGO.AddComponent<VideoPlayer>();

        if (videoClip != null)
        {
            _videoPlayer.source = VideoSource.VideoClip;
            _videoPlayer.clip   = videoClip;
        }
        else
        {
            _videoPlayer.source = VideoSource.Url;
            _videoPlayer.url    = videoUrl;
        }

        _videoPlayer.renderMode          = VideoRenderMode.RenderTexture;
        _videoPlayer.targetTexture       = _renderTexture;
        _videoPlayer.isLooping           = loop;
        _videoPlayer.playOnAwake         = false;
        _videoPlayer.waitForFirstFrame   = true;
        _videoPlayer.skipOnDrop          = true;

        // Audio
        _videoPlayer.audioOutputMode = muteAudio
            ? VideoAudioOutputMode.None
            : VideoAudioOutputMode.Direct;

        if (!muteAudio)
            _videoPlayer.SetDirectAudioVolume(0, volume);

        // Start preparing — play once ready
        _videoPlayer.prepareCompleted += OnVideoPrepared;
        _videoPlayer.Prepare();

        // ── Step 4: Background camera — fixed, never moves ───────────────────
        GameObject bgCamGO = new GameObject("[BackgroundCamera]");
        Camera bgCam = bgCamGO.AddComponent<Camera>();
        bgCam.clearFlags      = CameraClearFlags.SolidColor;
        bgCam.backgroundColor = Color.black;
        bgCam.cullingMask     = 1 << 31; // layer 31 only
        bgCam.depth           = 0;
        bgCam.nearClipPlane   = 0.1f;
        bgCam.farClipPlane    = 10f;
        bgCam.fieldOfView     = 60f;
        bgCam.stereoTargetEye = StereoTargetEyeMask.Both;

        bgCamGO.transform.position = Vector3.zero;
        bgCamGO.transform.rotation = Quaternion.identity;

        // ── Step 5: Fullscreen quad on layer 31 ──────────────────────────────
        GameObject quad = GameObject.CreatePrimitive(PrimitiveType.Quad);
        quad.name  = "[BackgroundQuad]";
        quad.layer = 31;
        Destroy(quad.GetComponent<MeshCollider>());

        float dist = 5f;
        float h    = 2f * dist * Mathf.Tan(bgCam.fieldOfView * 0.5f * Mathf.Deg2Rad);
        float w    = h * ((float)renderTextureWidth / renderTextureHeight);

        quad.transform.position   = new Vector3(0f, 0f, dist);
        quad.transform.rotation   = Quaternion.identity;
        quad.transform.localScale = new Vector3(w, h, 1f);

        // Unlit material using the RenderTexture
        Material mat = new Material(Shader.Find("Unlit/Texture"));
        mat.mainTexture = _renderTexture;

        Renderer rend = quad.GetComponent<Renderer>();
        rend.material             = mat;
        rend.shadowCastingMode    = UnityEngine.Rendering.ShadowCastingMode.Off;
        rend.receiveShadows       = false;

        Debug.Log("[VideoBackground] Setup complete — waiting for video to prepare.");
    }

    void OnVideoPrepared(VideoPlayer vp)
    {
        vp.Play();
        Debug.Log("[VideoBackground] Video playing.");
    }

    void OnDestroy()
    {
        if (_videoPlayer != null)
            _videoPlayer.prepareCompleted -= OnVideoPrepared;

        if (_renderTexture != null)
        {
            _renderTexture.Release();
            Destroy(_renderTexture);
        }
    }
}
