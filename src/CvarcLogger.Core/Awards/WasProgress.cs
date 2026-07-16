namespace CvarcLogger.Core.Awards;

public record WasStateStatus(string State, bool Worked, bool Confirmed);

public record WasProgress(int WorkedCount, int ConfirmedCount, IReadOnlyList<WasStateStatus> States);
