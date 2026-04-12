using System.Security.Claims;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.SignalR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using PackTracker.Api.Controllers;
using PackTracker.Api.Hubs;
using PackTracker.Application.DTOs.Request;
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

    private static AssistanceRequestsController BuildController(AppDbContext db, string discordId = TestDiscordId)
    {
        var hubMock = new Mock<IHubContext<RequestsHub>>();

        var clientsMock = new Mock<IHubClients>();
        var clientProxyMock = new Mock<IClientProxy>();
        clientsMock.Setup(c => c.All).Returns(clientProxyMock.Object);
        clientProxyMock.Setup(c => c.SendCoreAsync(It.IsAny<string>(), It.IsAny<object[]>(), It.IsAny<CancellationToken>()))
                       .Returns(Task.CompletedTask);
        hubMock.Setup(h => h.Clients).Returns(clientsMock.Object);

        var controller = new AssistanceRequestsController(db, NullLogger<AssistanceRequestsController>.Instance, hubMock.Object);

        controller.ControllerContext = new ControllerContext
        {
            HttpContext = new DefaultHttpContext
            {
                User = new ClaimsPrincipal(new ClaimsIdentity(new[]
                {
                    new Claim(ClaimTypes.NameIdentifier, discordId),
                    new Claim(ClaimTypes.Name, "testuser")
                }, "Test"))
            }
        };
        return controller;
    }

    private static async Task<Profile> SeedProfileAsync(AppDbContext db, string discordId = TestDiscordId)
    {
        var profile = new Profile { DiscordId = discordId, Username = "testuser" };
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
    public async Task GetRequests_ExcludesCancelledAndCompleted()
    {
        var db = CreateDb();
        var profile = await SeedProfileAsync(db);
        var controller = BuildController(db);

        db.AssistanceRequests.AddRange(
            new AssistanceRequest { Title = "Open", Status = RequestStatus.Open, CreatedByProfileId = profile.Id },
            new AssistanceRequest { Title = "Cancelled", Status = RequestStatus.Cancelled, CreatedByProfileId = profile.Id },
            new AssistanceRequest { Title = "Completed", Status = RequestStatus.Completed, CreatedByProfileId = profile.Id }
        );
        await db.SaveChangesAsync();

        var result = await controller.GetRequests(null, null, CancellationToken.None);

        var ok = Assert.IsType<OkObjectResult>(result.Result);
        var list = Assert.IsAssignableFrom<IReadOnlyList<AssistanceRequestDto>>(ok.Value);
        Assert.Single(list);
        Assert.Equal("Open", list[0].Title);
    }

    [Fact]
    public async Task GetRequests_FiltersByKindAndStatus_OnServer()
    {
        var db = CreateDb();
        var profile = await SeedProfileAsync(db);
        var controller = BuildController(db);

        db.AssistanceRequests.AddRange(
            new AssistanceRequest
            {
                Title = "Mining Open",
                Kind = RequestKind.MiningMaterials,
                Status = RequestStatus.Open,
                CreatedByProfileId = profile.Id
            },
            new AssistanceRequest
            {
                Title = "Mining Completed",
                Kind = RequestKind.MiningMaterials,
                Status = RequestStatus.Completed,
                CreatedByProfileId = profile.Id
            },
            new AssistanceRequest
            {
                Title = "Escort Completed",
                Kind = RequestKind.CargoEscort,
                Status = RequestStatus.Completed,
                CreatedByProfileId = profile.Id
            });
        await db.SaveChangesAsync();

        var result = await controller.GetRequests(
            RequestKind.MiningMaterials,
            RequestStatus.Completed,
            CancellationToken.None);

        var ok = Assert.IsType<OkObjectResult>(result.Result);
        var list = Assert.IsAssignableFrom<IReadOnlyList<AssistanceRequestDto>>(ok.Value);

        var request = Assert.Single(list);
        Assert.Equal("Mining Completed", request.Title);
        Assert.Equal(RequestKind.MiningMaterials, request.Kind);
        Assert.Equal(RequestStatus.Completed.ToString(), request.Status);
    }

    [Fact]
    public async Task CreateRequest_ReturnsBadRequest_WhenTitleEmpty()
    {
        var db = CreateDb();
        var controller = BuildController(db);

        var result = await controller.CreateRequest(new RequestCreateDto { Title = "" }, CancellationToken.None);

        Assert.IsType<BadRequestObjectResult>(result);
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
        var callerProfile = await SeedProfileAsync(db, TestDiscordId);

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
}
