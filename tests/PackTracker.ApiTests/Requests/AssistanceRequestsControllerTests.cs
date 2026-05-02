using MediatR;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using PackTracker.Api.Controllers;
using PackTracker.ApiTests.TestDoubles;
using PackTracker.Application;
using PackTracker.Application.DTOs.Request;
using PackTracker.Application.Interfaces;
using PackTracker.Domain.Entities;
using PackTracker.Domain.Enums;
using PackTracker.Infrastructure.Persistence;

namespace PackTracker.ApiTests.Requests;

public class AssistanceRequestsControllerTests
{
    private const string TestDiscordId = "999888777666555";

    private static AppDbContext CreateDb() =>
        new(new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString()).Options);

    private static AssistanceRequestsController BuildController(
        AppDbContext db,
        string discordId = TestDiscordId,
        string displayName = "testuser",
        string? role = null)
    {
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddApplication();
        services.AddSingleton<IApplicationDbContext>(db);
        services.AddSingleton<ICurrentUserService>(new TestCurrentUserService(discordId, displayName, role));
        services.AddSingleton<IProfileService>(_ => new FakeProfileService(db));
        services.AddSingleton<IAssistanceRequestNotifier, TestAssistanceRequestNotifier>();

        var provider = services.BuildServiceProvider();
        var sender = provider.GetRequiredService<ISender>();

        var controller = new AssistanceRequestsController(sender, NullLogger<AssistanceRequestsController>.Instance)
        {
            ControllerContext = new ControllerContext
            {
                HttpContext = new DefaultHttpContext()
            }
        };

        return controller;
    }

    private static async Task<Profile> SeedProfileAsync(AppDbContext db, string discordId = TestDiscordId, string? role = null)
    {
        var profile = new Profile { DiscordId = discordId, Username = "testuser", DiscordRank = role };
        db.Profiles.Add(profile);
        await db.SaveChangesAsync();
        return profile;
    }

    [Fact]
    public async Task GetRequests_ReturnsOk_WithEmptyList()
    {
        var db = CreateDb();
        var controller = BuildController(db);

        var result = await controller.GetRequests(null, null, CancellationToken.None);

        var ok = Assert.IsType<OkObjectResult>(result.Result);
        var list = Assert.IsAssignableFrom<IReadOnlyList<AssistanceRequestDto>>(ok.Value);
        Assert.Empty(list);
    }

    [Fact]
    public async Task GetRequests_ExcludesCancelledAndCompleted_ForNonOwner()
    {
        var db = CreateDb();
        var ownerProfile = await SeedProfileAsync(db, discordId: "owner-discord-999");
        // Current user is a different person — should not see the owner's closed items
        var controller = BuildController(db, discordId: "viewer-discord-111");

        db.AssistanceRequests.AddRange(
            new AssistanceRequest { Title = "Open", Status = RequestStatus.Open, CreatedByProfileId = ownerProfile.Id },
            new AssistanceRequest { Title = "Cancelled", Status = RequestStatus.Cancelled, CreatedByProfileId = ownerProfile.Id },
            new AssistanceRequest { Title = "Completed", Status = RequestStatus.Completed, CreatedByProfileId = ownerProfile.Id });
        await db.SaveChangesAsync();

        var result = await controller.GetRequests(null, null, CancellationToken.None);

        var ok = Assert.IsType<OkObjectResult>(result.Result);
        var list = Assert.IsAssignableFrom<IReadOnlyList<AssistanceRequestDto>>(ok.Value);
        Assert.Single(list);
        Assert.Equal("Open", list[0].Title);
    }

    [Fact]
    public async Task GetRequests_OwnerOnlySeesActiveRequestsByDefault()
    {
        var db = CreateDb();
        var profile = await SeedProfileAsync(db);
        var controller = BuildController(db); // current user IS the owner

        db.AssistanceRequests.AddRange(
            new AssistanceRequest { Title = "Open", Status = RequestStatus.Open, CreatedByProfileId = profile.Id },
            new AssistanceRequest { Title = "Cancelled", Status = RequestStatus.Cancelled, CreatedByProfileId = profile.Id },
            new AssistanceRequest { Title = "Completed", Status = RequestStatus.Completed, CreatedByProfileId = profile.Id });
        await db.SaveChangesAsync();

        var result = await controller.GetRequests(null, null, CancellationToken.None);

        var ok = Assert.IsType<OkObjectResult>(result.Result);
        var list = Assert.IsAssignableFrom<IReadOnlyList<AssistanceRequestDto>>(ok.Value);
        var request = Assert.Single(list);
        Assert.Equal("Open", request.Title);
    }

    [Fact]
    public async Task CreateRequest_ReturnsUnauthorized_WhenNoProfile()
    {
        var db = CreateDb();
        var controller = BuildController(db);

        var result = await controller.CreateRequest(new RequestCreateDto { Title = "Need help" }, CancellationToken.None);

        Assert.IsType<UnauthorizedResult>(result);
    }

    [Fact]
    public async Task CreateRequest_ReturnsOk_WhenValid()
    {
        var db = CreateDb();
        await SeedProfileAsync(db);
        var controller = BuildController(db);

        var result = await controller.CreateRequest(new RequestCreateDto { Title = "Need help please" }, CancellationToken.None);

        Assert.IsType<OkObjectResult>(result);
        Assert.Equal(1, await db.AssistanceRequests.CountAsync());
    }

    [Fact]
    public async Task ClaimRequest_ReturnsBadRequest_WhenAlreadyAccepted()
    {
        var db = CreateDb();
        var profile = await SeedProfileAsync(db);
        var controller = BuildController(db);

        var request = new AssistanceRequest
        {
            Title = "Accepted request",
            Status = RequestStatus.Accepted,
            CreatedByProfileId = profile.Id
        };
        db.AssistanceRequests.Add(request);
        await db.SaveChangesAsync();

        var result = await controller.ClaimRequest(request.Id, CancellationToken.None);

        Assert.IsType<BadRequestObjectResult>(result);
    }

    [Fact]
    public async Task ClaimRequest_ReturnsNotFound_WhenRequestMissing()
    {
        var db = CreateDb();
        await SeedProfileAsync(db);
        var controller = BuildController(db);

        var result = await controller.ClaimRequest(Guid.NewGuid(), CancellationToken.None);

        Assert.IsType<NotFoundObjectResult>(result);
    }

    [Fact]
    public async Task CancelRequest_ReturnsForbidden_WhenNotCreator()
    {
        var db = CreateDb();
        var creatorDiscordId = "111222333444555";
        var creatorProfile = await SeedProfileAsync(db, creatorDiscordId);
        await SeedProfileAsync(db, TestDiscordId);

        var request = new AssistanceRequest
        {
            Title = "Creator's request",
            Status = RequestStatus.Open,
            CreatedByProfileId = creatorProfile.Id
        };
        db.AssistanceRequests.Add(request);
        await db.SaveChangesAsync();

        var controller = BuildController(db, TestDiscordId);

        var result = await controller.CancelRequest(request.Id, CancellationToken.None);

        var statusResult = Assert.IsType<ObjectResult>(result);
        Assert.Equal(403, statusResult.StatusCode);
    }

    [Fact]
    public async Task CancelRequest_ReturnsOk_WhenModerator()
    {
        var db = CreateDb();
        var creatorDiscordId = "111222333444555";
        var creatorProfile = await SeedProfileAsync(db, creatorDiscordId);
        await SeedProfileAsync(db, TestDiscordId, role: "Captain");

        var request = new AssistanceRequest
        {
            Title = "Moderator can cancel",
            Status = RequestStatus.Open,
            CreatedByProfileId = creatorProfile.Id
        };
        db.AssistanceRequests.Add(request);
        await db.SaveChangesAsync();

        var controller = BuildController(db, TestDiscordId, role: "Captain");

        var result = await controller.CancelRequest(request.Id, CancellationToken.None);

        Assert.IsType<OkObjectResult>(result);

        var updated = await db.AssistanceRequests.FindAsync(request.Id);
        Assert.Equal(RequestStatus.Cancelled, updated!.Status);
    }

    [Fact]
    public async Task CancelRequest_ReturnsOk_WhenCreator()
    {
        var db = CreateDb();
        var profile = await SeedProfileAsync(db);
        var controller = BuildController(db);

        var request = new AssistanceRequest
        {
            Title = "My request",
            Status = RequestStatus.Open,
            CreatedByProfileId = profile.Id
        };
        db.AssistanceRequests.Add(request);
        await db.SaveChangesAsync();

        var result = await controller.CancelRequest(request.Id, CancellationToken.None);

        Assert.IsType<OkObjectResult>(result);

        var updated = await db.AssistanceRequests.FindAsync(request.Id);
        Assert.Equal(RequestStatus.Cancelled, updated!.Status);
    }

    [Fact]
    public async Task PinRequest_ReturnsForbidden_WhenUserIsBelowRallyMaster()
    {
        var db = CreateDb();
        var profile = await SeedProfileAsync(db, role: "Wolf Dragoon");
        var controller = BuildController(db, role: "Wolf Dragoon");

        var request = new AssistanceRequest
        {
            Title = "Need escort",
            Status = RequestStatus.Open,
            CreatedByProfileId = profile.Id
        };
        db.AssistanceRequests.Add(request);
        await db.SaveChangesAsync();

        var result = await controller.PinRequest(request.Id.ToString(), CancellationToken.None);

        var status = Assert.IsType<ObjectResult>(result);
        Assert.Equal(403, status.StatusCode);
    }

    [Fact]
    public async Task PinRequest_ReturnsOk_WhenUserIsRallyMasterOrAbove()
    {
        var db = CreateDb();
        var profile = await SeedProfileAsync(db, role: "Rally Master");
        var controller = BuildController(db, role: "Rally Master");

        var request = new AssistanceRequest
        {
            Title = "Critical cargo escort",
            Status = RequestStatus.Open,
            CreatedByProfileId = profile.Id
        };
        db.AssistanceRequests.Add(request);
        await db.SaveChangesAsync();

        var result = await controller.PinRequest(request.Id.ToString(), CancellationToken.None);

        Assert.IsType<OkObjectResult>(result);
        var updated = await db.AssistanceRequests.FindAsync(request.Id);
        Assert.True(updated!.IsPinned);
    }

    [Fact]
    public async Task UnclaimRequest_ReturnsOk_WhenCurrentUserHasClaimedRequest()
    {
        var db = CreateDb();
        var profile = await SeedProfileAsync(db);
        var controller = BuildController(db);

        var request = new AssistanceRequest
        {
            Title = "Claimed request",
            Status = RequestStatus.Accepted,
            CreatedByProfileId = profile.Id
        };
        db.AssistanceRequests.Add(request);
        await db.SaveChangesAsync();

        db.RequestClaims.Add(new RequestClaim
        {
            RequestId = request.Id,
            RequestType = "Assistance",
            ProfileId = profile.Id
        });
        await db.SaveChangesAsync();

        var result = await controller.UnclaimRequest(request.Id.ToString(), CancellationToken.None);

        Assert.IsType<OkObjectResult>(result);
        Assert.Empty(db.RequestClaims);

        var updated = await db.AssistanceRequests.FindAsync(request.Id);
        Assert.Equal(RequestStatus.Open, updated!.Status);
    }

    [Fact]
    public async Task CompleteRequest_ReturnsBadRequest_WhenAlreadyCompleted()
    {
        var db = CreateDb();
        var profile = await SeedProfileAsync(db);
        var controller = BuildController(db);

        var request = new AssistanceRequest
        {
            Title = "Done request",
            Status = RequestStatus.Completed,
            CreatedByProfileId = profile.Id
        };
        db.AssistanceRequests.Add(request);
        await db.SaveChangesAsync();

        var result = await controller.CompleteRequest(request.Id, CancellationToken.None);

        Assert.IsType<BadRequestObjectResult>(result);
    }

    [Fact]
    public async Task CompleteRequest_ReturnsForbidden_WhenNotCreator()
    {
        var db = CreateDb();
        var creatorDiscordId = "111222333444555";
        var creatorProfile = await SeedProfileAsync(db, creatorDiscordId);
        await SeedProfileAsync(db, TestDiscordId);

        var request = new AssistanceRequest
        {
            Title = "Only creator may complete",
            Status = RequestStatus.Accepted,
            CreatedByProfileId = creatorProfile.Id
        };
        db.AssistanceRequests.Add(request);
        await db.SaveChangesAsync();

        var controller = BuildController(db, TestDiscordId);

        var result = await controller.CompleteRequest(request.Id, CancellationToken.None);

        var statusResult = Assert.IsType<ObjectResult>(result);
        Assert.Equal(StatusCodes.Status403Forbidden, statusResult.StatusCode);
    }

    private sealed class TestCurrentUserService : ICurrentUserService
    {
        private readonly string? _role;

        public TestCurrentUserService(string userId, string displayName, string? role)
        {
            UserId = userId;
            DisplayName = displayName;
            _role = role;
        }

        public string UserId { get; }

        public string DisplayName { get; }

        public bool IsAuthenticated => true;

        public bool IsInRole(string role) => string.Equals(role, _role, StringComparison.OrdinalIgnoreCase);
    }

    private sealed class TestAssistanceRequestNotifier : IAssistanceRequestNotifier
    {
        public Task NotifyCreatedAsync(Guid requestId, CancellationToken cancellationToken) => Task.CompletedTask;

        public Task NotifyUpdatedAsync(Guid requestId, CancellationToken cancellationToken) => Task.CompletedTask;

        public Task NotifyClaimedAsync(string requesterDiscordId, string claimerDiscordId, string claimerDisplayName, string requesterDisplayName, Guid requestId, string requestTitle, CancellationToken cancellationToken) => Task.CompletedTask;
    }
}
