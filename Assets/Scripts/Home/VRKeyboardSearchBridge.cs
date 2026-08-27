using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using VisualKeyboard;

public class VRKeyboardSearchBridge : MonoBehaviour
{
    public enum WaveOriginType
    {
        Center,
        LeftToRight,
        RightToLeft,
        BottomToTop,
        TopToBottom
    }

    [Header("Components")]
    [Tooltip("Reference to the VisualKeyboard component.")]
    public VisualKeyboard.VisualKeyboard keyboard;

    [Tooltip("Reference to the VRSearchInput component.")]
    public VRSearchInput searchInput;

    [Header("Visibility Settings")]
    [Tooltip("If true, the keyboard GameObject will automatically activate/deactivate when the Search Input is focused/unfocused.")]
    public bool autoToggleVisibility = true;

    [Tooltip("The panel or root object representing the keyboard visual body to toggle.")]
    public GameObject keyboardPanel;

    [Header("Background Animation Settings")]
    [Tooltip("Duration of the background panel transition animation in seconds.")]
    public float animDuration = 0.25f;

    [Tooltip("Local position offset of the keyboard when it is hidden.")]
    public Vector3 hiddenPosOffset = new Vector3(0f, -0.6f, 0f);

    [Tooltip("Local rotation of the keyboard when it is hidden.")]
    public Vector3 hiddenRotation = new Vector3(15f, 0f, 0f);

    [Tooltip("Scale of the keyboard panel when it is hidden.")]
    public Vector3 hiddenScale = new Vector3(0.1f, 0.1f, 0.1f);

    [Tooltip("Animate position?")]
    public bool animatePosition = true;

    [Tooltip("Animate rotation?")]
    public bool animateRotation = true;

    [Tooltip("Animate scale?")]
    public bool animateScale = true;

    [Header("Holographic Wave Settings")]
    [Tooltip("The starting point of the holographic wave.")]
    public WaveOriginType waveOrigin = WaveOriginType.Center;

    [Tooltip("Delay scale factor between keys (higher value = slower wave propagation).")]
    public float waveDelayFactor = 0.25f;

    [Tooltip("Duration of each key's individual scale-up/scale-down animation.")]
    public float keyAnimDuration = 0.2f;

    [Tooltip("Futuristic glow color flashed as keys materialize.")]
    public Color waveGlowColor = new Color(0f, 0.8f, 1f, 0.2f);

    private Vector3 _finalLocalPos;
    private Quaternion _finalLocalRot;
    private Vector3 _targetScale;
    private bool _lastFocusedState;
    private Coroutine _animCoroutine;
    private bool _hasStoredDefaults = false;

    private Dictionary<VisualKeyForKeyboard, Vector3> _defaultKeyScales = new Dictionary<VisualKeyForKeyboard, Vector3>();
    private Dictionary<VisualKeyForKeyboard, Coroutine> _keyCoroutines = new Dictionary<VisualKeyForKeyboard, Coroutine>();

    private Vector3 _keysCenter;
    private float _maxDistance;
    private float _minX, _maxX, _minY, _maxY;

