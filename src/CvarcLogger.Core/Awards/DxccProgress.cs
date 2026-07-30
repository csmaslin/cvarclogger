namespace CvarcLogger.Core.Awards;

public record DxccEntityStatus(int EntityCode, string EntityName, bool Worked, bool Confirmed);

public record DxccProgress(int WorkedCount, int ConfirmedCount, IReadOnlyList<DxccEntityStatus> Entities);

/// <summary>Total QSO count logged on one band -- plain volume, independent of DXCC entity resolution or
/// confirmation status (unlike FiveBandDxccRow-style breakdowns, which count confirmed *entities*, not
/// QSOs). Ordered by QsoFieldOptions.Bands' canonical band order; bands with zero QSOs are omitted.</summary>
public record BandQsoCount(string Band, int QsoCount);
