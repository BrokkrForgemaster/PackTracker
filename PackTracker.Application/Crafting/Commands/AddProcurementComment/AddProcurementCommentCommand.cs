using MediatR;
using Microsoft.EntityFrameworkCore;
using PackTracker.Application.Common;
using PackTracker.Application.DTOs.Crafting;
using PackTracker.Application.Interfaces;
using PackTracker.Domain.Entities;

namespace PackTracker.Application.Crafting.Commands.AddProcurementComment;

public sealed record AddProcurementCommentCommand(Guid RequestId, AddRequestCommentDto Request) : IRequest<OperationResult<Guid>>;

public sealed class AddProcurementCommentCommandHandler : IRequestHandler<AddProcurementCommentCommand, OperationResult<Guid>>
{
    private readonly IApplicationDbContext _db;
    private readonly ICurrentUserService _currentUser;
    private readonly ICraftingWorkflowNotifier _notifier;

    public AddProcurementCommentCommandHandler(
        IApplicationDbContext db,
        ICurrentUserService currentUser,
        ICraftingWorkflowNotifier notifier)
    {
        _db = db;
        _currentUser = currentUser;
        _notifier = notifier;
    }

    public async Task<OperationResult<Guid>> Handle(AddProcurementCommentCommand command, CancellationToken cancellationToken)
    {
        var exists = await _db.MaterialProcurementRequests
            .AsNoTracking()
            .AnyAsync(x => x.Id == command.RequestId, cancellationToken);
        if (!exists)
            return OperationResult<Guid>.Fail("Procurement request not found.");

        if (!_currentUser.IsAuthenticated || string.IsNullOrWhiteSpace(_currentUser.UserId))
            return OperationResult<Guid>.Fail("Unauthorized");

        var profile = await _db.Profiles
            .FirstOrDefaultAsync(x => x.DiscordId == _currentUser.UserId, cancellationToken);
        if (profile is null)
            return OperationResult<Guid>.Fail("Unauthorized");

        var comment = new RequestComment
        {
            RequestId = command.RequestId,
            AuthorProfileId = profile.Id,
            Content = command.Request.Content.Trim(),
            IsLiveChat = false,
            CreatedAt = DateTime.UtcNow
        };

        _db.RequestComments.Add(comment);
        await _db.SaveChangesAsync(cancellationToken);
        await _notifier.NotifyAsync("RequestCommentAdded", command.RequestId, cancellationToken);

        return OperationResult<Guid>.Ok(comment.Id);
    }
}
