using System.IO;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using CvarcLogger.App.Services;
using CvarcLogger.Core.Abstractions;
using CvarcLogger.Core.Adif;

namespace CvarcLogger.App.ViewModels;

public partial class ImportExportViewModel : ObservableObject
{
    private readonly IQsoRepository _qsoRepository;
    private readonly FilePickerService _filePicker;
    private readonly DialogService _dialogService;

    public event EventHandler? ImportCompleted;

    [ObservableProperty] private bool isBusy;
    [ObservableProperty] private string? lastResultMessage;

    public ImportExportViewModel(IQsoRepository qsoRepository, FilePickerService filePicker, DialogService dialogService)
    {
        _qsoRepository = qsoRepository;
        _filePicker = filePicker;
        _dialogService = dialogService;
    }

    [RelayCommand]
    private async Task ImportAsync()
    {
        var path = _filePicker.PickAdifFileToOpen();
        if (path is null) return;

        IsBusy = true;
        try
        {
            using var reader = new StreamReader(path);
            var records = AdifReader.ReadAll(reader);
            int imported = 0;
            foreach (var record in records)
            {
                var qso = AdifFieldMapper.ToQso(record);
                if (string.IsNullOrWhiteSpace(qso.Callsign)) continue;
                await _qsoRepository.AddAsync(qso);
                imported++;
            }

            LastResultMessage = $"Imported {imported} of {records.Count} record(s) from {Path.GetFileName(path)}.";
            _dialogService.ShowInfo(LastResultMessage);
            ImportCompleted?.Invoke(this, EventArgs.Empty);
        }
        catch (Exception ex)
        {
            _dialogService.ShowError($"Import failed: {ex.Message}");
        }
        finally
        {
            IsBusy = false;
        }
    }

    [RelayCommand]
    private async Task ExportAsync()
    {
        var path = _filePicker.PickAdifFileToSave($"cvarclogger-export-{DateTime.Now:yyyyMMdd}.adi");
        if (path is null) return;

        IsBusy = true;
        try
        {
            var qsos = await _qsoRepository.GetAllAsync();
            using var writer = new StreamWriter(path, append: false);
            AdifWriter.WriteAll(writer, qsos.Select(AdifFieldMapper.ToAdifRecord));
            LastResultMessage = $"Exported {qsos.Count} QSO(s) to {Path.GetFileName(path)}.";
            _dialogService.ShowInfo(LastResultMessage);
        }
        catch (Exception ex)
        {
            _dialogService.ShowError($"Export failed: {ex.Message}");
        }
        finally
        {
            IsBusy = false;
        }
    }
}
