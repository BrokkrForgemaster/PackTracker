using PackTracker.Domain.Enums;

namespace PackTracker.Domain.Entities;

public class RequestTicket
{
    public int Id { get; set; }
    public string Title { get; set; } = "";
    public string? Description { get; set; }
    public RequestKind Kind { get; set; }
    public RequestPriority Priority { get; set; } = RequestPriority.Normal;
    public RequestStatus Status { get; set; } = RequestStatus.Open;
    public string CreatedByUserId { get; set; } = ""; 
    public string CreatedByDisplayName { get; set; } = "";
    public string? AssignedToUserId { get; set; }
    public string? AssignedToDisplayName { get; set; }
    public DateTime? DueAt { get; set; }
    
    public string SkillObjective { get; set; } = "";
    
    public string GameBuild { get; set; } = "";
    
    public string PlayerHandle { get; set; } = "";
    
    public string TimeZone { get; set; } = "";
    
    public bool HasMic { get; set; }
    
    public string PlatformSpecs { get; set; } = "";
    
    public string Availability { get; set; } = "";
    
    public string CurrentBaseline { get; set; } = "";
    
    public string AssetsShips { get; set; } = "";
    
    public string Urgency { get; set; } = "";
    
    public string GroupPreference { get; set; } = "";
    
    public string SuccessCriteria { get; set; } = "";
    
    public string RecordingPermission { get; set; } = "";

    // Material/Resource Request Fields
    public string? MaterialName { get; set; }           // e.g., "Quantanium", "Hadanite", "Medical Supplies"
    public int? QuantityNeeded { get; set; }            // Amount needed
    public string? MeetingLocation { get; set; }        // Where to meet/deliver (e.g., "Port Olisar", "Crusader L1")
    public string? RewardOffered { get; set; }          // aUEC amount or trade offer
    public int? NumberOfHelpersNeeded { get; set; }     // How many people needed (for crew/escort requests)

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
    public DateTime? CompletedAt { get; set; }
    public string? CompletedByUserId { get; set; }
}