using System;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

namespace EngineMR.UI
{
    /// <summary>
    /// Attached to each engine card in the MR "Choose a Model" grid.
    /// Handles thumbnail display, model title, hover state, and selection.
    /// </summary>
    public class MREngineCardUI : MonoBehaviour
    {
        [Header("UI References")]
        [SerializeField] private Image thumbnailImage;
        [SerializeField] private TextMeshProUGUI titleText;
        [SerializeField] private TextMeshProUGUI categoryText;
        [SerializeField] private Button selectButton;
        [SerializeField] private GameObject highlightBorder;
        [SerializeField] private CanvasGroup canvasGroup;

        private EngineData _engineData;
        private Action<EngineData> _onSelectCallback;

        public EngineData Data => _engineData;

        public void BindData(EngineData data, Action<EngineData> onSelectCallback)
        {
            _engineData = data;
            _onSelectCallback = onSelectCallback;

            if (titleText != null)
                titleText.text = data != null ? data.engineName : "Unknown";

            if (categoryText != null && data != null)
                categoryText.text = string.IsNullOrEmpty(data.engineCategory) ? "General" : data.engineCategory;

            if (thumbnailImage != null && data != null)
            {
                thumbnailImage.sprite = data.thumbnail;
                thumbnailImage.enabled = data.thumbnail != null;
            }

            if (highlightBorder != null)
                highlightBorder.SetActive(false);

            if (selectButton != null)
            {
                selectButton.onClick.RemoveAllListeners();
                selectButton.onClick.AddListener(OnCardClicked);
            }
        }

        public void SetSelectedHighlight(bool isSelected)
        {
            if (highlightBorder != null)
                highlightBorder.SetActive(isSelected);
        }

        private void OnCardClicked()
        {
            if (_engineData != null)
            {
                _onSelectCallback?.Invoke(_engineData);
            }
        }

        public void OnHoverEnter()
        {
            if (highlightBorder != null) highlightBorder.SetActive(true);
            if (canvasGroup != null) canvasGroup.alpha = 1f;
        }

        public void OnHoverExit()
        {
            if (highlightBorder != null) highlightBorder.SetActive(false);
            if (canvasGroup != null) canvasGroup.alpha = 0.95f;
        }
    }
}
