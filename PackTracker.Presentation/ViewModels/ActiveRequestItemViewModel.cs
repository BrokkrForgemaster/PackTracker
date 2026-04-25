using System;
using System.Windows.Input;
using PackTracker.Application.DTOs.Dashboard;
using PackTracker.Presentation.Commands;

namespace PackTracker.Presentation.ViewModels;

public class ActiveRequestItemViewModel : ViewModelBase
{
    private int _newClaimCount;

    public ActiveRequestItemViewModel(ActiveRequestDto dto, Action<ActiveRequestItemViewModel> onDismiss)
    {
        Dto = dto;
        DismissCommand = new RelayCommand(() =>
        {
            ClearNewClaims();
            onDismiss(this);
        });
    }

    public ActiveRequestDto Dto { get; }

    // Forwarded so existing XAML bindings require no changes
    public Guid Id => Dto.Id;
    public string Title => Dto.Title;
    public string RequestType => Dto.RequestType;
    public string Status => Dto.Status;
    public string Priority => Dto.Priority;
    public bool IsPinned => Dto.IsPinned;
    public bool IsRequestedByCurrentUser => Dto.IsRequestedByCurrentUser;
    public bool IsAssignedToCurrentUser => Dto.IsAssignedToCurrentUser;
    public bool IsAvailableToClaim => Dto.IsAvailableToClaim;
    public string RequesterDisplayName => Dto.RequesterDisplayName;
    public string? AssigneeDisplayName => Dto.AssigneeDisplayName;
    public DateTime CreatedAt => Dto.CreatedAt;
    public int MaxClaims => Dto.MaxClaims;
    public int ClaimCount => Dto.ClaimCount;

    public int NewClaimCount
    {
        get => _newClaimCount;
        private set
        {
            if (SetProperty(ref _newClaimCount, value))
                OnPropertyChanged(nameof(HasNewClaims));
        }
    }

    public bool HasNewClaims => _newClaimCount > 0;

    public ICommand DismissCommand { get; }

    public void IncrementNewClaim() => NewClaimCount++;

    public void SetNewClaimCount(int count) => NewClaimCount = count;

    public void ClearNewClaims() => NewClaimCount = 0;
}
