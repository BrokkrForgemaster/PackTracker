namespace PackTracker.Domain.Enums;

public enum RequestKind
{
    None = 0,
    MiningMaterials = 1,     
    TradingGoods = 2,     
    ShipComponents = 3,      
    MissionBackup = 4,       
    CargoEscort = 5,        
    CombatSupport = 6,        
    ShipCrew = 7,            
    Transportation = 8,       
    LocationScout = 9,       
    Guidance = 10,   
    EventSupport = 11,     
    Other = 99
}