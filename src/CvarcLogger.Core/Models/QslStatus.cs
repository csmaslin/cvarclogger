namespace CvarcLogger.Core.Models;

/// <summary>ADIF QSL status codes, shared by QSL_SENT/QSL_RCVD and their LoTW counterparts.</summary>
public enum QslStatus
{
    NotSent,   // N
    Sent,      // Y (sent) / Yes (received, confirmed)
    Requested, // R
    Queued,    // Q (SENT only)
    Verified,  // V (RCVD only)
    Ignore     // I
}
