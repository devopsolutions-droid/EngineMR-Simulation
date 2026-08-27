using UnityEngine;
using UnityEngine.XR.Interaction.Toolkit;
using System.Collections;
using System.Collections.Generic;

/// <summary>
/// Fades out and deactivates specified GameObjects when the user grabs the tablet for the first time.
/// Supports both UI CanvasGroups and 3D GameObjects (via Renderers).
/// </summary>
[RequireComponent(typeof(XRGrabInteractable))]
public class TabletFirstGrabHider : MonoBehaviour
{
    [Header("Targets to Hide")]
    [Tooltip("GameObjects that should fade out and disappear on first grab.")]
    public GameObject[] targetsToHide;

    [Header("Fade Settings")]
    [Tooltip("Delay in seconds before the fading starts after grabbing the tablet.")]
    public float fadeDelay = 0f;

    [Tooltip("How long the fade-out transition lasts in seconds.")]
    public float fadeDuration = 1.5f;

    private XRGrabInteractable _grab;
    private bool _hasTriggered = false;
    private int _activeFadesCount = 0;

    void Awake()
    {
        _grab = GetComponent<XRGrabInteractable>();
        if (_grab != null)
        {
            _grab.selectEntered.AddListener(OnGrabbed);
        }
    }

    void OnDestroy()
    {
        if (_grab != null)
        {
            _grab.selectEntered.RemoveListener(OnGrabbed);
        }
    }

    private void OnGrabbed(SelectEnterEventArgs args)
    {
        if (_hasTriggered) return;
        _hasTriggered = true;

        // Start the fade out coroutine for all active targets
        if (targetsToHide != null)
        {
            foreach (var target in targetsToHide)
            {
                if (target != null && target.activeSelf)
                {
                    StartCoroutine(FadeOutAndDeactivate(target));
                }
            }
        }

        // Unsubscribe from grab events
        if (_grab != null)
        {
            _grab.selectEntered.RemoveListener(OnGrabbed);
        }
    }

    private IEnumerator FadeOutAndDeactivate(GameObject target)
    {
        _activeFadesCount++;

        // Wait for the configured delay
        if (fadeDelay > 0f)
        {
            yield return new WaitForSeconds(fadeDelay);
        }

        if (target == null)
        {
            _activeFadesCount--;
            yield break;
        }

        // Gather all components to fade
        CanvasGroup canvasGroup = target.GetComponent<CanvasGroup>();
        if (canvasGroup == null)
        {
            canvasGroup = target.GetComponentInChildren<CanvasGroup>();
        }

        Renderer[] renderers = target.GetComponentsInChildren<Renderer>(true);
        
        // Cache original colors using MaterialPropertyBlock to avoid material leak/instantiation
        MaterialPropertyBlock propBlock = new MaterialPropertyBlock();
        Dictionary<Renderer, Color[]> originalColors = new Dictionary<Renderer, Color[]>();

        foreach (var r in renderers)
        {
            int matCount = r.sharedMaterials.Length;
            Color[] colors = new Color[matCount];
            for (int i = 0; i < matCount; i++)
            {
                Material mat = r.sharedMaterials[i];
                if (mat != null && mat.HasProperty("_Color"))
                {
                    colors[i] = mat.color;
                }
                else
                {
                    colors[i] = Color.white; // fallback
                }
            }
            originalColors[r] = colors;
        }

        float elapsed = 0f;
        while (elapsed < fadeDuration)
        {
            if (target == null) yield break; // Safety check in case the object is destroyed externally

            elapsed += Time.deltaTime;
            float t = elapsed / fadeDuration;
            float alpha = Mathf.Lerp(1f, 0f, t);

            // 1. Fade UI CanvasGroup if present
            if (canvasGroup != null)
            {
                canvasGroup.alpha = alpha;
            }

            // 2. Fade Renderers via MaterialPropertyBlock
            foreach (var r in renderers)
            {
                if (r == null || !originalColors.TryGetValue(r, out Color[] colors)) continue;

                r.GetPropertyBlock(propBlock);
                
                if (colors.Length > 0)
                {
                    Color baseColor = colors[0];
                    propBlock.SetColor("_Color", new Color(baseColor.r, baseColor.g, baseColor.b, baseColor.a * alpha));
                }
                
                r.SetPropertyBlock(propBlock);
            }

            yield return null;
        }

        if (target != null)
        {
            // Ensure fully transparent
            if (canvasGroup != null)
            {
                canvasGroup.alpha = 0f;
            }

            // Deactivate the GameObject completely
            target.SetActive(false);
        }

        _activeFadesCount--;

        // Once all fades are complete, self-destroy the script component
        if (_activeFadesCount <= 0)
        {
            Destroy(this);
        }
    }
}
