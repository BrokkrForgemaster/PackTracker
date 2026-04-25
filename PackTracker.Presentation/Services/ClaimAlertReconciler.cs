namespace PackTracker.Presentation.Services;

public static class ClaimAlertReconciler
{
    public static (int lastKnownClaimCount, int? newClaimCount) Reconcile(
        int currentClaimCount,
        int? lastKnownClaimCount,
        int? existingNewClaimCount)
    {
        var known = lastKnownClaimCount ?? 0;
        var existing = existingNewClaimCount ?? 0;

        if (lastKnownClaimCount is null)
        {
            return currentClaimCount > 0
                ? (currentClaimCount, currentClaimCount)
                : (0, null);
        }

        var delta = currentClaimCount - known;
        if (delta > 0)
            return (currentClaimCount, existing + delta);

        if (delta < 0)
        {
            var reduced = existing + delta;
            return (currentClaimCount, reduced > 0 ? reduced : null);
        }

        return (known, existing > 0 ? existing : null);
    }
}
