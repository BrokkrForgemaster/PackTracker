using System.Windows;
using PackTracker.Application.Admin.DTOs;

namespace PackTracker.Presentation.Views.Admin;

public partial class AuditLogDetailWindow : Window
{
    public AdminAuditLogDetailDto Log { get; }
    public bool HasException => !string.IsNullOrEmpty(Log.Exception);

    public AuditLogDetailWindow(AdminAuditLogDetailDto log)
    {
        InitializeComponent();
        Log = log;
        DataContext = this;
    }

    private void Close_Click(object sender, RoutedEventArgs e)
    {
        Close();
    }
}
