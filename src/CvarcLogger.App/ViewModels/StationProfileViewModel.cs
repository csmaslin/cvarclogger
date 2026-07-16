using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using CvarcLogger.App.Services;
using CvarcLogger.Core.Abstractions;
using CvarcLogger.Core.Models;

namespace CvarcLogger.App.ViewModels;

public partial class StationProfileViewModel : ObservableObject
{
    private readonly IStationProfileRepository _repository;
    private readonly DialogService _dialogService;

    public ObservableCollection<StationProfile> Profiles { get; } = new();

    [ObservableProperty] private StationProfile? selectedProfile;
    [ObservableProperty] private string editCallsign = string.Empty;
    [ObservableProperty] private string? editOperatorCallsign;
    [ObservableProperty] private string? editMyGridSquare;
    [ObservableProperty] private string? editMyState;
    [ObservableProperty] private string? editMyCounty;
    [ObservableProperty] private bool editIsDefault;

    public StationProfileViewModel(IStationProfileRepository repository, DialogService dialogService)
    {
        _repository = repository;
        _dialogService = dialogService;
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
        EditIsDefault = Profiles.Count == 0;
    }

    partial void OnSelectedProfileChanged(StationProfile? value)
    {
        if (value is null) return;
        EditCallsign = value.Callsign;
        EditOperatorCallsign = value.OperatorCallsign;
        EditMyGridSquare = value.MyGridSquare;
        EditMyState = value.MyState;
        EditMyCounty = value.MyCounty;
        EditIsDefault = value.IsDefault;
    }

    [RelayCommand]
    private async Task SaveAsync()
    {
        if (string.IsNullOrWhiteSpace(EditCallsign))
        {
            _dialogService.ShowError("Enter a callsign for this station profile.");
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
            SelectedProfile.IsDefault = EditIsDefault;
            await _repository.UpdateAsync(SelectedProfile);
        }

        await LoadAsync();
    }

    [RelayCommand]
    private async Task DeleteAsync()
    {
        if (SelectedProfile is null) return;
        if (!_dialogService.Confirm($"Delete station profile {SelectedProfile.Callsign}?")) return;
        await _repository.DeleteAsync(SelectedProfile.Id);
        SelectedProfile = null;
        await LoadAsync();
    }
}
