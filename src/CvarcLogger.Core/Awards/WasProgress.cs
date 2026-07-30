namespace CvarcLogger.Core.Awards;

/// <summary>Phone/Cw/Digital are true when at least one WAS-eligible QSO with that state was logged in
/// that mode category, independent of Worked/Confirmed (which track QSL confirmation, not mode) -- same
/// semantics as DxccEntityModeRow's per-entity mode breakdown.</summary>
public record WasStateStatus(string State, bool Worked, bool Confirmed, bool Phone, bool Cw, bool Digital);

public record WasProgress(int WorkedCount, int ConfirmedCount, IReadOnlyList<WasStateStatus> States);
