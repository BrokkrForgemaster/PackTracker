using System.Text.Json;
using MediatR;
using Microsoft.EntityFrameworkCore;
using PackTracker.Application.Admin.Abstractions;
using PackTracker.Application.Admin.DTOs;
using PackTracker.Domain.Security;

namespace PackTracker.Application.Admin.Commands.RevokeAdminRole;

public sealed record RevokeAdminRoleCommand(RevokeAdminRoleRequestDto Request) : IRequest;

public sealed class RevokeAdminRoleCommandHandler : IRequestHandler<RevokeAdminRoleCommand>
{
    private readonly IAdminDbContext _db;
    private readonly IAuthorizationService _authorization;
    private readonly IAuditLogService _audit;

    public RevokeAdminRoleCommandHandler(
        IAdminDbContext db,
        IAuthorizationService authorization,
        IAuditLogService audit)
    {
        _db = db;
        _authorization = authorization;
        _audit = audit;
    }

    public async Task Handle(RevokeAdminRoleCommand command, CancellationToken cancellationToken)
    {
        await _authorization.RequirePermissionAsync(AdminPermissions.MembersRolesManage, cancellationToken);

        var assignment = await _db.MemberRoleAssignments
            .Include(x => x.AdminRole)
            .Include(x => x.Profile)
            .FirstOrDefaultAsync(
                x => x.ProfileId == command.Request.ProfileId
                  && x.AdminRoleId == command.Request.AdminRoleId
                  && x.RevokedAt == null,
                cancellationToken);

        if (assignment is null)
        {
            return;
        }

        var beforeJson = JsonSerializer.Serialize(new
        {
            assignment.Id,
            assignment.ProfileId,
            ProfileUsername = assignment.Profile?.Username,
            assignment.AdminRoleId,
            RoleName = assignment.AdminRole?.Name,
            assignment.AssignedAt,
            assignment.RevokedAt,
            assignment.Notes
        });

        assignment.RevokedAt = DateTime.UtcNow;
        if (!string.IsNullOrWhiteSpace(command.Request.Notes))
        {
            assignment.Notes = command.Request.Notes;
        }

        await _db.SaveChangesAsync(cancellationToken);

        await _audit.WriteAsync(
            new AdminAuditLogEntryDto(
                "AdminRoleRevoked",
                "MemberRoleAssignment",
                assignment.Id.ToString(),
                $"Revoked admin role '{assignment.AdminRole?.Name}' from {assignment.Profile?.Username}.",
                "Warning",
                beforeJson,
                JsonSerializer.Serialize(new
                {
                    assignment.Id,
                    assignment.ProfileId,
                    ProfileUsername = assignment.Profile?.Username,
                    assignment.AdminRoleId,
                    RoleName = assignment.AdminRole?.Name,
                    assignment.AssignedAt,
                    assignment.RevokedAt,
                    assignment.Notes
                })),
            cancellationToken);
    }
}
