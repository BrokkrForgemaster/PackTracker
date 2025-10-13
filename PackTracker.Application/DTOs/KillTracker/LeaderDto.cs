namespace PackTracker.Application.DTOs.KillTracker;

public class LeaderDto
{
    public string? Attacker { get; set; }
    public int KillCount { get; set; }
    public string? MostUsedWeapon { get; set; }
}