    private void Start()
    {
        if (keyboard == null)
        {
            // Include inactive since the keyboard might start inactive
            keyboard = FindFirstObjectByType<VisualKeyboard.VisualKeyboard>(FindObjectsInactive.Include);
        }

        if (searchInput == null)
        {
            searchInput = GetComponent<VRSearchInput>();
            if (searchInput == null)
            {
                searchInput = FindFirstObjectByType<VRSearchInput>();
            }
        }

        if (keyboardPanel == null && keyboard != null)
        {
            keyboardPanel = keyboard.gameObject;
        }

        if (keyboardPanel != null)
        {
            // Store the default design-time transform values of the keyboard panel
            _finalLocalPos = keyboardPanel.transform.localPosition;
            _finalLocalRot = keyboardPanel.transform.localRotation;
            _targetScale = keyboardPanel.transform.localScale;

            // Fallback for scale if saved as zero
            if (_targetScale == Vector3.zero)
            {
                _targetScale = new Vector3(0.007f, 0.007f, 0.007f);
            }
            _hasStoredDefaults = true;
        }

        // Cache default scales and positions of keys for wave calculation
        if (keyboard != null && keyboard.keys != null && keyboard.keys.Count > 0 && keyboardPanel != null)
        {
            _defaultKeyScales.Clear();
            int validCount = 0;
            _minX = float.MaxValue;
            _maxX = float.MinValue;
            _minY = float.MaxValue;
            _maxY = float.MinValue;

            foreach (var key in keyboard.keys)
            {
                if (key == null) continue;

                _defaultKeyScales[key] = key.transform.localScale;

                // Use position relative to keyboardPanel to normalize different row parent spaces
                Vector3 panelLocalPos = keyboardPanel.transform.InverseTransformPoint(key.transform.position);
                validCount++;

                if (panelLocalPos.x < _minX) _minX = panelLocalPos.x;
                if (panelLocalPos.x > _maxX) _maxX = panelLocalPos.x;
                if (panelLocalPos.y < _minY) _minY = panelLocalPos.y;
                if (panelLocalPos.y > _maxY) _maxY = panelLocalPos.y;
            }

            if (validCount > 0)
            {
                // Bounding box center guarantees the exact geometric middle of the keyboard deck
                _keysCenter = new Vector3((_minX + _maxX) * 0.5f, (_minY + _maxY) * 0.5f, 0f);

                _maxDistance = 0f;
                foreach (var key in keyboard.keys)
                {
                    if (key == null) continue;
                    Vector3 panelLocalPos = keyboardPanel.transform.InverseTransformPoint(key.transform.position);
                    float dist = Vector3.Distance(panelLocalPos, _keysCenter);
                    if (dist > _maxDistance) _maxDistance = dist;
                }
            }
        }

        if (keyboard != null)
        {
            keyboard.OnKeyClick += HandleKeyClick;
        }

        // Set initial visibility immediately on Start to prevent a one-frame flash when the parent canvas is enabled
        if (autoToggleVisibility && searchInput != null && keyboardPanel != null && _hasStoredDefaults)
        {
            _lastFocusedState = searchInput.IsFocused;

            if (_lastFocusedState)
            {
                keyboardPanel.SetActive(true);
                if (animateScale) keyboardPanel.transform.localScale = _targetScale;
                if (animatePosition) keyboardPanel.transform.localPosition = _finalLocalPos;
                if (animateRotation) keyboardPanel.transform.localRotation = _finalLocalRot;
                SetAllKeysScaleToDefault();
            }
            else
            {
                keyboardPanel.SetActive(false);
                if (animateScale) keyboardPanel.transform.localScale = hiddenScale;
                if (animatePosition) keyboardPanel.transform.localPosition = _finalLocalPos + hiddenPosOffset;
                if (animateRotation) keyboardPanel.transform.localRotation = Quaternion.Euler(hiddenRotation);
                SetAllKeysScale(Vector3.zero);
            }

            // Sync text label at startup
            if (keyboard != null)
            {
                keyboard.Text = searchInput.CurrentQuery;
            }
        }
    }

    private void OnDestroy()
    {
        if (keyboard != null)
        {
            keyboard.OnKeyClick -= HandleKeyClick;
        }
    }

    private void Update()
    {
        if (autoToggleVisibility && searchInput != null && keyboardPanel != null && _hasStoredDefaults)
        {
            bool isFocused = searchInput.IsFocused;
            if (_lastFocusedState != isFocused)
            {
                _lastFocusedState = isFocused;
                Debug.Log($"[VRKeyboardSearchBridge] Focus state changed to: {isFocused}. Starting transition animation.");
                ToggleKeyboardAnimated(isFocused);

                // Sync text label when keyboard panel is animated / toggled
                if (keyboard != null && isFocused)
                {
                    keyboard.Text = searchInput.CurrentQuery;
                }
            }
        }
    }

    private void ToggleKeyboardAnimated(bool show)
    {
        if (_animCoroutine != null) StopCoroutine(_animCoroutine);
        _animCoroutine = StartCoroutine(AnimateKeyboard(show));
    }

    private void SetAllKeysScale(Vector3 scale)
    {
        if (keyboard != null && keyboard.keys != null)
        {
            foreach (var key in keyboard.keys)
            {
                if (key != null)
                {
                    key.transform.localScale = scale;
                }
            }
        }
    }

