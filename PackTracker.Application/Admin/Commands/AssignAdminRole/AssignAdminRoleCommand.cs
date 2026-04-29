using System.Text.Json;
using MediatR;
using Microsoft.EntityFrameworkCore;
using PackTracker.Application.Admin.Abstractions;
using PackTracker.Application.Admin.DTOs;
using PackTracker.Domain.Entities.Admin;
using PackTracker.Domain.Security;

namespace PackTracker.Application.Admin.Commands.AssignAdminRole;

public sealed record AssignAdminRoleCommand(AssignAdminRoleRequestDto Request) : IRequest;

public sealed class AssignAdminRoleCommandHandler : IRequestHandler<AssignAdminRoleCommand>
{
    private readonly IAdminDbContext _db;
    private readonly IAuthorizationService _authorization;
    private readonly IRbacService _rbac;
    private readonly IAuditLogService _audit;

    public AssignAdminRoleCommandHandler(
        IAdminDbContext db,
        IAuthorizationService authorization,
        IRbacService rbac,
        IAuditLogService audit)
    {
        _db = db;
        _authorization = authorization;
        _rbac = rbac;
        _audit = audit;
    }

    public async Task Handle(AssignAdminRoleCommand command, CancellationToken cancellationToken)
    {
        await _authorization.RequirePermissionAsync(AdminPermissions.MembersRolesManage, cancellationToken);

        var actor = await _rbac.GetCurrentAdminContextAsync(cancellationToken);
        var role = await _db.AdminRoles.FirstOrDefaultAsync(x => x.Id == command.Request.AdminRoleId, cancellationToken)
            ?? throw new InvalidOperationException("Admin role not found.");
        var profile = await _db.Profiles.FirstOrDefaultAsync(x => x.Id == command.Request.ProfileId, cancellationToken)
            ?? throw new InvalidOperationException("Profile not found.");

        var existing = await _db.MemberRoleAssignments.FirstOrDefaultAsync(
            x => x.ProfileId == command.Request.ProfileId
              && x.AdminRoleId == command.Request.AdminRoleId
              && x.RevokedAt == null,
            cancellationToken);

        if (existing is not null)
        {
            return;
        }

        var assignment = new MemberRoleAssignment
        {
            ProfileId = command.Request.ProfileId,
            AdminRoleId = command.Request.AdminRoleId,
            AssignedByProfileId = actor.ProfileId,
            AssignedAt = DateTime.UtcNow,
            Notes = command.Request.Notes
        };

        _db.MemberRoleAssignments.Add(assignment);
        await _db.SaveChangesAsync(cancellationToken);

        await _audit.WriteAsync(
            new AdminAuditLogEntryDto(
                "AdminRoleAssigned",
                "MemberRoleAssignment",
                assignment.Id.ToString(),
                $"Assigned admin role '{role.Name}' to {profile.Username}.",
                "Info",
                null,
                JsonSerializer.Serialize(new
                {
                    assignment.ProfileId,
                    ProfileUsername = profile.Username,
                    assignment.AdminRoleId,
                    RoleName = role.Name,
                    assignment.Notes
                })),
            cancellationToken);
    }
}
