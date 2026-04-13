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
        public const string Foundling = "Foundling";
        public const string WolfDragoon = "Wolf Dragoon";
        public const string RallyMaster = "Rally Master";
        public const string PlatoonSergeant = "Platoon Sergeant";
        public const string FieldMarshal = "Field Marshal";
        public const string Lieutenant = "Lieutenant";
        public const string Captain = "Captain";
        public const string FleetCommander = "Fleet Commander";
        public const string Armor = "Armor";
        public const string HighCouncilor = "High Councilor";
        public const string HandOfTheClan = "Hand of the Clan";
        public const string ClanWarlord = "Clan Warlord";
    }

    /// <summary>
    /// Complete role hierarchy ordered from lowest (index 0) to highest.
    /// Used to determine a user's highest role when they hold multiple roles.
    /// </summary>
    public static readonly IReadOnlyList<string> RoleHierarchy =
        Array.AsReadOnly(new[]
        {
            Roles.Foundling,
            Roles.WolfDragoon,
            Roles.RallyMaster,
            Roles.PlatoonSergeant,
            Roles.FieldMarshal,
            Roles.Lieutenant,
            Roles.Captain,
            Roles.FleetCommander,
            Roles.Armor,
            Roles.HighCouncilor,
            Roles.HandOfTheClan,
            Roles.ClanWarlord
        });

    public static readonly IReadOnlyCollection<string> ElevatedRequestRoles =
        Array.AsReadOnly([
            Roles.Captain,
            Roles.FleetCommander,
            Roles.Armor,
            Roles.HighCouncilor,
            Roles.HandOfTheClan,
            Roles.ClanWarlord
        ]);

    public static bool IsElevatedRequestRole(string? role) =>
        !string.IsNullOrWhiteSpace(role)
        && ElevatedRequestRoles.Contains(role, StringComparer.OrdinalIgnoreCase);

    /// <summary>
    /// Returns the hierarchy position of a role (higher = more senior), or -1 if not recognized.
    /// </summary>
    public static int GetRolePosition(string? roleName)
    {
        if (string.IsNullOrWhiteSpace(roleName))
            return -1;

        for (int i = 0; i < RoleHierarchy.Count; i++)
        {
            if (string.Equals(RoleHierarchy[i], roleName, StringComparison.OrdinalIgnoreCase))
                return i;
        }

        return -1;
    }
}