    private void SetAllKeysScaleToDefault()
    {
        if (keyboard != null && keyboard.keys != null)
        {
            foreach (var key in keyboard.keys)
            {
                if (key != null)
                {
                    key.transform.localScale = GetDefaultKeyScale(key);
                }
            }
        }
    }

    private Vector3 GetDefaultKeyScale(VisualKeyForKeyboard key)
    {
        if (key != null && _defaultKeyScales.TryGetValue(key, out Vector3 scale))
        {
            return scale;
        }
        return Vector3.one;
    }

    private float GetKeyDelay(VisualKeyForKeyboard key, bool show)
    {
        if (key == null) return 0f;

        Vector3 pos = keyboardPanel.transform.InverseTransformPoint(key.transform.position);
        float factor = 0f;

        switch (waveOrigin)
        {
            case WaveOriginType.Center:
                if (_maxDistance > 0.001f)
                {
                    factor = Vector3.Distance(pos, _keysCenter) / _maxDistance;
                }
                break;
            case WaveOriginType.LeftToRight:
                if (Mathf.Abs(_maxX - _minX) > 0.001f)
                {
                    factor = (pos.x - _minX) / (_maxX - _minX);
                }
                break;
            case WaveOriginType.RightToLeft:
                if (Mathf.Abs(_maxX - _minX) > 0.001f)
                {
                    factor = (_maxX - pos.x) / (_maxX - _minX);
                }
                break;
            case WaveOriginType.BottomToTop:
                if (Mathf.Abs(_maxY - _minY) > 0.001f)
                {
                    factor = (pos.y - _minY) / (_maxY - _minY);
                }
                break;
            case WaveOriginType.TopToBottom:
                if (Mathf.Abs(_maxY - _minY) > 0.001f)
                {
                    factor = (_maxY - pos.y) / (_maxY - _minY);
                }
                break;
        }

        // Reverse the wave direction on hide for an imploding effect
        if (!show)
        {
            factor = 1f - factor;
        }

        return factor * waveDelayFactor;
    }

    private void StopAllKeyAnimations()
    {
        foreach (var kvp in _keyCoroutines)
        {
            if (kvp.Value != null)
            {
                StopCoroutine(kvp.Value);
            }
        }
        _keyCoroutines.Clear();
    }

    private IEnumerator AnimateSingleKey(VisualKeyForKeyboard key, float delay, bool show)
    {
        // Wait for the staggered delay
        yield return new WaitForSeconds(delay);

        Vector3 startScale = show ? Vector3.zero : GetDefaultKeyScale(key);
        Vector3 endScale = show ? GetDefaultKeyScale(key) : Vector3.zero;

        float elapsed = 0f;

        // Pulse the glow as they materialize
        if (show)
        {
            key.HighlightAnimation(waveGlowColor, keyAnimDuration * 2f);
        }

        while (elapsed < keyAnimDuration)
        {
            elapsed += Time.deltaTime;
            float t = elapsed / keyAnimDuration;
            float ease = show ? EaseOutBack(t) : EaseInCubic(t);

            key.transform.localScale = Vector3.Lerp(startScale, endScale, ease);
            yield return null;
        }

        key.transform.localScale = endScale;
        _keyCoroutines.Remove(key);
    }

