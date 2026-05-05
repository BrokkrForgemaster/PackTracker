using PackTracker.Application.DTOs.Crafting;
using PackTracker.Presentation.ViewModels;

namespace PackTracker.UnitTests.Presentation;

public class CraftingRequestsViewModelTests
{
    [Fact]
    public void CanAssignRequest_ReturnsFalse_WhenCrafterAlreadyAssigned()
    {
        var request = new CraftingRequestListItemDto
        {
            Status = RequestStatus.Open,
            AssignedCrafterUsername = "wolfcrafter"
        };

        var canAssign = CraftingRequestsViewModel.CanAssignRequest(request);

        Assert.False(canAssign);
    }

    [Fact]
    public void CanAssignRequest_ReturnsTrue_WhenRequestIsOpenAndUnassigned()
    {
        var request = new CraftingRequestListItemDto
        {
            Status = RequestStatus.Open,
            AssignedCrafterUsername = null
        };

        var canAssign = CraftingRequestsViewModel.CanAssignRequest(request);

        Assert.True(canAssign);
    }
}
