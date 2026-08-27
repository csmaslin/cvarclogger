using Microsoft.Win32;

namespace CvarcLogger.App.Services;

/// <summary>Thin wrapper over Win32 file dialogs so ViewModels don't take a direct WPF dependency.</summary>
public class FilePickerService
{
    public string? PickAdifFileToOpen()
    {
        var dialog = new OpenFileDialog
        {
            Filter = "ADIF files (*.adi;*.adif)|*.adi;*.adif|All files (*.*)|*.*",
            Title = "Import ADIF Log"
        };
        return dialog.ShowDialog() == true ? dialog.FileName : null;
    }

    public string? PickAdifFileToSave(string suggestedFileName)
    {
        var dialog = new SaveFileDialog
        {
            Filter = "ADIF files (*.adi)|*.adi|All files (*.*)|*.*",
            FileName = suggestedFileName,
            Title = "Export ADIF Log"
        };
        return dialog.ShowDialog() == true ? dialog.FileName : null;
    }

    public string? PickNewDatabaseFileToCreate(string suggestedFileName)
    {
        var dialog = new SaveFileDialog
        {
            Filter = "CVARC Logger database (*.db)|*.db|All files (*.*)|*.*",
            FileName = suggestedFileName,
            InitialDirectory = AppContext.BaseDirectory,
            Title = "Create New Log"
        };
        return dialog.ShowDialog() == true ? dialog.FileName : null;
    }

    public string? PickBackupDatabaseFileToSave(string suggestedFileName)
    {
        var dialog = new SaveFileDialog
        {
            Filter = "CVARC Logger database (*.db)|*.db|All files (*.*)|*.*",
            FileName = suggestedFileName,
            InitialDirectory = AppContext.BaseDirectory,
            Title = "Save Log As"
        };
        return dialog.ShowDialog() == true ? dialog.FileName : null;
    }

    public string? PickCabrilloFileToOpen()
    {
        var dialog = new OpenFileDialog
        {
            Filter = "Cabrillo files (*.cbr;*.log)|*.cbr;*.log|All files (*.*)|*.*",
            Title = "Import Cabrillo Log"
        };
        return dialog.ShowDialog() == true ? dialog.FileName : null;
    }

    public string? PickCabrilloFileToSave(string suggestedFileName)
    {
        var dialog = new SaveFileDialog
        {
            Filter = "Cabrillo files (*.cbr)|*.cbr|Cabrillo log files (*.log)|*.log|All files (*.*)|*.*",
            FileName = suggestedFileName,
            Title = "Export Cabrillo Log"
        };
        return dialog.ShowDialog() == true ? dialog.FileName : null;
    }

    public string? PickExistingDatabaseFileToOpen()
    {
        var dialog = new OpenFileDialog
        {
            Filter = "CVARC Logger database (*.db)|*.db|All files (*.*)|*.*",
            Title = "Open Log"
        };
        return dialog.ShowDialog() == true ? dialog.FileName : null;
    }
}
