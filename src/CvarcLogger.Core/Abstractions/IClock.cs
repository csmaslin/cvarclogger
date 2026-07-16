namespace CvarcLogger.Core.Abstractions;

/// <summary>Testable indirection over the current time.</summary>
public interface IClock
{
    DateTime UtcNow { get; }
}

public class SystemClock : IClock
{
    public DateTime UtcNow => DateTime.UtcNow;
}
