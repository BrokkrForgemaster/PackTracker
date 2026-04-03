namespace PackTracker.Domain.Events;

/// <summary>
/// Immutable event data representing a vehicle destruction in Star Citizen.
/// </summary>
public record VehicleDestructionData(
    string VehicleName,
    string Owner,
    DateTime Timestamp,
    string Location
);
