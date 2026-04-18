using MediatR;

namespace PackTracker.Application.DevTools.Commands;

public sealed record ClearDatabaseCommand : IRequest;