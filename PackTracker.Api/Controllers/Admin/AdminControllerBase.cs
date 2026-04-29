using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using PackTracker.Domain.Security;

namespace PackTracker.Api.Controllers.Admin;

[ApiController]
[Authorize(Policy = AdminPolicyNames.AdminAccess)]
[Route("api/v1/admin/[controller]")]
public abstract class AdminControllerBase : ControllerBase
{
}
