using PackTracker.Domain.Enums;

namespace PackTracker.Domain.Events;

/// <summary>
/// Immutable event data representing an actor death in Star Citizen.
/// Fired when one player kills another (FPS, vehicle, or other).
/// </summary>
/// <remarks>
/// This record is immutable by design for thread safety and is passed through
/// the event dispatcher to all subscribers.
/// </remarks>
public record ActorDeathData(
    string VictimPilot,
    string AttackerPilot,
    string VictimShip,
    string Weapon,
    string WeaponClass,
    string DamageType,
    string Zone,
    DateTime Timestamp,
    KillType ClassifiedType
);
