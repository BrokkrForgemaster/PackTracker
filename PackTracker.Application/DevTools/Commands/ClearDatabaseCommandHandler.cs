using MediatR;
using PackTracker.Application.Abstractions.Services;

namespace PackTracker.Application.DevTools.Commands;

public sealed class ClearDatabaseCommandHandler : IRequestHandler<ClearDatabaseCommand>
{
    private readonly IDatabaseResetTool _databaseResetTool;

    public ClearDatabaseCommandHandler(IDatabaseResetTool databaseResetTool)
    {
        _databaseResetTool = databaseResetTool;
    }

    public async Task Handle(ClearDatabaseCommand request, CancellationToken cancellationToken)
    {
        await _databaseResetTool.ClearAllTablesAsync("CLEAR_MY_DATABASE", cancellationToken);
    }
}