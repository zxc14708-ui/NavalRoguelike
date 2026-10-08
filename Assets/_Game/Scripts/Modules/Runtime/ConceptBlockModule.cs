namespace Game.Modules.Runtime
{
    /// <summary>
    /// Starting ship art prototype block. It occupies a normal grid cell and can
    /// take module damage, but intentionally contributes no gameplay bonuses.
    /// Fleet relay, turbo intake, fire control and missile logistics need their
    /// own behaviors before entering the live draft pool.
    /// </summary>
    public sealed class ConceptBlockModule : Game.Modules.ModuleRuntime { }
}
