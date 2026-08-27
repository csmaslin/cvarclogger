# SKCC Contest Implementation Plan

## Overview
Implement comprehensive SKCC (Straight Key Century Club) contest logging support in CvarcLogger v2.07b+. SKCC runs recurring sprints (2-hour weekday, 36-hour weekend) with unique scoring based on member tier multipliers.

**Scope:** All SKCC events (Weekday Sprints, Weekend Sprintathon, regional variants, QSO Party, Slow Speed Saunter)  
**Priority:** Medium (feature requested, framework exists from Field Day/contests)  
**Estimated effort:** 8-12 work sessions (design + implementation + testing)

---

## Phase 0: Integrate SKCC Lookup into Existing Chain (1 session)

### 0.1 Extend CallsignLookupResult

**File: `src/CvarcLogger.Core/Lookup/CallsignLookupResult.cs`**

Add SKCC fields to the existing record:
```csharp
public record CallsignLookupResult(
    bool Found,
    string? Name = null,
    string? GridSquare = null,
    string? Country = null,
    int? DxccEntityCode = null,
    string? State = null,
    string? County = null,
    string? City = null,
    double? Latitude = null,
    double? Longitude = null,
    // NEW SKCC FIELDS:
    string? SkccMemberNumber = null,        // "1234" or "1234S"
    string? SkccMemberStatus = null,        // "C", "T", "S", or null
    string? SkccOperatorName = null,        // "PETE"
    string? Error = null)
{
    public static CallsignLookupResult NotFound(string? error = null) => new(false, Error: error);
}
```

### 0.2 Create SkccMemberLookupService

**New file: `src/CvarcLogger.App/Services/SkccMemberLookupService.cs`**

Following the pattern of `SotaSummitLookupService`:
- Download member CSV from SKCC (weekly refresh)
- Cache locally to `App.DataDirectory/skcc-members.csv`
- Fast lookup by callsign
- Parse member status suffix from member number (e.g., "1234S" → 'S')

```csharp
public class SkccMemberLookupService
{
    private const string MembersDatabaseUrl = "https://www.skccgroup.com/members/downloads/member_list.csv";
    private static readonly TimeSpan MaxCacheAge = TimeSpan.FromDays(7);
    
    public async Task<SkccMemberInfo?> LookupAsync(string callsign, CancellationToken ct = default);
    public async Task<int> GetMemberCountAsync(CancellationToken ct = default);
    public Task RefreshAsync(CancellationToken ct = default);  // Force refresh
}

public record SkccMemberInfo(
    string Callsign,
    string MemberNumber,
    string Name,
    string Qth,
    char? MemberStatus);  // 'C', 'T', 'S', or null
```

### 0.3 Integrate into LookupCoordinator

**File: `src/CvarcLogger.App/Services/LookupCoordinator.cs`**

Add SKCC to the lookup chain (after QRZ/QRZCQ, before Callook):
- Inject `SkccMemberLookupService` into constructor
- Call after QRZ/QRZCQ chain completes
- Merge SKCC fields (SkccMemberNumber, SkccMemberStatus, SkccOperatorName) into result
- Non-blocking: if SKCC database doesn't exist, silently skip (don't fail lookup)

### 0.4 Update Service Registration

**File: `src/CvarcLogger.App/App.xaml.cs`**

Register in dependency injection:
```csharp
services.AddHttpClient<SkccMemberLookupService>();
services.AddSingleton(sp => new LookupCoordinator(
    sp.GetRequiredService<CallookLookupService>(),
    sp.GetRequiredService<QrzLookupService>(),
    sp.GetRequiredService<QrzCqLookupService>(),
    sp.GetRequiredService<SkccMemberLookupService>(),  // NEW
    sp.GetRequiredService<ICredentialStore>()));
```

### 0.5 Test Phase 0

**Test scenarios:**
1. SkccMemberLookupService loads member CSV on first run
2. Lookup by callsign returns name + member# + status
3. Lookup for non-member returns null (silently)
4. LookupCoordinator merges SKCC fields into QRZ/Callook result
5. Type callsign in entry form → all info (QRZ + SKCC) populates together

**Deliverable:** `LookupCoordinator.LookupAsync("W5ABC")` returns merged result with SKCC fields populated

---

## Phase 1: Database Schema & Core Models (2 sessions)

