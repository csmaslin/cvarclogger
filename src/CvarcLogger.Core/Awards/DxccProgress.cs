namespace CvarcLogger.Core.Awards;

public record DxccEntityStatus(int EntityCode, string EntityName, bool Worked, bool Confirmed);

public record DxccProgress(int WorkedCount, int ConfirmedCount, IReadOnlyList<DxccEntityStatus> Entities);
