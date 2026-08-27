/// <summary>
/// Shared interface for all engine-specific Show Working tour/walkthrough controllers.
/// Allows EngineViewManager to trigger them dynamically at runtime.
/// </summary>
public interface IShowWorkingController
{
    /// <summary>Is the tour/walkthrough currently running?</summary>
    bool IsRunning { get; }

    /// <summary>Starts the interactive flow/tour.</summary>
    void StartInteractiveFlow();

    /// <summary>Stops the interactive flow/tour and cleans up visuals/state.</summary>
    void StopInteractiveFlow();

    /// <summary>Called when the Next button is pressed.</summary>
    void OnNextPressed();

    /// <summary>Called when the Previous button is pressed.</summary>
    void OnPreviousPressed();
}
