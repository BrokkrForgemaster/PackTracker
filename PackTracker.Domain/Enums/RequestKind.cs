namespace PackTracker.Domain.Enums;

public enum RequestKind
{
    None = 0,

    // Materials & Resources
    MiningMaterials = 1,      // Need specific ores/minerals
    TradingGoods = 2,         // Need commodities for trading
    ShipComponents = 3,       // Need ship parts/weapons

    // Mission Support
    MissionBackup = 4,        // Need help completing a mission
    CargoEscort = 5,          // Need escort for cargo run
    CombatSupport = 6,        // Need combat backup

    // Logistics
    ShipCrew = 7,             // Need crew members for multi-crew ship
    Transportation = 8,       // Need a ride/pickup
    LocationScout = 9,        // Need someone to scout a location

    // General
    Guidance = 10,            // Need advice/teaching
    EventSupport = 11,        // Need help with org event
    Other = 99
}