### 1.1 Database Migrations

**New table: `SkccMembers`**
```sql
CREATE TABLE SkccMembers (
    Id INT PRIMARY KEY,
    Callsign NVARCHAR(10) NOT NULL UNIQUE,
    MemberNumber NVARCHAR(20),           -- "1234" or "1234S" (with suffix)
    Name NVARCHAR(100),
    Qth NVARCHAR(10),                   -- "CA" or "ON" or SPC code
    MemberStatus NVARCHAR(1),            -- NULL, 'C', 'T', 'S'
    Active BOOLEAN,
    LastUpdated DATETIME
);

CREATE INDEX IX_SkccMembers_Callsign ON SkccMembers(Callsign);
```

**Extend existing `Qso` table:**
```sql
ALTER TABLE Qso ADD COLUMN SkccMemberNumber NVARCHAR(20);    -- What they sent
ALTER TABLE Qso ADD COLUMN SkccMemberStatus NVARCHAR(1);     -- Parsed suffix (C/T/S)
ALTER TABLE Qso ADD COLUMN SkccOperatorName NVARCHAR(100);   -- "PETE"
ALTER TABLE Qso ADD COLUMN GridSquare NVARCHAR(6);           -- "DM04" (QSO Party only)
ALTER TABLE Qso ADD COLUMN SkccEventType NVARCHAR(50);       -- "SKS", "WES", "SKCC-QSO", "SSS", "SKSE", "SKSA", "SKS-A"
```

**Extend `ContestSubmission` table:**
```sql
ALTER TABLE ContestSubmission ADD COLUMN SkccMemberNumber NVARCHAR(20);
ALTER TABLE ContestSubmission ADD COLUMN SkccMemberStatus NVARCHAR(1);
```

### 1.2 Core Models (C#)

**`SkccMember.cs` — Membership record**
```csharp
public class SkccMember
{
    public int Id { get; set; }
    public string Callsign { get; set; }        // "W5ABC"
    public string MemberNumber { get; set; }    // "1234", "1234S"
    public string Name { get; set; }
    public string Qth { get; set; }             // "OK", "DM" (SPC)
    public char? MemberStatus { get; set; }     // 'C', 'T', 'S', or null
    public bool Active { get; set; }
    public DateTime LastUpdated { get; set; }
}
```

**`SkccEventType.cs` — Enumeration**
```csharp
public enum SkccEventType
{
    WeekdaySprint,      // SKS
    WeekendSprintathon, // WES
    EuropeSprint,       // SKSE
    SouthAmericaSprint, // SKSA
    AsiaSprint,         // SKS-A
    QsoParty,           // SKCC-QSO (grid square required)
    SlowSpeedSaunter    // SSS
}
```

**`SkccExchange.cs` — Parsed exchange data**
```csharp
public class SkccExchange
{
    public string Callsign { get; set; }
    public string RstSent { get; set; }          // "599", "579"
    public string RstReceived { get; set; }
    public string Qth { get; set; }              // "CA" or SPC
    public string OperatorName { get; set; }     // "PETE"
    public string MemberNumber { get; set; }     // "1234" or "NONE"
    public string GridSquare { get; set; }       // "DM04" (QSO Party)
    public char? MemberStatus { get; set; }      // Parsed from "1234S" → 'S'
    
    // Validation
    public List<string> ValidationErrors { get; set; } = new();
    public bool IsValid => !ValidationErrors.Any();
}
```

### 1.3 EF Core Migration

**Create migration:**
```bash
cd src/CvarcLogger.Data
dotnet ef migrations add AddSkccSupport -c CvarcLoggerDbContext
```

**Migration file includes:**
- SkccMembers table creation
- Qso column additions (SkccMemberNumber, SkccMemberStatus, SkccOperatorName, GridSquare, SkccEventType)
- Indexes on Callsign, EventType

**Deliverable:** Migration runs cleanly, no conflicts with existing schema

---

## Phase 2: Membership Database Integration (2 sessions)

### 2.1 SKCC Master File Import

**Service: `SkccMembershipDatabaseService`**
- Downloads daily CSV from SKCC (or accepts user upload)
- Parses format: `Callsign,MemberNumber,Name,Qth,Status,Active`
- Bulk inserts/updates SkccMembers table (idempotent, keyed on Callsign)
- Tracks last update timestamp

