/// <summary>
/// Defines the type of interaction for a ShowWorking step.
/// </summary>
public enum InteractiveStepType
{
    /// <summary>User grabs and pulls the part away (existing cover-removal behavior).</summary>
    GrabRemove,

    /// <summary>User presses Next to auto-start turbine + blades + airflow.</summary>
    TurbineStart,

    /// <summary>User presses Next to auto-play stage VFX (air compression, fuel injection).</summary>
    PartTap,

    /// <summary>User presses Next to auto-play combustion VFX.</summary>
    IgniteButton,

    /// <summary>User presses Next to start rotating a specific blade group + advance airflow.</summary>
    BladeSpin
}