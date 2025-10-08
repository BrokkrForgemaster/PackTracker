using System.Collections.ObjectModel;
using System.Windows.Media;
using Microsoft.Extensions.DependencyInjection;
using PackTracker.Application.DTOs.Regolith;
using PackTracker.Application.Interfaces;

namespace PackTracker.Presentation.ViewModels
{
    public class RefineryJobsViewModel
    {
        public ObservableCollection<RefineryJobCard> RefineryJobs { get; set; } = new();
        public ObservableCollection<RegolithRefineryJobDto> RefineryJob { get; } = new();
        
        private readonly IRegolithService _service;

        public RefineryJobsViewModel(IRegolithService service)
        {
            _service = service;
            _ = LoadJobsAsync();
        }

        private async Task LoadJobsAsync()
        {
            var jobs = await _service.GetRefineryJobsAsync();
            RefineryJobs.Clear();
            foreach (var j in jobs)
                RefineryJobs.Add(new RefineryJobCard
                {
                    OreType = j.OreType,
                    Quantity = j.Quantity,
                    Yield = j.Yield,
                    Efficiency = j.Efficiency,
                    Progress = j.Progress,
                    Status = j.Status,
                    Location = j.Location,
                    Eta = j.Eta,
                    StatusColor = new SolidColorBrush(GetStatusColor(j.Status))
                });
        }


        private Color GetStatusColor(string status) => status.ToLower() switch
        {
            "completed" => Color.FromRgb(90, 200, 120),
            "processing" => Color.FromRgb(0, 180, 255),
            "pending" => Color.FromRgb(255, 200, 0),
            "queued" => Color.FromRgb(200, 90, 90),
            _ => Color.FromRgb(150, 150, 150)
        };
    }

    public class RefineryJobCard
    {
        public string OreType { get; set; } = string.Empty;
        public double Quantity { get; set; }
        public double Yield { get; set; }
        public double Efficiency { get; set; }
        public double Progress { get; set; }
        public string Status { get; set; } = string.Empty;
        public string Location { get; set; } = string.Empty;
        public DateTime Eta { get; set; }
        public SolidColorBrush StatusColor { get; set; } = new(Color.FromRgb(150, 150, 150));
    }
}
