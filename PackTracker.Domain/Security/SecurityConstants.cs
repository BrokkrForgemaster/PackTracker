using System.Collections.ObjectModel;

namespace PackTracker.Domain.Security;

/// <summary>
/// Defines shared authorization policies and role names used across the solution.
/// </summary>
public static class SecurityConstants
{
    public static class Policies
    {
        public const string HouseWolfOnly = "HouseWolfOnly";
    }

    public static class Roles
    {
        public const string HouseWolfMember = "HouseWolfMember";
        public const string Captain = "Captain";
        public const string FleetCommander = "Fleet Commander";
        public const string Armor = "Armor";
        public const string HandOfTheClan = "Hand of the Clan";
        public const string ClanWarlord = "Clan Warlord";
    }

    public static readonly IReadOnlyCollection<string> ElevatedRequestRoles =
        Array.AsReadOnly([
            Roles.Captain,
            Roles.FleetCommander,
            Roles.Armor,
            Roles.HandOfTheClan,
            Roles.ClanWarlord
        ]);

    public static bool IsElevatedRequestRole(string? role) =>
        !string.IsNullOrWhiteSpace(role)
        && ElevatedRequestRoles.Contains(role, StringComparer.OrdinalIgnoreCase);
}
