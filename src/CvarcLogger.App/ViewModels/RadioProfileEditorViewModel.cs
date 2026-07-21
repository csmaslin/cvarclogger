using System.Collections.ObjectModel;
using System.Linq;
using CommunityToolkit.Mvvm.ComponentModel;
using CvarcLogger.Core.Rig;

namespace CvarcLogger.App.ViewModels;

/// <summary>Editable view of one RadioProfile for the Settings window. Numeric fields are edited as
/// text so TextBox binding doesn't need a converter; ApplyTo parses them back on Save. SelectedRig and
/// HamlibModelId are kept in sync in both directions: picking a radio from the dropdown fills in its
/// numeric ID, and typing an ID directly (for a rig not in the bundled list) re-selects a matching
/// dropdown entry if one shows up later.</summary>
public partial class RadioProfileEditorViewModel : ObservableObject
{
    public string Name { get; }

    /// <summary>Positional label ("Radio 1".."Radio 4") shown as the box heading in Settings — distinct
    /// from Name, which stays the profile's stored identity (used by the Active radio dropdown and by
    /// MigrateRadioProfiles to match profiles across app versions).</summary>
    public string SlotLabel { get; }

    /// <summary>Shared with the other radio slots and the parent SettingsViewModel — populated
    /// asynchronously after construction, once `rigctld --list` finishes.</summary>
    public ObservableCollection<HamlibRigInfo> AvailableRigs { get; }

    /// <summary>Shared with the other radio slots and the parent SettingsViewModel — the COM ports
    /// Windows currently enumerates, refreshed each time the Settings window opens.</summary>
    public ObservableCollection<string> AvailableComPorts { get; }

    [ObservableProperty] private string hamlibModelId;
    [ObservableProperty] private string comPort;
    [ObservableProperty] private string baudRate;
    [ObservableProperty] private string maxPowerWatts;
    [ObservableProperty] private HamlibRigInfo? selectedRig;

    private bool _syncing;

    public RadioProfileEditorViewModel(
        RadioProfile profile,
        string slotLabel,
        ObservableCollection<HamlibRigInfo> availableRigs,
        ObservableCollection<string> availableComPorts)
    {
        Name = profile.Name;
        SlotLabel = slotLabel;
        AvailableRigs = availableRigs;
        AvailableComPorts = availableComPorts;
        hamlibModelId = profile.HamlibModelId.ToString();
        comPort = profile.ComPort;
        baudRate = profile.BaudRate.ToString();
        maxPowerWatts = profile.MaxPowerWatts.ToString();
        selectedRig = FindRig(hamlibModelId);
    }

    /// <summary>Call after AvailableRigs has been populated (or refreshed) to (re)match the current
    /// HamlibModelId against it.</summary>
    public void RefreshSelectedRig() => SelectedRig = FindRig(HamlibModelId);

    private HamlibRigInfo? FindRig(string modelId) =>
        int.TryParse(modelId, out var id) ? AvailableRigs.FirstOrDefault(r => r.Id == id) : null;

    partial void OnSelectedRigChanged(HamlibRigInfo? value)
    {
        if (_syncing || value is null) return;
        _syncing = true;
        HamlibModelId = value.Id.ToString();
        if (value.Id == 0)
        {
            // -none- selected -- reset the rest of the slot to RadioProfile's own unconfigured
            // defaults too, so it reads as genuinely blank rather than just missing a model ID.
            // ApplyTo() below deliberately keeps the previous ComPort/BaudRate when those fields
            // are blank or unparseable (so a moment of empty text while retyping doesn't silently
            // wipe a saved value), so an empty string here wouldn't actually persist on Save.
            ComPort = "COM1";
            BaudRate = "38400";
            MaxPowerWatts = "100";
        }
        _syncing = false;
    }

    partial void OnHamlibModelIdChanged(string value)
    {
        if (_syncing) return;
        _syncing = true;
        SelectedRig = FindRig(value);
        _syncing = false;
    }

    public void ApplyTo(RadioProfile profile)
    {
        profile.HamlibModelId = int.TryParse(HamlibModelId, out var id) ? id : 0;
        profile.ComPort = string.IsNullOrWhiteSpace(ComPort) ? profile.ComPort : ComPort;
        profile.BaudRate = int.TryParse(BaudRate, out var baud) ? baud : profile.BaudRate;
        profile.MaxPowerWatts = int.TryParse(MaxPowerWatts, out var maxWatts) ? maxWatts : profile.MaxPowerWatts;
    }
}
