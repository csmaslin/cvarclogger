# CvarcLogger v2.0 Backend API Mapping
## Prototype Integration Reference

---

## 1. QSO (Log Entry) Management

### Interface: `IQsoRepository`
**Location:** `CvarcLogger.Core/Abstractions/IQsoRepository.cs`
**Implementation:** `CvarcLogger.Data/Repositories/QsoRepository.cs`

**Methods:**
- `Task<List<Qso>> GetAllAsync()` - Load all QSO records from database
- `Task<Qso?> GetByIdAsync(int id)` - Fetch single QSO by ID
- `Task<Qso> AddAsync(Qso qso)` - Create new QSO (logging)
- `Task UpdateAsync(Qso qso)` - Update existing QSO
- `Task DeleteAsync(int id)` - Delete single QSO
- `Task<int> DeleteAllAsync()` - Clear entire log (with user confirmation)

**Prototype Connection:**
- Entry Form → AddAsync() - Save logged QSO
- Contact Log Grid → GetAllAsync() - Display all QSOs
- Grid Row Selection → GetByIdAsync() - Edit existing QSO
- Delete Button → DeleteAsync() - Remove QSO

**Data Model:** `Qso` (CvarcLogger.Core/Models/Qso.cs)
- QsoDateTimeOnUtc, QsoDateTimeOffUtc
- Callsign, Frequency, Band, Mode, SubMode
- RstSent, RstRcvd, Name, GridSquare, State, County, Country
- DxccEntityCode, CqZone, ItuZone, TxPower
- Comment, Operator, MyGridSquare, MyState, MyCounty, Qth
- SotaReference, PotaReference, SkccNumber
- QslSent, QslRcvd, QslVia, QslDate

---

## 2. Station Profile Management

### Interface: `IStationProfileRepository`
**Location:** `CvarcLogger.Core/Abstractions/IStationProfileRepository.cs`
**Implementation:** `CvarcLogger.Data/Repositories/StationProfileRepository.cs`

**Methods:**
- `Task<List<StationProfile>> GetAllAsync()` - Load all station profiles
- `Task<StationProfile?> GetDefaultAsync()` - Get active station
- `Task<StationProfile> AddAsync(StationProfile profile)` - Create new profile
- `Task UpdateAsync(StationProfile profile)` - Update profile
- `Task DeleteAsync(int id)` - Delete profile

**Prototype Connection:**
- Station Modal → GetAllAsync() - Display profiles list
- Station Modal → AddAsync() / UpdateAsync() - Save/Edit profile
- Station Modal → DeleteAsync() - Remove profile
- Active Station Display → GetDefaultAsync()

**Data Model:** `StationProfile`
- Callsign (required), OperatorCallsign
- MyGridSquare, MyState, MyCounty, Qth
- Op (Operator Name), SkccNr
- UtcOffsetHours, ObservesDaylightSavingTime
- IsDefault (boolean)

---

## 3. CAT (Radio Control)

### Service: `InternetCatCoordinator`
**Location:** `CvarcLogger.App/Services/InternetCatCoordinator.cs`

**Methods:**
- `Task<(bool Success, string? Error)> ConnectAsync()` - Connect to radio via Internet Control (K4 protocol)
- `Task<K4PollResult> PollAsync()` - Get radio status (frequency, mode, power)
- `Task DisconnectAsync()` - Disconnect from radio

### Service: `RigControlCoordinator`
**Location:** `CvarcLogger.App/Services/RigControlCoordinator.cs`
- USB serial CAT via Hamlib/rigctld

### Settings (for configuration)
- `InternetRadioEnabled` (bool)
- `InternetRadioHost` (string) - Radio IP/hostname
- `InternetRadioPort` (int) - Radio TCP port (default 9200)
- `CatEnabled` (bool) - USB serial mode enable
- `CatSource` (enum) - Off/Usb/Internet selector

**Prototype Connection:**
- CAT Control Modal → ConnectAsync() / DisconnectAsync()
- CAT Status Indicator → PollAsync() - Update frequency/mode
- Settings save → SettingsService properties

---

## 4. Callsign Lookup Services

### Service: `LookupCoordinator`
**Location:** `CvarcLogger.App/Services/LookupCoordinator.cs`

**Lookup Chain Order:**
1. QRZ.com (highest priority - only source with County field)
2. QRZCQ.com
3. Callook.info (free fallback, US-only FCC data)

**Method:**
- `Task<CallsignLookupResult> LookupAsync(string callsign)` - Chain lookup across services

