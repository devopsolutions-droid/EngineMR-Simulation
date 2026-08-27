using System;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

namespace EngineMR.UI
{
    /// <summary>
    /// Step 4: Preview Model Panel.
    /// Shows a 3D preview/turntable of the selected engine, details, cycle buttons (< >), Cancel, and Select.
    /// </summary>
    public class MRModelPreviewPanel : MonoBehaviour
    {
        [Header("UI Text & Details")]
        [SerializeField] private TextMeshProUGUI engineTitleText;
        [SerializeField] private TextMeshProUGUI engineCategoryText;
        [SerializeField] private TextMeshProUGUI engineDescriptionText;

        [Header("Action Buttons")]
        [SerializeField] private Button prevEngineButton;
        [SerializeField] private Button nextEngineButton;
        [SerializeField] private Button cancelButton;
        [SerializeField] private Button selectButton;

        [Header("3D Turntable Preview Container")]
        [SerializeField] private Transform previewSpawnPoint;
        [SerializeField] private float turntableRotationSpeed = 25f;
        [SerializeField] private float previewScaleMultiplier = 0.25f;

        private EngineRegistry _registry;
        private int _currentIndex = 0;
        private GameObject _spawnedPreviewInstance;

        public event Action OnCancelClicked;
        public event Action<EngineData> OnSelectConfirmed;

        private void Awake()
        {
            if (cancelButton != null)
                cancelButton.onClick.AddListener(() => OnCancelClicked?.Invoke());

            if (selectButton != null)
                selectButton.onClick.AddListener(OnSelectClicked);

            if (prevEngineButton != null)
                prevEngineButton.onClick.AddListener(ShowPreviousEngine);

            if (nextEngineButton != null)
                nextEngineButton.onClick.AddListener(ShowNextEngine);
        }

        private void Update()
        {
            if (previewSpawnPoint != null)
            {
                previewSpawnPoint.Rotate(Vector3.up, turntableRotationSpeed * Time.deltaTime, Space.World);
            }
        }

        public void BindRegistry(EngineRegistry registry)
        {
            _registry = registry;
        }

        public void ShowPreview(EngineData engineData, int index)
        {
            _currentIndex = index;
            UpdatePreviewDisplay(engineData);
        }

        private void ShowPreviousEngine()
        {
            if (_registry == null || _registry.Count == 0) return;
            _currentIndex--;
            if (_currentIndex < 0) _currentIndex = _registry.Count - 1;
            UpdatePreviewDisplay(_registry.Get(_currentIndex));
        }

        private void ShowNextEngine()
        {
            if (_registry == null || _registry.Count == 0) return;
            _currentIndex++;
            if (_currentIndex >= _registry.Count) _currentIndex = 0;
            UpdatePreviewDisplay(_registry.Get(_currentIndex));
        }

        private void UpdatePreviewDisplay(EngineData data)
        {
            if (data == null) return;

            if (engineTitleText != null)
                engineTitleText.text = data.engineName;

            if (engineCategoryText != null)
                engineCategoryText.text = string.IsNullOrEmpty(data.engineCategory) ? "Mechanical" : data.engineCategory;

            if (engineDescriptionText != null)
                engineDescriptionText.text = data.engineDescription;

            // Spawn preview 3D model if assigned
            Spawn3DPreviewModel(data);
        }

        private void Spawn3DPreviewModel(EngineData data)
        {
            if (_spawnedPreviewInstance != null)
            {
                Destroy(_spawnedPreviewInstance);
            }

            if (previewSpawnPoint != null && data.enginePrefab != null)
            {
                _spawnedPreviewInstance = Instantiate(data.enginePrefab, previewSpawnPoint);
                _spawnedPreviewInstance.transform.localPosition = Vector3.zero;
                _spawnedPreviewInstance.transform.localRotation = Quaternion.identity;
                _spawnedPreviewInstance.transform.localScale = Vector3.one * previewScaleMultiplier;

                // Disable colliders and interactions on preview model to avoid physics/raycast interference
                var colliders = _spawnedPreviewInstance.GetComponentsInChildren<Collider>();
                foreach (var col in colliders)
                {
                    col.enabled = false;
                }
            }
        }

        private void OnSelectClicked()
        {
            if (_registry != null && _currentIndex >= 0 && _currentIndex < _registry.Count)
            {
                var selectedData = _registry.Get(_currentIndex);
                OnSelectConfirmed?.Invoke(selectedData);
            }
        }

        private void OnDisable()
        {
            if (_spawnedPreviewInstance != null)
            {
                Destroy(_spawnedPreviewInstance);
            }
        }
    }
}
