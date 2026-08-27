using System.Collections;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// Handles smooth fade transitions between scenes.
///
/// IMPORTANT: Place this in the EngineButtons HomeScene (the first scene that loads).
/// DontDestroyOnLoad keeps it alive across scene loads so the fade coroutine
/// completes even after the originating scene is destroyed.
///
/// The fade quad is also marked DontDestroyOnLoad so it doesn't get
/// destroyed along with the old scene mid-fade.
/// </summary>
public class SceneTransitionManager : MonoBehaviour
{
    public static SceneTransitionManager Instance { get; private set; }

    [Header("Fade Settings")]
    [Range(0.1f, 2f)] public float fadeDuration = 0.5f;
    public Color fadeColor = Color.black;

    private GameObject _fadeQuad;
    private Material   _fadeMat;
    private bool       _isFading;

    void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Debug.Log("[SceneTransitionManager] Duplicate — destroying.");
            Destroy(gameObject);
            return;
        }

        Instance = this;
        DontDestroyOnLoad(gameObject);
        BuildFadeQuad();

        Debug.Log($"[SceneTransitionManager] Initialized in '{gameObject.scene.name}'. Persisting across loads.");
    }

    void BuildFadeQuad()
    {
        _fadeQuad = GameObject.CreatePrimitive(PrimitiveType.Quad);
        _fadeQuad.name = "[FadeQuad]";
        Destroy(_fadeQuad.GetComponent<MeshCollider>());

        // Quad must also persist — otherwise LoadSceneMode.Single destroys it
        // mid-fade when the old scene is unloaded
        DontDestroyOnLoad(_fadeQuad);

        _fadeMat = new Material(Shader.Find("Unlit/Color"))
        {
            color = new Color(fadeColor.r, fadeColor.g, fadeColor.b, 0f)
        };
        _fadeMat.SetFloat("_Mode", 2);
        _fadeMat.SetInt("_SrcBlend", (int)UnityEngine.Rendering.BlendMode.SrcAlpha);
        _fadeMat.SetInt("_DstBlend", (int)UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha);
        _fadeMat.SetInt("_ZWrite", 0);
        _fadeMat.EnableKeyword("_ALPHABLEND_ON");
        _fadeMat.renderQueue = 5000;

        _fadeQuad.GetComponent<Renderer>().material = _fadeMat;
        _fadeQuad.SetActive(false);
    }

    /// <summary>Load a scene by name with a fade transition.</summary>
    public void LoadScene(string sceneName)
    {
        if (_isFading)
        {
            Debug.LogWarning("[SceneTransitionManager] Already fading — ignoring call.");
            return;
        }
        StartCoroutine(FadeAndLoad(sceneName));
    }

    IEnumerator FadeAndLoad(string sceneName)
    {
        _isFading = true;
        Debug.Log($"[SceneTransitionManager] START: '{SceneManager.GetActiveScene().name}' → '{sceneName}'  frame={Time.frameCount}");

        AttachQuadToCamera();
        _fadeQuad.SetActive(true);

        // Fade to black
        yield return Fade(0f, 1f);
        Debug.Log("[SceneTransitionManager] Faded out. Loading...");

        var op = SceneManager.LoadSceneAsync(sceneName, LoadSceneMode.Single);
        op.allowSceneActivation = false;
        while (op.progress < 0.9f) yield return null;

        Debug.Log("[SceneTransitionManager] Scene ready. Activating...");
        op.allowSceneActivation = true;

        // Frame +1: Awake() fires on new scene objects
        yield return null;
        Debug.Log($"[SceneTransitionManager] Frame+1 — scene='{SceneManager.GetActiveScene().name}'  isLoaded={SceneManager.GetActiveScene().isLoaded}");

        // Frame +2: Start() + sceneLoaded event fire, LightingRestorer.OnSceneLoaded runs
        yield return null;
        Debug.Log($"[SceneTransitionManager] Frame+2 — isLoaded={SceneManager.GetActiveScene().isLoaded}");

        // Frame +3: LightingRestorer's RestoreAfterFrame coroutine has completed
        yield return null;
        Debug.Log("[SceneTransitionManager] Frame+3 — lighting restore complete");

        // Re-attach quad to new scene's camera before fading in
        AttachQuadToCamera();

        // Fade back in
        yield return Fade(1f, 0f);

        _fadeQuad.SetActive(false);
        _isFading = false;
        Debug.Log($"[SceneTransitionManager] COMPLETE  frame={Time.frameCount}");
    }

    void AttachQuadToCamera()
    {
        // Camera.main may not work in XR — fall back to FindFirstObjectByType
        var cam = Camera.main ?? FindFirstObjectByType<Camera>();

        if (cam != null)
        {
            _fadeQuad.transform.SetParent(cam.transform, false);
            _fadeQuad.transform.localPosition = new Vector3(0f, 0f, cam.nearClipPlane + 0.01f);
            _fadeQuad.transform.localRotation = Quaternion.identity;
            _fadeQuad.transform.localScale    = new Vector3(0.1f, 0.1f, 1f);
            Debug.Log($"[SceneTransitionManager] Quad attached to '{cam.name}'");
        }
        else
        {
            Debug.LogWarning("[SceneTransitionManager] No camera found — quad not attached!");
        }
    }

    IEnumerator Fade(float from, float to)
    {
        float elapsed = 0f;
        var c = _fadeMat.color;
        while (elapsed < fadeDuration)
        {
            elapsed += Time.deltaTime;
            c.a = Mathf.Lerp(from, to, elapsed / fadeDuration);
            _fadeMat.color = c;
            yield return null;
        }
        c.a = to;
        _fadeMat.color = c;
    }
}
