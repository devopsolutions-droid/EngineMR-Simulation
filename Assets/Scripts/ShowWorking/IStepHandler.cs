/// <summary>
/// Interface for all Show Working step type handlers.
/// Each step type (GrabRemove, TurbineStart, PartTap, IgniteButton, BladeSpin)
/// has its own handler implementing this interface.
/// </summary>
public interface IStepHandler
{
    /// <summary>Called when the step is entered (from AdvanceToNextStep).</summary>
    void OnStepEnter(ShowWorkingStep step, StepContext ctx);

    /// <summary>Called when the user presses Next on this step.</summary>
    void OnNextPressed(ShowWorkingStep step, StepContext ctx);

    /// <summary>Called when the user presses Previous — must undo the step's side-effects.</summary>
    void OnStepExit(ShowWorkingStep step, StepContext ctx);

    /// <summary>Called when the entire flow is stopped — full cleanup.</summary>
    void Cleanup(ShowWorkingStep step, StepContext ctx);
}