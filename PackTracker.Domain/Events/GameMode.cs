namespace PackTracker.Domain.Events;

/// <summary>
/// Represents the current Star Citizen game mode.
/// </summary>
public enum GameMode
{
    /// <summary>Game mode not yet determined</summary>
    Unknown,

    /// <summary>Persistent Universe (main MMO mode)</summary>
    PersistentUniverse,

    /// <summary>Arena Commander (dogfighting mode)</summary>
    ArenaCommander,

    /// <summary>Star Marine (FPS mode)</summary>
    StarMarine
}
