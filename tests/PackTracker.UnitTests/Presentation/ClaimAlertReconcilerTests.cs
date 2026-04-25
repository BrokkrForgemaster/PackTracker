using PackTracker.Presentation.Services;

namespace PackTracker.UnitTests.Presentation;

public class ClaimAlertReconcilerTests
{
    [Fact]
    public void Reconcile_AddsNewAlert_WhenClaimCountIncreases()
    {
        var result = ClaimAlertReconciler.Reconcile(
            currentClaimCount: 2,
            lastKnownClaimCount: 1,
            existingNewClaimCount: 0);

        Assert.Equal(2, result.lastKnownClaimCount);
        Assert.Equal(1, result.newClaimCount);
    }

    [Fact]
    public void Reconcile_ReducesAlert_WhenClaimCountDrops()
    {
        var result = ClaimAlertReconciler.Reconcile(
            currentClaimCount: 1,
            lastKnownClaimCount: 2,
            existingNewClaimCount: 2);

        Assert.Equal(1, result.lastKnownClaimCount);
        Assert.Equal(1, result.newClaimCount);
    }

    [Fact]
    public void Reconcile_ClearsAlert_WhenUnclaimRemovesLastOutstandingClaim()
    {
        var result = ClaimAlertReconciler.Reconcile(
            currentClaimCount: 0,
            lastKnownClaimCount: 1,
            existingNewClaimCount: 1);

        Assert.Equal(0, result.lastKnownClaimCount);
        Assert.Null(result.newClaimCount);
    }
}
