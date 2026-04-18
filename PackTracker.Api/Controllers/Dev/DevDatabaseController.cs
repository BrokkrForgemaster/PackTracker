using Microsoft.AspNetCore.Mvc;
using PackTracker.Application.Abstractions.Services;

namespace PackTracker.Api.Controllers.Dev;

[ApiController]
[Route("api/dev/database")]
public sealed class DevDatabaseController : ControllerBase
{
    private readonly IDatabaseResetTool _databaseResetTool;
    private readonly IWebHostEnvironment _environment;

    public DevDatabaseController(
        IDatabaseResetTool databaseResetTool,
        IWebHostEnvironment environment)
    {
        _databaseResetTool = databaseResetTool;
        _environment = environment;
    }

    [HttpPost("clear")]
    public async Task<IActionResult> Clear(CancellationToken cancellationToken)
    {
        if (!_environment.IsDevelopment())
        {
            return NotFound();
        }

        await _databaseResetTool.ClearAllTablesAsync("CLEAR_MY_DATABASE",cancellationToken);
        return Ok(new { message = "All database tables were cleared successfully." });
    }
}