**Individual Services:**
- `QrzLookupService` - Paid subscription
- `QrzCqLookupService` - Paid subscription  
- `CallookLookupService` - Free/public

**Prototype Connection:**
- Lookup Modal → Test buttons trigger individual service tests
- Save/Clear → Credential store (SettingsService)
- Auto-lookup on entry form (entry from Contact column)

**Data Model:** `CallsignLookupResult`
- Name, GridSquare, City, State, County, Country
- DxccEntityCode, Latitude, Longitude
- Found (bool)

---

## 5. Settings & Configuration

### Service: `SettingsService`
**Location:** `CvarcLogger.App/Services/SettingsService.cs`
**Storage:** `{DataDirectory}/settings.json`

**Key Properties:**
- `LastUsedStationProfileId` - Active station
- `CatSource` - Radio connection mode (Off/Usb/Internet)
- `InternetRadioEnabled`, `InternetRadioHost`, `InternetRadioPort`
- `CatEnabled` - USB serial mode
- `HiddenLogColumns` - Column visibility state (List<string>)
- `LogColumnOrder` - Column display order (Dictionary<string, int>)
- `LogColumnWidths` - Column width settings (Dictionary<string, double>)

**Methods:**
- `EnsureLogColumnDefault(key, defaultVisible)` - Initialize column defaults
- `SaveHiddenLogColumns()` - Persist visibility state
- `SaveLogColumnOrder(order)` - Persist column order
- `SaveLogColumnWidths(widths)` - Persist column widths

**Prototype Connection:**
- Column Visibility Modal → HiddenLogColumns property
- All Settings Modals → Individual properties

---

## 6. ADIF Import/Export

### ViewModel: `ImportExportViewModel`
**Location:** `CvarcLogger.App/ViewModels/ImportExportViewModel.cs`

**Methods:**
- `Task ImportAsync()` - Parse ADIF file, add QSOs to database
- `Task ExportAsync()` - Export current log as ADIF file

### Data Mapping: `AdifFieldMapper`
**Location:** `CvarcLogger.Core/Adif/AdifFieldMapper.cs`
- Bidirectional mapping between `Qso` model and ADIF standard fields
- Handles extended fields not in Qso model

**Prototype Connection:**
- File Modal → ImportAsync() - Load ADIF file
- File Modal → ExportAsync() - Save ADIF file
- Handles ADIF 3.14 standard field mapping

---

## 7. Reference Data

### DXCC Entities
- **Interface:** `IDxccEntityRepository`
- **Implementation:** `CvarcLogger.Data/Repositories/DxccEntityRepository.cs`
- **Usage:** Callsign lookup DXCC entity resolution

### SOTA Summits
- **Interface:** `ISotaActivationRepository`
- **Implementation:** `CvarcLogger.Data/Repositories/SotaActivationRepository.cs`
- **Usage:** SOTA reference lookup and validation

### POTA Parks
- **Service:** `PotaParkLookupService`
- **Location:** `CvarcLogger.App/Services/PotaParkLookupService.cs`
- **Data:** Pre-loaded from CSV (PotaParks.csv in Assets)
- **Usage:** POTA reference lookup

---

## 8. Integration Summary

### For Prototype v2.0 Backend Integration:

**Critical Path:**
1. QsoRepository → Log Grid display & entry save
2. StationProfileRepository → Station modal config
3. SettingsService → All modal settings, column visibility
4. LookupCoordinator → Callsign lookups
5. CAT Services → Radio status polling

**Optional (Phase 2):**
- ImportExportViewModel → File operations
- SOTA/POTA reference lookups
- GridTracker2 broadcast (WsjtxUdpListenerService)

**Data Flow:**
```
Prototype UI
    ↓
JavaScript Event Handlers
    ↓
REST/gRPC API Layer (NEW - to be created)
    ↓
Backend Services (existing)
    ↓
Repository Layer
    ↓
SQLite Database
```

---

## 9. Next Steps for Integration

1. **Create API Layer** - Expose repository/service methods as HTTP endpoints
2. **Wire JavaScript** - Add fetch() calls from prototype modals
3. **Test QSO CRUD** - Entry form → AddAsync() → Grid refresh
4. **Test Settings** - Modal save → SettingsService properties
5. **Test CAT** - Connect button → InternetCatCoordinator.ConnectAsync()
6. **Test Lookups** - Lookup modal → LookupCoordinator.LookupAsync()