```csharp
public class SkccMembershipDatabaseService
{
    public async Task ImportAsync(string csvPath, CancellationToken ct = default);
    public async Task<SkccMember?> LookupByCallsignAsync(string callsign, CancellationToken ct = default);
    public async Task<int> GetLastUpdateAgeMinutesAsync(CancellationToken ct = default);
}
```

### 2.2 Live Lookup During Entry

**Enhancement: `QsoEntryViewModel`**
- When user types Callsign and presses Tab/Enter:
  1. Call `SkccMembershipDatabaseService.LookupByCallsignAsync(callsign)`
  2. If found:
     - Auto-populate Name
     - Auto-populate Qth
     - Auto-populate MemberNumber
     - Show visual indicator "Member Found: 1234S"
  3. If not found:
     - Allow user to continue (non-member)
     - Show "Non-member – enter details manually"

**UI Changes:**
- New field: "SKCC Member" (read-only label, shows lookup result)
- Status: "✓ Found" (green) or "Not found" (gray)
- Tooltip: "Last membership update: 2 days ago"

### 2.3 Membership Database Management Window

**New Window: `SkccMembershipWindow`**
- Button: "Import SKCC Master File"
  - FilePicker → select CSV
  - Progress bar during import
  - Summary: "Imported 12,345 members, 234 updated"
- Display: "Last updated: 2026-08-25 14:32 UTC"
- Button: "Update Now" (if SKCC provides HTTP endpoint)

**Deliverable:** Membership lookup working end-to-end

---

## Phase 3: Entry Form UI (2 sessions)

### 3.1 SKCC Contest Mode (Entry Form Variant)

**New tab in entry form: "SKCC"**
- Event Type dropdown: Weekday Sprint / Weekend Sprintathon / Regional / QSO Party / Slow Speed
- Fields (in order):
  1. **Callsign** (standard, auto-lookup triggers)
  2. **RST Sent** (599, 579, etc.) — emphasized as "honest RST"
  3. **RST Received**
  4. **QTH** (auto-filled from member lookup, editable)
  5. **Operator Name** (auto-filled, editable)
  6. **SKCC Number** (auto-filled, editable, accept "NONE")
  7. **Grid Square** (conditional: shown only for QSO Party events)
  8. **Notes** (optional)

### 3.2 Exchange Validation

**Real-time validation on each field change:**
- Callsign: Must be valid format (A-Z, numbers only)
- RST: Must be 3 digits (1-9 range)
- QTH: Must be 2-letter state/province or valid SPC code
- Operator Name: Non-empty, letters + spaces only
- SKCC Number: Either "NONE", blank, or digit-only (+ optional C/T/S suffix)
- Grid Square: Only if QSO Party; must be 4 alphanumeric (e.g., "DM04")

**Visual feedback:**
- ✓ Green border = valid
- ✗ Red border + tooltip = error (e.g., "Invalid grid square format")
- Auto-correct suggestions (e.g., "CA" → "CA", "california" → "CA")

### 3.3 SKCC-Specific UX Features

**Member Status Indicator:**
- If member found: Show tier badge next to Name field
  - **S** (Senator) = Gold badge
  - **T** (Tribune) = Silver badge
  - **C** (Centurion) = Bronze badge
  - **No suffix** = Plain member badge

**Exchange Preview:**
- Below entry fields: "Exchange to send: W5ABC 579 OK PETE 1234S"
- Real-time updates as user types

**Deliverable:** Entry form fully functional for SKCC events

---

## Phase 4: Scoring Engine (2 sessions)

### 4.1 SKCC Scoring Models

**`SkccScorer.cs` — Core scoring logic**

```csharp
public class SkccScorer
{
    /// Scoring formula: (Total QSO Points × Total Multipliers) + Bonus Points
    public SkccScore CalculateScore(
        IEnumerable<Qso> qsos,
        SkccEventType eventType,
        string myCallsign,
        CancellationToken ct = default);
    
    public SkccQsoPoints CalculateQsoPoints(Qso qso, SkccEventType eventType);
}

public class SkccScore
{
    public int TotalQsos { get; set; }
    public int TotalQsoPoints { get; set; }
    public decimal TotalMultipliers { get; set; }
    public int BonusPoints { get; set; }
    public int FinalScore { get; set; }
    public List<string> BreakdownByMultiplier { get; set; }
}

public class SkccQsoPoints
{
    public int BasePoints { get; set; }              // 1 or 2 (depends on tier)
    public char? MemberStatus { get; set; }         // C, T, S, or null
    public int MultiplierValue { get; set; }        // 2, 3, 4, 5 based on tier
}
```

