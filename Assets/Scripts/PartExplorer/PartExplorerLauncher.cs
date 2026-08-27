using System;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Simple launcher for the part explorer.
/// Attach to a button to start the explorer when clicked.
/// </summary>
[Obsolete("PartExplorerLauncher is orphaned (zero cross-references). Use TabletUIController.OnShowWorkingClicked() instead.")]
public class PartExplorerLauncher : MonoBehaviour
{
    [SerializeField] private PartExplorerController explorerController;
    [SerializeField] private Button launchButton;

    private void Start()
    {
        if (explorerController == null)
        {
            explorerController = FindFirstObjectByType<PartExplorerController>();
        }
        
        if (launchButton == null)
        {
            launchButton = GetComponent<Button>();
        }
        
        if (launchButton != null)
        {
            launchButton.onClick.AddListener(LaunchExplorer);
        }
    }

    private void OnDestroy()
    {
        if (launchButton != null)
        {
            launchButton.onClick.RemoveListener(LaunchExplorer);
        }
    }

    public void LaunchExplorer()
    {
        if (explorerController != null)
        {
            explorerController.StartExplorer();
        }
        else
        {
            Debug.LogWarning("PartExplorerLauncher: No PartExplorerController found!");
        }
    }

    public void StopExplorer()
    {
        if (explorerController != null)
        {
            explorerController.EndExplorer();
        }
    }
}
