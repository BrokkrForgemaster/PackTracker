using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using PackTracker.Application.Admin.DTOs;
using PackTracker.Presentation.ViewModels.Admin;

namespace PackTracker.Presentation.Views.Admin;

public partial class AdminAwardRibbonWindow : Window
{
    private readonly AdminMedalsViewModel _viewModel;
    private readonly IReadOnlyList<AdminMedalDefinitionDto> _allRibbons;
    private readonly List<MemberItem> _allMembers;
    private AdminMedalDefinitionDto? _selectedRibbon;

    private sealed record MemberItem(
        Guid ProfileId,
        string DisplayLabel,
        string SubLabel);

    public AdminAwardRibbonWindow(
        IReadOnlyList<AdminMemberListItemDto> members,
        IReadOnlyList<AdminMedalDefinitionDto> ribbons,
        AdminMedalsViewModel viewModel,
        Window owner)
    {
        InitializeComponent();

        Owner = owner;
        _viewModel = viewModel;
        _allRibbons = ribbons;

        _allMembers = members
            .Select(m => new MemberItem(
                m.ProfileId,
                m.DisplayName ?? m.Username,
                string.Join(" · ", new[] { m.DiscordRank, m.DiscordDivision }
                    .Where(s => !string.IsNullOrWhiteSpace(s)))))
            .OrderBy(m => m.DisplayLabel)
            .ToList();

        ApplyScreenSize();
        RefreshMemberList(string.Empty);

        RibbonPickerList.ItemsSource = _allRibbons;
    }

    private void ApplyScreenSize()
    {
        var workArea = SystemParameters.WorkArea;

        Width = Math.Clamp(workArea.Width * 0.60, 620, 1000);
        Height = Math.Clamp(workArea.Height * 0.70, 480, 860);
    }

    private void RefreshMemberList(string filter)
    {
        var filtered = string.IsNullOrWhiteSpace(filter)
            ? _allMembers
            : _allMembers
                .Where(m =>
                    m.DisplayLabel.Contains(filter, StringComparison.OrdinalIgnoreCase) ||
                    m.SubLabel.Contains(filter, StringComparison.OrdinalIgnoreCase))
                .ToList();

        MemberListBox.ItemsSource = filtered;
    }

    private void MemberSearch_TextChanged(object sender, TextChangedEventArgs e)
    {
        RefreshMemberList(MemberSearch.Text);
    }

    private void MemberListBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        var selectedMembers = MemberListBox.SelectedItems
            .Cast<MemberItem>()
            .ToList();

        SelectedMemberLabel.Text = selectedMembers.Count switch
        {
            0 => "None selected",
            1 => selectedMembers[0].DisplayLabel,
            _ => $"{selectedMembers.Count} members selected"
        };

        ValidationMessage.Visibility = Visibility.Collapsed;
    }

    private void RibbonPicker_Click(object sender, MouseButtonEventArgs e)
    {
        if (sender is not FrameworkElement fe ||
            fe.DataContext is not AdminMedalDefinitionDto ribbon)
        {
            return;
        }

        _selectedRibbon = ribbon;
        SelectedRibbonLabel.Text = ribbon.Name;
        ValidationMessage.Visibility = Visibility.Collapsed;
    }

    private async void Submit_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            ValidationMessage.Visibility = Visibility.Collapsed;

            if (_selectedRibbon is null)
            {
                ValidationMessage.Text = "Select a ribbon.";
                ValidationMessage.Visibility = Visibility.Visible;
                return;
            }

            var selectedMembers = MemberListBox.SelectedItems
                .Cast<MemberItem>()
                .ToList();

            if (selectedMembers.Count == 0)
            {
                ValidationMessage.Text = "Select at least one member.";
                ValidationMessage.Visibility = Visibility.Visible;
                return;
            }

            var profileIds = selectedMembers
                .Select(m => m.ProfileId)
                .ToList();

            var request = new AwardRibbonRequestDto(
                _selectedRibbon.Name,
                _selectedRibbon.Description,
                _selectedRibbon.ImagePath,
                profileIds,
                CitationBox.Text);

            await _viewModel.AwardRibbonAsync(request);

            DialogResult = true;
            Close();
        }
        catch (Exception ex)
        {
            ValidationMessage.Text = ex.Message;
            ValidationMessage.Visibility = Visibility.Visible;
        }
    }

    private void Close_Click(object sender, RoutedEventArgs e)
    {
        Close();
    }

    private void Border_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        if (e.ButtonState == MouseButtonState.Pressed)
            DragMove();
    }
}