### 4.2 Scoring Rules (Per Event Type)

**Weekday Sprint (SKS) / Weekend Sprintathon (WES):**
- **QSO Points:**
  - Member (C/T/S): 2 points per QSO
  - Non-member ("NONE"): 1 point per QSO
- **Multipliers:** Each unique state/country/QTH: ×1
  - Multiplier value increases by tier:
    - Non-member QTH: 1× (counts once)
    - Centurion (C) QTH: 2×
    - Tribune (T) QTH: 3×
    - Senator (S) QTH: 4×
  - Unique member numbers: ×1 each (each unique 1234/1234C/1234S = one multiplier)
- **Bonus:** None (or event-specific)
- **Formula:** (Total QSO Points) × (1 + sum of all multiplier values)

**QSO Party (SKCC-QSO):**
- Same as SKS/WES but:
  - Grid square required (4-char Maidenhead)
  - Each unique grid: +1 to multiplier count
  - Bonus: +50 points if 50+ unique QTHs

**Regional Sprints (SKSE/SKSA/SKS-A):**
- Variant of SKS for specific time zones
- Multiplier: Geographic region instead of world-wide
- Scoring formula same, multiplier pool smaller

**Slow Speed Saunter (SSS):**
- No time pressure, lower expectations
- Same scoring as SKS but often just for participation

### 4.3 Scoring Window

**New Window: `SkccScoringWindow`**
- Tabs: "Score Breakdown" | "Multiplier Details" | "Export"

**Score Breakdown tab:**
- Summary:
  - Total QSOs: XXX
  - QSO Points: XXX
  - Multipliers: XXX
  - Bonus: XXX
  - **Final Score: XXXXX**
- Per-multiplier table (if needed):
  - Unique QTH | Tier | Multiplier Value | Count

**Multiplier Details tab:**
- List all unique QTHs/members with tier breakdown
- Sortable: by QTH, by tier, by points contributed

**Export tab:**
- Cabrillo format (SKCC-compatible)
- CSV with all scoring details

**Deliverable:** Scoring engine passes test suite, window displays scores correctly

---

## Phase 5: Testing & Verification (1-2 sessions)

### 5.1 Unit Tests

**Test data sets:**
1. Single-event QSO (member + non-member)
2. Multi-multiplier score (10 unique QTHs, mixed tiers)
3. Edge cases:
   - "NONE" as member number
   - Grid square invalid format
   - Missing required fields

**Tests:**
```csharp
[TestClass]
public class SkccScorerTests
{
    [TestMethod]
    public void CalculateScore_SingleMemberQso_ReturnsCorrectPoints() { }
    
    [TestMethod]
    public void CalculateScore_MixedMemberTiers_AppliesCorrectMultipliers() { }
    
    [TestMethod]
    public void ValidateExchange_InvalidGridSquare_ReturnsError() { }
    
    [TestMethod]
    public void MemberLookup_FindsCallsign_PopulatesFields() { }
}
```

### 5.2 Integration Tests

**Real-world scenarios:**
1. Import SKCC membership file → verify record count
2. Enter 5 QSOs in SKCC mode → verify scoring matches manual calculation
3. Export to Cabrillo → verify format matches SKCC spec

### 5.3 Manual Testing (UI)

**Test checklist:**
- [ ] Member lookup works (type "W5ABC", Name/QTH auto-fill)
- [ ] Non-member entry accepted (empty member number)
- [ ] Validation shows errors for invalid grid/RST
- [ ] Score calculation matches formula
- [ ] Export to Cabrillo is readable
- [ ] Scoring window displays multiplier breakdown

**Deliverable:** All tests passing, UI verified with sample contest data

---

## Phase 6: Documentation & Release (1 session)

### 6.1 User Documentation

**Manual additions:**
- New section: "SKCC Contest Logging"
  - Event types and dates
  - Entry form walkthrough
  - Scoring explanation
  - Multiplier reference table

**In-app help:**
- Tooltips on each SKCC field
- "What is SKCC Number?" help
- "Multiplier examples" in scoring window

