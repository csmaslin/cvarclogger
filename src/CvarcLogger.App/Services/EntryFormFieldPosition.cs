namespace CvarcLogger.App.Services;

/// <summary>Where one entry-form field sits within its Log Entry Mode's layout -- Row is which row it
/// renders in (top to bottom), Position is left-to-right order within that row (1-based, capped at 5
/// per the row-width limit established for the Normal-mode form). Written by the drag-and-drop layout
/// editor (see SettingsService.SaveEntryFormFieldPositions), one independent map per mode.</summary>
public record EntryFormFieldPosition(int Row, int Position);
