using Microsoft.AspNetCore.Mvc;
using PackTracker.Domain.Security;
using Microsoft.AspNetCore.Authorization;

namespace PackTracker.Api.Controllers.Admin;

/// <summary>
/// Base controller for admin controllers. All admin controllers should inherit
/// from this class to ensure that they are properly secured and have the correct route prefix.
/// </summary>
[ApiController]
[Authorize(Policy = AdminPolicyNames.AdminAccess)]
[Route("api/v1/admin/[controller]")]
public abstract class AdminControllerBase : ControllerBase
{
}
