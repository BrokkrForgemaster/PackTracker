using FluentValidation.TestHelper;
using PackTracker.Application.Crafting.Commands.CreateCraftingRequest;
using PackTracker.Application.Crafting.Commands.CreateProcurementRequest;
using PackTracker.Application.DTOs.Crafting;

namespace PackTracker.UnitTests.Crafting;

public sealed class CreateCraftingAndProcurementValidationTests
{
    [Fact]
    public void CreateCraftingRequestCommandValidator_RejectsRewardOfferedLongerThan100()
    {
        var validator = new CreateCraftingRequestCommandValidator();
        var command = new CreateCraftingRequestCommand(new CreateCraftingRequestDto
        {
            BlueprintId = Guid.NewGuid(),
            QuantityRequested = 1,
            MinimumQuality = 500,
            RewardOffered = new string('R', 101)
        });

        var result = validator.TestValidate(command);

        result.ShouldHaveValidationErrorFor(x => x.Request.RewardOffered);
    }

    [Fact]
    public void CreateProcurementRequestCommandValidator_RejectsRewardOfferedLongerThan100()
    {
        var validator = new CreateProcurementRequestCommandValidator();
        var command = new CreateProcurementRequestCommand(new CreateMaterialProcurementRequestDto
        {
            MaterialId = Guid.NewGuid(),
            QuantityRequested = 1,
            MinimumQuality = 500,
            RewardOffered = new string('R', 101)
        });

        var result = validator.TestValidate(command);

        result.ShouldHaveValidationErrorFor(x => x.Request.RewardOffered);
    }
}
