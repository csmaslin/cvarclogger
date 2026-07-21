using System.Collections.ObjectModel;
using System.Globalization;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using CvarcLogger.Core.Abstractions;
using CvarcLogger.Core.Models;

namespace CvarcCellLog.ViewModels;

/// <summary>Adapted from the WPF app's StationProfileViewModel: same list+edit-form shape (select a
/// profile to load it into the edit fields, or New Profile for a blank one), same validation rules. Uses
/// this app's established ErrorMessage/HasErrorMessage inline-error pattern (see QsoEntryViewModel)
/// instead of the WPF app's DialogService, and a page-level DisplayAlert for delete confirmation (see
/// QsoEditPage) instead of DialogService.Confirm.</summary>
public partial class StationProfileViewModel : ObservableObject
{
    private readonly IStationProfileRepository _repository;

    public ObservableCollection<StationProfile> Profiles { get; } = new();

    [ObservableProperty] private StationProfile? selectedProfile;
    [ObservableProperty] private string editCallsign = string.Empty;
    [ObservableProperty] private string? editOperatorCallsign;
    [ObservableProperty] private string? editMyGridSquare;
    [ObservableProperty] private string? editMyState;
    [ObservableProperty] private string? editMyCounty;
    [ObservableProperty] private string? editQth;
    [ObservableProperty] private string? editOp;
    [ObservableProperty] private string editUtcOffsetHours = "0";
    [ObservableProperty] private bool editObservesDaylightSavingTime;
    [ObservableProperty] private bool editIsDefault;
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasErrorMessage))]
    private string? errorMessage;

    public bool HasErrorMessage => !string.IsNullOrEmpty(ErrorMessage);

    public StationProfileViewModel(IStationProfileRepository repository)
    {
        _repository = repository;
    }

    [RelayCommand]
    public async Task LoadAsync()
    {
        var profiles = await _repository.GetAllAsync();
        Profiles.Clear();
        foreach (var p in profiles) Profiles.Add(p);
    }

    [RelayCommand]
    private void NewProfile()
    {
        SelectedProfile = null;
        EditCallsign = string.Empty;
        EditOperatorCallsign = null;
        EditMyGridSquare = null;
        EditMyState = null;
        EditMyCounty = null;
        EditQth = null;
        EditOp = null;
        EditUtcOffsetHours = "0";
        EditObservesDaylightSavingTime = false;
        EditIsDefault = Profiles.Count == 0;
        ErrorMessage = null;
    }

    partial void OnSelectedProfileChanged(StationProfile? value)
    {
        if (value is null) return;
        EditCallsign = value.Callsign;
        EditOperatorCallsign = value.OperatorCallsign;
        EditMyGridSquare = value.MyGridSquare;
        EditMyState = value.MyState;
        EditMyCounty = value.MyCounty;
        EditQth = value.Qth;
        EditOp = value.Op;
        EditUtcOffsetHours = value.UtcOffsetHours.ToString(CultureInfo.InvariantCulture);
        EditObservesDaylightSavingTime = value.ObservesDaylightSavingTime;
        EditIsDefault = value.IsDefault;
        ErrorMessage = null;
    }

    [RelayCommand]
    private async Task SaveAsync()
    {
        ErrorMessage = null;

        if (string.IsNullOrWhiteSpace(EditCallsign))
        {
            ErrorMessage = "Enter a callsign for this station profile.";
            return;
        }

        if (!decimal.TryParse(EditUtcOffsetHours, NumberStyles.Number, CultureInfo.InvariantCulture, out var utcOffsetHours))
        {
            ErrorMessage = "Enter a valid UTC offset (e.g. -5 or +5.5) for this station profile.";
            return;
        }

        if (SelectedProfile is null)
        {
            var profile = new StationProfile
            {
                Callsign = EditCallsign.Trim().ToUpperInvariant(),
                OperatorCallsign = EditOperatorCallsign,
                MyGridSquare = EditMyGridSquare,
                MyState = EditMyState,
                MyCounty = EditMyCounty,
                Qth = EditQth,
                Op = EditOp,
                UtcOffsetHours = utcOffsetHours,
                ObservesDaylightSavingTime = EditObservesDaylightSavingTime,
                IsDefault = EditIsDefault,
            };
            await _repository.AddAsync(profile);
        }
        else
        {
            SelectedProfile.Callsign = EditCallsign.Trim().ToUpperInvariant();
            SelectedProfile.OperatorCallsign = EditOperatorCallsign;
            SelectedProfile.MyGridSquare = EditMyGridSquare;
            SelectedProfile.MyState = EditMyState;
            SelectedProfile.MyCounty = EditMyCounty;
            SelectedProfile.Qth = EditQth;
            SelectedProfile.Op = EditOp;
            SelectedProfile.UtcOffsetHours = utcOffsetHours;
            SelectedProfile.ObservesDaylightSavingTime = EditObservesDaylightSavingTime;
            SelectedProfile.IsDefault = EditIsDefault;
            await _repository.UpdateAsync(SelectedProfile);
        }

        await LoadAsync();
    }

    [RelayCommand]
    private async Task DeleteAsync()
    {
        if (SelectedProfile is null) return;
        await _repository.DeleteAsync(SelectedProfile.Id);
        SelectedProfile = null;
        await LoadAsync();
    }
}
