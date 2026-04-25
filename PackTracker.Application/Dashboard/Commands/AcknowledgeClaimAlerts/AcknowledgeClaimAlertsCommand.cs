using FluentValidation;
using MediatR;
using Microsoft.EntityFrameworkCore;
using PackTracker.Application.Common;
using PackTracker.Application.Interfaces;

namespace PackTracker.Application.Dashboard.Commands.AcknowledgeClaimAlerts;

public sealed record AcknowledgeClaimAlertsCommand(
    Dictionary<string, int> AcknowledgedClaimCounts) : IRequest<Result>;

public sealed class AcknowledgeClaimAlertsCommandValidator : AbstractValidator<AcknowledgeClaimAlertsCommand>
{
    public AcknowledgeClaimAlertsCommandValidator()
    {
        RuleForEach(x => x.AcknowledgedClaimCounts.Keys)
            .Must(key => Guid.TryParse(key, out _))
            .WithMessage("Acknowledged claim count keys must be valid request IDs.");

        RuleForEach(x => x.AcknowledgedClaimCounts.Values)
            .GreaterThanOrEqualTo(0);
    }
}

public sealed class AcknowledgeClaimAlertsCommandHandler
    : IRequestHandler<AcknowledgeClaimAlertsCommand, Result>
{
    private readonly IApplicationDbContext _dbContext;
    private readonly ICurrentUserService _currentUser;

    public AcknowledgeClaimAlertsCommandHandler(
        IApplicationDbContext dbContext,
        ICurrentUserService currentUser)
    {
        _dbContext = dbContext;
        _currentUser = currentUser;
    }

    public async Task<Result> Handle(AcknowledgeClaimAlertsCommand request, CancellationToken cancellationToken)
    {
        var profile = await _dbContext.Profiles
            .FirstOrDefaultAsync(x => x.DiscordId == _currentUser.UserId, cancellationToken)
            .ConfigureAwait(false);

        if (profile is null)
            return Result.Fail("Unauthorized");

        profile.AcknowledgedClaimCounts = request.AcknowledgedClaimCounts
            .Where(static pair => Guid.TryParse(pair.Key, out _) && pair.Value >= 0)
            .ToDictionary(static pair => pair.Key, static pair => pair.Value);

        await _dbContext.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        return Result.Ok();
    }
}
