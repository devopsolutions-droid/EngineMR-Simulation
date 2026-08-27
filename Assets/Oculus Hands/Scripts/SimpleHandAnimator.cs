using UnityEngine;
using UnityEngine.InputSystem;

/// <summary>
/// Simple hand animator that works with EngineGrabManager
/// Animates hand based on grab input
/// </summary>
public class SimpleHandAnimator : MonoBehaviour
{
    public Animator handAnimator;
    public InputActionReference grabAction;
    
    private float currentGripValue = 0f;
    private float gripLerpSpeed = 5f;
    private EngineGrabManager grabManager;

    void Start()
    {
        if (handAnimator == null)
            handAnimator = GetComponent<Animator>();
            
        grabManager = FindObjectOfType<EngineGrabManager>();
        
        // If no grab action assigned, try to get it from EngineGrabManager
        if (grabAction == null && grabManager != null)
        {
            grabAction = grabManager.grabAction;
        }
        
        Debug.Log($"[SimpleHandAnimator] Animator: {handAnimator}, GrabAction: {grabAction}, GrabManager: {grabManager}");
    }

    void Update()
    {
        if (handAnimator == null)
        {
            Debug.LogWarning("[SimpleHandAnimator] Animator is null!");
            return;
        }

        float grabInputValue = 0f;
        
        // Try to read from grab action
        if (grabAction != null && grabAction.action != null)
        {
            grabInputValue = grabAction.action.ReadValue<float>();
        }
        else
        {
            Debug.LogWarning("[SimpleHandAnimator] Grab action is null! Assign it in the Inspector.");
        }
        
        // Smoothly lerp grip value
        currentGripValue = Mathf.Lerp(currentGripValue, grabInputValue, Time.deltaTime * gripLerpSpeed);
        
        // Set animator parameters
        handAnimator.SetFloat("Grip", currentGripValue);
        handAnimator.SetFloat("Trigger", currentGripValue);
    }
}
