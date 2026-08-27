using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Simple launcher for the tutorial system.
/// Attach to a button to start the tutorial when clicked.
/// </summary>
public class TutorialLauncher : MonoBehaviour
{
    [SerializeField] private TutorialController tutorialController;
    [SerializeField] private Button launchButton;

    private void Start()
    {
        if (tutorialController == null)
        {
            tutorialController = FindFirstObjectByType<TutorialController>();
        }
        
        if (launchButton == null)
        {
            launchButton = GetComponent<Button>();
        }
        
        if (launchButton != null)
        {
            launchButton.onClick.AddListener(LaunchTutorial);
        }
    }

    private void OnDestroy()
    {
        if (launchButton != null)
        {
            launchButton.onClick.RemoveListener(LaunchTutorial);
        }
    }

    public void LaunchTutorial()
    {
        if (tutorialController != null)
        {
            tutorialController.StartTutorial();
        }
        else
        {
            Debug.LogWarning("TutorialLauncher: No TutorialController found!");
        }
    }

    public void StopTutorial()
    {
        if (tutorialController != null)
        {
            tutorialController.EndTutorial();
        }
    }
}
