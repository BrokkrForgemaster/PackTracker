using Microsoft.EntityFrameworkCore;
using Moq;
using PackTracker.Application.Admin.Abstractions;
using PackTracker.Application.Admin.Commands.ImportMedals;
using PackTracker.Application.Admin.Common;
using PackTracker.Application.Admin.DTOs;
using PackTracker.Domain.Entities;
using PackTracker.Domain.Security;
using PackTracker.Infrastructure.Persistence;

namespace PackTracker.UnitTests.Admin;

public sealed class ImportMedalsCommandTests
{
    [Fact]
    public async Task Handle_ValidPayload_UpsertsMedalsAndLinksAwards()
    {
        await using var db = CreateDbContext();
        db.Profiles.Add(new Profile
        {
            Id = Guid.NewGuid(),
            Username = "Brokkr",
            DiscordDisplayName = "Brokkr Forgemaster",
            DiscordId = "12345",
            Discriminator = "0001"
        });
        await db.SaveChangesAsync();

        var authorization = new Mock<IAuthorizationService>();
        authorization
            .Setup(x => x.RequirePermissionAsync(AdminPermissions.MedalsManage, It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        var audit = new Mock<IAuditLogService>();
        audit
            .Setup(x => x.WriteAsync(It.IsAny<AdminAuditLogEntryDto>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        var rbac = new Mock<IRbacService>();
        rbac
            .Setup(x => x.GetCurrentAdminContextAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(new CurrentAdminContext(
                Guid.NewGuid(),
                "Admin",
                ["Clan Warlord"],
                [AdminPermissions.MedalsManage],
                AdminTier.SuperAdmin,
                true));

        var handler = new ImportMedalsCommandHandler(db, authorization.Object, audit.Object, rbac.Object);
        var request = new ImportMedalsRequestDto(
            AvailableMedals: new[]
            {
                new ImportMedalDefinitionDto("Medal of Honor", "Highest military decoration.", null, null),
                new ImportMedalDefinitionDto("Silver Star", "Third-highest military decoration.", null,  null),
            },
            AvailableRibbons: Array.Empty<ImportMedalDefinitionDto>(),
            Recipients: new Dictionary<string, IReadOnlyList<string>>
            {
                ["Medal of Honor"] = ["Brokkr Forgemaster"],
                ["Silver Star"] = ["Unknown Pilot"]
            });

        var result = await handler.Handle(new ImportMedalsCommand(request), CancellationToken.None);

        Assert.Equal(2, result.MedalDefinitionsCreated);
        Assert.Equal(0, result.MedalDefinitionsUpdated);
        Assert.Equal(2, result.AwardsCreated);
        Assert.Empty(result.UnknownMedals);
        Assert.Contains("Unknown Pilot", result.UnmatchedRecipients);

        var medals = await db.MedalDefinitions.OrderBy(x => x.DisplayOrder).ToListAsync();
        var awards = await db.MedalAwards.OrderBy(x => x.RecipientName).ToListAsync();

        Assert.Collection(medals,
            first => Assert.Equal("Medal of Honor", first.Name),
            second => Assert.Equal("Silver Star", second.Name));

        Assert.Equal(2, awards.Count);
        Assert.Equal("Brokkr Forgemaster", awards[0].RecipientName);
        Assert.NotNull(awards[0].ProfileId);
        Assert.Equal("Unknown Pilot", awards[1].RecipientName);
        Assert.Null(awards[1].ProfileId);
    }

    private static AppDbContext CreateDbContext()
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString("N"))
            .Options;

        return new AppDbContext(options);
    }
}
