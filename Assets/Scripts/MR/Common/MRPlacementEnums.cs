namespace EngineMR.Common
{
    /// <summary>
    /// Represents the current phase of the MR placement workflow.
    /// </summary>
    public enum PlacementState
    {
        Searching,       // Scanning real-world environment for valid surfaces
        Previewing,      // Valid surface hit, showing holographic ghost engine
        Placed           // Engine placed in the world, ready for interaction
    }

    /// <summary>
    /// Classified physical surface type from MRUK / Scene Understanding.
    /// </summary>
    public enum SurfaceType
    {
        Unknown,
        Floor,
        Table,
        Wall,
        Couch,
        Ceiling
    }

    /// <summary>
    /// Interaction modes for manipulating the 3D engine in mixed reality.
    /// </summary>
    public enum ManipulationMode
    {
        None,
        Translating,
        Rotating,
        Scaling
    }
}
