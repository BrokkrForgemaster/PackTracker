using System.Collections.ObjectModel;
using System.Text;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Win32;
using PackTracker.Application.Admin.DTOs;
using PackTracker.Presentation.Services.Admin;
using PackTracker.Presentation.Views.Admin;
using System.IO;

namespace PackTracker.Presentation.ViewModels.Admin;

public partial class AdminAuditLogsViewModel : ObservableObject
{
    private readonly AdminApiClient _adminClient;

    [ObservableProperty]
    private bool _isLoading;

    [ObservableProperty]
    private string? _errorMessage;

    [ObservableProperty]
    private AdminAuditLogListItemDto? _selectedLog;

    public ObservableCollection<AdminAuditLogListItemDto> Logs { get; } = new();

    public AdminAuditLogsViewModel(AdminApiClient adminClient)
    {
        _adminClient = adminClient;
    }

    [RelayCommand]
    public async Task LoadLogsAsync()
    {
        IsLoading = true;
        ErrorMessage = null;
        Logs.Clear();

        try
        {
            var results = await _adminClient.GetAuditLogsAsync(take: 200);
            foreach (var log in results)
            {
                Logs.Add(log);
            }
        }
        catch (Exception ex)
        {
            ErrorMessage = $"Failed to load audit logs: {ex.Message}";
        }
        finally
        {
            IsLoading = false;
        }
    }

    [RelayCommand]
    public async Task ViewDetailsAsync()
    {
        if (SelectedLog == null) return;

        try
        {
            var detail = await _adminClient.GetAuditLogDetailAsync(SelectedLog.Id);
            if (detail != null)
            {
                var window = new AuditLogDetailWindow(detail);
                window.Owner = System.Windows.Application.Current.MainWindow;
                window.ShowDialog();
            }
        }
        catch (Exception ex)
        {
            ErrorMessage = $"Failed to load log details: {ex.Message}";
        }
    }

    [RelayCommand]
    public void ExportCsv()
    {
        if (Logs.Count == 0) return;

        var sfd = new SaveFileDialog
        {
            Filter = "CSV Files (*.csv)|*.csv",
            FileName = $"AuditLogs_{DateTime.Now:yyyyMMdd_HHmm}.csv"
        };

        if (sfd.ShowDialog() == true)
        {
            try
            {
                var sb = new StringBuilder();
                sb.AppendLine("OccurredAt,Severity,Actor,Action,TargetType,TargetId,Summary,Machine,Environment,HasException");

                foreach (var log in Logs)
                {
                    sb.AppendLine($"\"{log.OccurredAt:yyyy-MM-dd HH:mm:ss}\",\"{log.Severity}\",\"{Escape(log.ActorDisplayName)}\",\"{Escape(log.Action)}\",\"{Escape(log.TargetType)}\",\"{Escape(log.TargetId)}\",\"{Escape(log.Summary)}\",\"{Escape(log.MachineName)}\",\"{Escape(log.Environment)}\",\"{log.HasException}\"");
                }

                File.WriteAllText(sfd.FileName, sb.ToString(), Encoding.UTF8);
            }
            catch (Exception ex)
            {
                ErrorMessage = $"Failed to export CSV: {ex.Message}";
            }
        }
    }

    private static string Escape(string? text) => text?.Replace("\"", "\"\"") ?? "";
}
