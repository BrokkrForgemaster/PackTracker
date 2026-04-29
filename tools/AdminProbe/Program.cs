using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using PackTracker.Application.Interfaces;
using PackTracker.Infrastructure.Persistence;
using PackTracker.Infrastructure.Services.Admin;

var discordId = args.Length > 0 ? args[0] : "371697279704236043";

var configuration = new ConfigurationBuilder()
    .AddJsonFile(Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", "PackTracker.Api", "appsettings.json"), optional: true)
    .AddUserSecrets<AppDbContext>(optional: true)
    .AddEnvironmentVariables()
    .Build();

var connectionString = configuration.GetConnectionString("DefaultConnection");
if (string.IsNullOrWhiteSpace(connectionString))
{
    Console.Error.WriteLine("No DefaultConnection string found.");
    return 1;
}

using var loggerFactory = LoggerFactory.Create(builder =>
{
    builder.AddSimpleConsole(options =>
    {
        options.SingleLine = true;
        options.TimestampFormat = "HH:mm:ss ";
    });
    builder.SetMinimumLevel(LogLevel.Information);
});

var options = new DbContextOptionsBuilder<AppDbContext>()
    .UseNpgsql(connectionString)
    .Options;

await using var db = new AppDbContext(options);

var profile = await db.Profiles.FirstOrDefaultAsync(x => x.DiscordId == discordId);
if (profile is null)
{
    Console.WriteLine($"Profile not found for DiscordId={discordId}");
    return 2;
}

Console.WriteLine($"ProfileId={profile.Id}");
Console.WriteLine($"DiscordId={profile.DiscordId}");
Console.WriteLine($"DisplayName={profile.DiscordDisplayName ?? profile.Username}");
Console.WriteLine($"DiscordRank={profile.DiscordRank ?? "<null>"}");

var assignments = await db.MemberRoleAssignments
    .Include(x => x.AdminRole)
    .ThenInclude(x => x!.PermissionAssignments)
    .Where(x => x.ProfileId == profile.Id && x.RevokedAt == null)
    .ToListAsync();

Console.WriteLine($"ExplicitAssignments={assignments.Count}");
foreach (var assignment in assignments)
{
    Console.WriteLine(
        $"Assignment Role={assignment.AdminRole?.Name ?? "<null>"} Tier={assignment.AdminRole?.Tier.ToString() ?? "<null>"} Permissions={assignment.AdminRole?.PermissionAssignments.Count ?? 0}");
}

var allAdminRoles = await db.AdminRoles
    .Include(x => x.PermissionAssignments)
    .OrderBy(x => x.Tier)
    .ThenBy(x => x.Name)
    .ToListAsync();

Console.WriteLine($"AdminRolesInDb={allAdminRoles.Count}");
foreach (var role in allAdminRoles)
{
    Console.WriteLine($"Role={role.Name} Tier={role.Tier} Permissions={role.PermissionAssignments.Count}");
}

var fallbackRole = await db.AdminRoles
    .Include(x => x.PermissionAssignments)
    .FirstOrDefaultAsync(x => x.Name == profile.DiscordRank);

Console.WriteLine($"FallbackRoleFound={fallbackRole is not null}");
if (fallbackRole is not null)
{
    Console.WriteLine($"FallbackRole={fallbackRole.Name}");
    Console.WriteLine($"FallbackTier={fallbackRole.Tier}");
    Console.WriteLine($"FallbackPermissions={string.Join(", ", fallbackRole.PermissionAssignments.Select(x => x.PermissionKey))}");
}

var currentUser = new ProbeCurrentUserService(profile.DiscordId, profile.DiscordDisplayName ?? profile.Username ?? "Unknown");
var rbac = new RbacService(db, currentUser, loggerFactory.CreateLogger<RbacService>());
var context = await rbac.GetCurrentAdminContextAsync();

Console.WriteLine($"ResolvedCanAccessAdmin={context.CanAccessAdmin}");
Console.WriteLine($"ResolvedHighestTier={context.HighestTier?.ToString() ?? "<null>"}");
Console.WriteLine($"ResolvedRoles={string.Join(", ", context.Roles)}");
Console.WriteLine($"ResolvedPermissions={string.Join(", ", context.Permissions)}");
return 0;

file sealed class ProbeCurrentUserService(string userId, string displayName) : ICurrentUserService
{
    public string UserId { get; } = userId;
    public string DisplayName { get; } = displayName;
    public bool IsAuthenticated => true;
    public bool IsInRole(string role) => false;
}
