using System.Collections.ObjectModel;
using System.Windows.Media;
using Microsoft.Extensions.Logging;
using PackTracker.Application.Interfaces;

namespace PackTracker.Presentation.ViewModels;

public class RefineryJobsViewModel
{
    private readonly IRegolithService _service;
    private readonly ILogger<RefineryJobsViewModel> _logger;

    public ObservableCollection<RefineryJobCard> RefineryJobs { get; } = new();

    public RefineryJobsViewModel(IRegolithService service, ILogger<RefineryJobsViewModel> logger)
    {
        _service = service ?? throw new ArgumentNullException(nameof(service));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));

        _ = LoadJobsAsync();
    }

    private async Task LoadJobsAsync(CancellationToken ct = default)
    {
        try
        {
            var jobs = await _service.GetRefineryJobsAsync(ct);

            RefineryJobs.Clear();
            foreach (var job in jobs)
            {
                RefineryJobs.Add(Map(job));
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to load refinery jobs.");
        }
    }

    private static RefineryJobCard Map(Application.DTOs.Regolith.RegolithRefineryJobDto job)
    {
        var etaLocal = job.Eta?.ToLocalTime();
        var completedLocal = job.CompletedAt?.ToLocalTime();

        return new RefineryJobCard
        {
            Material = job.Material,
            Quantity = job.Quantity,
            Yield = job.Yield,
            Efficiency = job.Efficiency,
            Progress = Math.Clamp(job.Progress, 0, 100),
            Status = job.Status,
            Location = job.Location,
            SubmittedAt = job.SubmittedAt.ToLocalTime(),
            CompletedAt = completedLocal,
            Eta = etaLocal,
            StatusColor = new SolidColorBrush(GetStatusColor(job.Status))
        };
    }

    private static Color GetStatusColor(string status) => status.ToLowerInvariant() switch
    {
        "completed" or "complete" => Color.FromRgb(90, 200, 120),
        "processing" => Color.FromRgb(0, 180, 255),
        "pending" => Color.FromRgb(255, 200, 0),
        "queued" => Color.FromRgb(200, 90, 90),
        _ => Color.FromRgb(150, 150, 150)
    };
}

public class RefineryJobCard
{
    public string Material { get; set; } = string.Empty;
    public double Quantity { get; set; }
    public double Yield { get; set; }
    public double Efficiency { get; set; }
    public double Progress { get; set; }
    public string Status { get; set; } = string.Empty;
    public string Location { get; set; } = string.Empty;
    public DateTime SubmittedAt { get; set; }
    public DateTime? CompletedAt { get; set; }
    public DateTime? Eta { get; set; }
    public SolidColorBrush StatusColor { get; set; } = new(Color.FromRgb(150, 150, 150));

    public string RemainingTime
    {
        get
        {
            if (CompletedAt.HasValue)
                return string.Empty;

            if (!Eta.HasValue)
                return "—";

            var delta = Eta.Value - DateTime.Now;
            if (delta < TimeSpan.Zero)
                return "0:00";

            return $"{(int)delta.TotalHours:00}:{delta.Minutes:00}";
        }
    }
}