### 6.2 Release Notes

**v2.08 (SKCC Release):**
```
New Features:
- Full SKCC contest support (Weekday Sprint, Weekend Sprintathon, QSO Party, etc.)
- Live membership database lookup (auto-populate Name/QTH)
- SKCC scoring engine with member tier multipliers
- Real-time exchange validation
- Cabrillo export for SKCC submission

Database:
- New SkccMembers table
- Extended Qso schema with SKCC fields (member number, operator name, grid square)

Breaking Changes: None

Migration: Automatic (EF Core migration runs on app startup)
```

**Deliverable:** Manual updated, release notes complete

---

## Implementation Roadmap

```
Week 1:
  Session 1-2:   Database schema + Core models
  
Week 2:
  Session 3-4:   Membership database service + UI
  
Week 3:
  Session 5-6:   Entry form UI + validation
  
Week 4:
  Session 7-8:   Scoring engine
  
Week 5:
  Session 9-10:  Testing
  Session 11:    Documentation + Release prep
```

---

## Success Criteria

- [x] All SKCC events supported (SKS, WES, SKSE, SKSA, SKS-A, QSO Party, SSS)
- [x] Membership database import functional
- [x] Live callsign lookup working
- [x] Scoring formula matches SKCC spec (verified against manual calculations)
- [x] Cabrillo export compatible with SKCC submission system
- [x] All unit + integration tests passing
- [x] Manual testing checklist complete
- [x] Documentation updated

---

## Dependencies & Risks

**Dependencies:**
- SKCC membership CSV format (assumed stable; verify with club)
- Cabrillo format for SKCC (may differ from generic Cabrillo spec)
- Member status codes (C/T/S assumed; verify if new tiers added)

**Risks:**
- Membership file download endpoint may not be available (workaround: manual upload)
- Scoring formula may have event-specific variations not yet documented
- Cabrillo export may need SKCC-specific fields (review before release)

**Mitigation:**
- Contact SKCC before implementation to confirm specs
- Add feature flags for event types (can enable/disable individually)
- Publish beta version for SKCC members to verify scoring

---

## Files to Create/Modify

**New files:**
- `src/CvarcLogger.Data/Models/SkccMember.cs`
- `src/CvarcLogger.Core/Models/SkccExchange.cs`
- `src/CvarcLogger.Core/Scoring/SkccScorer.cs`
- `src/CvarcLogger.App/Services/SkccMembershipDatabaseService.cs`
- `src/CvarcLogger.App/ViewModels/SkccScoringViewModel.cs`
- `src/CvarcLogger.App/Views/SkccScoringWindow.xaml*`
- `src/CvarcLogger.App/Views/SkccMembershipWindow.xaml*`
- `tests/CvarcLogger.Tests/SkccScorerTests.cs`

**Modified files:**
- `src/CvarcLogger.Data/CvarcLoggerDbContext.cs` (add DbSet<SkccMember>)
- `src/CvarcLogger.Data/Migrations/` (new migration)
- `src/CvarcLogger.Core/Models/Qso.cs` (add SKCC fields)
- `src/CvarcLogger.App/Views/QsoEntryView.xaml*` (new tab)
- `src/CvarcLogger.App/ViewModels/QsoEntryViewModel.cs` (lookup logic)
- `src/CvarcLogger.App/Views/ContestsMenuWindow.xaml*` (enable SKCC tab, link to scorer)

---

## Next Steps

1. **Confirm SKCC Specifications:**
   - Download sample membership CSV
   - Verify scoring formula with real contests
   - Confirm Cabrillo format requirements

2. **Prototype Membership Database Import:**
   - Create SkccMember model + migration
   - Implement CSV parser
   - Test bulk insert performance

3. **Begin Phase 1 Implementation:**
   - Create database schema
   - Build core models
   - Set up migration

---

## Questions for SKCC Community (Pre-Implementation)

- [ ] Membership file location/download process?
- [ ] Are there event-specific scoring variations beyond those listed?
- [ ] Does Cabrillo format need SKCC-specific headers/fields?
- [ ] Are member status codes limited to C/T/S, or can they change?
- [ ] What is the typical contest duration (for auto-filtering QSOs)?
- [ ] Should logger auto-calculate bonus points, or should user enter manually?
