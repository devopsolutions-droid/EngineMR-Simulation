using System.Collections;
using UnityEngine;
using UnityEngine.UI;

public class ScrollNavigator : MonoBehaviour
{
    [Header("References")]
    public ScrollRect scrollRect;
    public Button navLeft;
    public Button navRight;

    [Header("Scroll Settings")]
    public float scrollStepUnits = 60f;
    public float smoothSpeed = 8f;

    private float _targetNormalized;
    private bool _smoothing;
    private bool _ready;

    void Start()
    {
        if (scrollRect == null) { Debug.LogError("[ScrollNavigator] scrollRect NOT assigned!"); return; }
        if (scrollRect.content == null) { Debug.LogError("[ScrollNavigator] No Content on ScrollRect!"); return; }
        if (scrollRect.viewport == null) { Debug.LogError("[ScrollNavigator] No Viewport on ScrollRect!"); return; }
        if (navLeft == null)  Debug.LogWarning("[ScrollNavigator] navLeft not assigned!");
        if (navRight == null) Debug.LogWarning("[ScrollNavigator] navRight not assigned!");

        navLeft?.onClick.AddListener(ScrollLeft);
        navRight?.onClick.AddListener(ScrollRight);
        // Don't init here — Scroll Canvas is hidden on Start.
        // Call InitNow() from HomeSceneUIController after Scroll Canvas is shown.
    }

    /// <summary>Call this from HomeSceneUIController.OnStartButtonClicked after activating Scroll Canvas.</summary>
    public void InitNow()
    {
        if (_ready) return;
        StartCoroutine(InitAfterLayout());
    }

    IEnumerator InitAfterLayout()
    {
        // Force Unity to rebuild the entire layout hierarchy first
        LayoutRebuilder.ForceRebuildLayoutImmediate(scrollRect.GetComponent<RectTransform>());
        LayoutRebuilder.ForceRebuildLayoutImmediate(scrollRect.viewport);
        LayoutRebuilder.ForceRebuildLayoutImmediate(scrollRect.content);

        yield return null;
        yield return null;
        yield return new WaitForEndOfFrame();

        // Log every direct child of Content
        int childCount = scrollRect.content.childCount;
        Debug.Log($"[ScrollNavigator] Content childCount={childCount}  Content.rect={scrollRect.content.rect}  Viewport.rect={scrollRect.viewport.rect}");

        for (int i = 0; i < childCount; i++)
        {
            var child = scrollRect.content.GetChild(i) as RectTransform;
            if (child == null) continue;
            Debug.Log($"[ScrollNavigator]   child[{i}] '{child.name}'  active={child.gameObject.activeInHierarchy}  rect.width={child.rect.width:F1}  sizeDelta={child.sizeDelta}");
        }

        SetContentWidth();

        _targetNormalized = scrollRect.horizontalNormalizedPosition;
        _ready = true;
        UpdateButtonStates();

        Debug.Log($"[ScrollNavigator] READY — contentW={scrollRect.content.rect.width:F1}  viewportW={scrollRect.viewport.rect.width:F1}  scrollable={scrollRect.content.rect.width - scrollRect.viewport.rect.width:F1}");
    }

    void SetContentWidth()
    {
        var hlg = scrollRect.content.GetComponent<HorizontalLayoutGroup>();
        float spacing = hlg != null ? hlg.spacing : 0f;
        float padL    = hlg != null ? hlg.padding.left : 0f;
        float padR    = hlg != null ? hlg.padding.right : 0f;

        float total = padL + padR;
        int count = 0;

        for (int i = 0; i < scrollRect.content.childCount; i++)
        {
            var child = scrollRect.content.GetChild(i) as RectTransform;
            if (child == null || !child.gameObject.activeInHierarchy) continue;
            total += child.sizeDelta.x > 0 ? child.sizeDelta.x : child.rect.width;
            count++;
        }

        if (count > 1) total += spacing * (count - 1);

        Debug.Log($"[ScrollNavigator] SetContentWidth: {count} active children, total={total:F1}");

        if (total <= 0)
        {
            Debug.LogError("[ScrollNavigator] Content width is still 0! Buttons may have sizeDelta.x=0. Check each button's RectTransform SizeDelta X in Inspector.");
            return;
        }

        var sd = scrollRect.content.sizeDelta;
        sd.x = total;
        scrollRect.content.sizeDelta = sd;

        LayoutRebuilder.ForceRebuildLayoutImmediate(scrollRect.content);
    }

    public void ScrollLeft()
    {
        if (!_ready) { Debug.LogWarning("[ScrollNavigator] Not ready yet."); return; }

        float contentW   = scrollRect.content.rect.width;
        float viewportW  = scrollRect.viewport.rect.width;
        float scrollable = contentW - viewportW;

        Debug.Log($"[ScrollNavigator] ScrollLeft — contentW={contentW:F1}  viewportW={viewportW:F1}  scrollable={scrollable:F1}");

        if (scrollable <= 0) { Debug.LogWarning("[ScrollNavigator] BLOCKED — content not wider than viewport."); return; }

        _targetNormalized = Mathf.Clamp01(_targetNormalized - scrollStepUnits / scrollable);
        _smoothing = true;
    }

    public void ScrollRight()
    {
        if (!_ready) { Debug.LogWarning("[ScrollNavigator] Not ready yet."); return; }

        float contentW   = scrollRect.content.rect.width;
        float viewportW  = scrollRect.viewport.rect.width;
        float scrollable = contentW - viewportW;

        Debug.Log($"[ScrollNavigator] ScrollRight — contentW={contentW:F1}  viewportW={viewportW:F1}  scrollable={scrollable:F1}");

        if (scrollable <= 0) { Debug.LogWarning("[ScrollNavigator] BLOCKED — content not wider than viewport."); return; }

        _targetNormalized = Mathf.Clamp01(_targetNormalized + scrollStepUnits / scrollable);
        _smoothing = true;
    }

    void Update()
    {
        if (!_smoothing) return;

        float current = scrollRect.horizontalNormalizedPosition;
        float next    = Mathf.MoveTowards(current, _targetNormalized, smoothSpeed * Time.deltaTime);
        scrollRect.horizontalNormalizedPosition = next;

        if (Mathf.Abs(next - _targetNormalized) < 0.001f)
        {
            scrollRect.horizontalNormalizedPosition = _targetNormalized;
            _smoothing = false;
        }

        UpdateButtonStates();
    }

    /// <summary>
    /// Recalculates the scroll content width and resets the scroll position to the start.
    /// Call this after filtering the elements in the scroll list.
    /// </summary>
    public void RefreshLayoutAndResetScroll()
    {
        SetContentWidth();
        _targetNormalized = 0f;
        scrollRect.horizontalNormalizedPosition = 0f;
        _smoothing = false;
        UpdateButtonStates();
    }

    void UpdateButtonStates()
    {
        if (navLeft  != null) navLeft.interactable  = scrollRect.horizontalNormalizedPosition > 0.001f;
        if (navRight != null) navRight.interactable = scrollRect.horizontalNormalizedPosition < 0.999f;
    }
}
