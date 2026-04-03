namespace PackTracker.Application.DTOs.KillTracker;

public class KillStatsDto
{
    public int TotalKills { get; set; }
    public int TotalDeaths { get; set; }

    public Dictionary<string, int> KillsByType { get; set; } = new();

    public Dictionary<string, int> WeaponStats { get; set; } = new();
    public int KillsToday { get; set; }
    public int KillsThisWeek { get; set; }
    public int KillsThisMonth { get; set; }
    public double AverageKillsPerDay { get; set; }
    public string MostUsedWeapon { get; set; } = string.Empty;
    public int LongestKillStreak { get; set; }
    public DateTime? LastKillTimestamp { get; set; }
}