    private IEnumerator AnimateKeyboard(bool show)
    {
        StopAllKeyAnimations();

        float elapsed = 0f;

        // Ensure we have a CanvasGroup component on the keyboard panel for fading
        CanvasGroup canvasGroup = keyboardPanel.GetComponent<CanvasGroup>();
        if (canvasGroup == null)
        {
            canvasGroup = keyboardPanel.AddComponent<CanvasGroup>();
        }

        Vector3 startScale = show ? hiddenScale : _targetScale;
        Vector3 endScale = show ? _targetScale : hiddenScale;

        Vector3 startPos = show ? (_finalLocalPos + hiddenPosOffset) : _finalLocalPos;
        Vector3 endPos = show ? _finalLocalPos : (_finalLocalPos + hiddenPosOffset);

        Quaternion startRot = show ? Quaternion.Euler(hiddenRotation) : _finalLocalRot;
        Quaternion endRot = show ? _finalLocalRot : Quaternion.Euler(hiddenRotation);

        if (show)
        {
            // Reset alpha and set keys to 0 before starting
            canvasGroup.alpha = 1f;
            SetAllKeysScale(Vector3.zero);
            keyboardPanel.SetActive(true);

            // Animate keys in with holographic wave
            if (keyboard != null && keyboard.keys != null)
            {
                foreach (var key in keyboard.keys)
                {
                    if (key == null) continue;
                    float delay = GetKeyDelay(key, true);
                    _keyCoroutines[key] = StartCoroutine(AnimateSingleKey(key, delay, true));
                }
            }

            // Animate the main background panel positioning/scaling
            while (elapsed < animDuration)
            {
                elapsed += Time.deltaTime;
                float t = elapsed / animDuration;
                float ease = EaseOutCubic(t);

                if (animateScale) keyboardPanel.transform.localScale = Vector3.Lerp(startScale, endScale, ease);
                if (animatePosition) keyboardPanel.transform.localPosition = Vector3.Lerp(startPos, endPos, ease);
                if (animateRotation) keyboardPanel.transform.localRotation = Quaternion.Lerp(startRot, endRot, ease);

                yield return null;
            }

            if (animateScale) keyboardPanel.transform.localScale = endScale;
            if (animatePosition) keyboardPanel.transform.localPosition = endPos;
            if (animateRotation) keyboardPanel.transform.localRotation = endRot;
        }
        else
        {
            // Close animation: Subtle fade out
            // Keep background transform at final (open) state and keys at default scales
            if (animateScale) keyboardPanel.transform.localScale = _targetScale;
            if (animatePosition) keyboardPanel.transform.localPosition = _finalLocalPos;
            if (animateRotation) keyboardPanel.transform.localRotation = _finalLocalRot;
            SetAllKeysScaleToDefault();

            // Fade the CanvasGroup alpha from 1 to 0
            while (elapsed < animDuration)
            {
                elapsed += Time.deltaTime;
                float t = elapsed / animDuration;
                // Ease out cubic fade
                canvasGroup.alpha = Mathf.Lerp(1f, 0f, EaseOutCubic(t));
                yield return null;
            }

            canvasGroup.alpha = 0f;
            keyboardPanel.SetActive(false);

            // Reset alpha back to 1 for the next show cycle
            canvasGroup.alpha = 1f;
        }
    }

    private float EaseOutBack(float x)
    {
        const float c1 = 1.70158f;
        const float c3 = c1 + 1f;
        return 1f + c3 * Mathf.Pow(x - 1f, 3f) + c1 * Mathf.Pow(x - 1f, 2f);
    }

    private float EaseInCubic(float x)
    {
        return x * x * x;
    }

    private float EaseOutCubic(float x)
    {
        return 1f - Mathf.Pow(1f - x, 3f);
    }

    private void HandleKeyClick(VisualKeyForKeyboard key)
    {
        if (searchInput == null) return;
        Debug.Log($"[VRKeyboardSearchBridge] HandleKeyClick: key={key.gameObject.name}, character={key.character}, oldKeyCode={key.oldKeyCode}");

        if (key.oldKeyCode == KeyCode.Backspace)
        {
            searchInput.DeleteCharacter();
        }
        else if (key.character != '\0')
        {
            // Get character based on shift hold status on the keyboard
            char charEntered = keyboard.isShiftHold ? key.shiftedCharacter : key.character;
            searchInput.InputCharacter(charEntered);
        }
        else if (key.oldKeyCode == KeyCode.Escape || key.oldKeyCode == KeyCode.Return)
        {
            searchInput.UnfocusInput();
        }

        // Sync text label immediately after key input modification
        if (keyboard != null)
        {
            keyboard.Text = searchInput.CurrentQuery;
        }
    }

    /// <summary>
    /// Public method to hide the keyboard by unfocusing the search input.
    /// Can be wired to Unity UI Button onClick() events in the Inspector.
    /// </summary>
    public void HideKeyboard()
    {
        if (searchInput != null)
        {
            searchInput.UnfocusInput();
        }
    }
}
