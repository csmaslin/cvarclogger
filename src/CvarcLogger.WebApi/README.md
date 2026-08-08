# CvarcLogger Web API v2.0

REST API layer for the CvarcLogger v2.0 GUI Prototype. Provides HTTP endpoints for all backend functionality.

## Architecture

The Web API bridges the gap between the HTML/JavaScript prototype and the existing CvarcLogger backend:

```
┌─────────────────────────────┐
│  HTML/JavaScript Prototype  │
│  (cvarclogger_gui_proto_v2) │
└────────────┬────────────────┘
             │ HTTP Requests
             ▼
┌─────────────────────────────┐
│  CvarcLogger.WebApi         │
│  (Controllers & Routes)     │
└────────────┬────────────────┘
             │ Method Calls
             ▼
┌─────────────────────────────┐
│  Backend Services           │
│  - LookupCoordinator        │
│  - InternetCatCoordinator   │
│  - SettingsService          │
└────────────┬────────────────┘
             │ Data Access
             ▼
┌─────────────────────────────┐
│  Repository Layer           │
│  - QsoRepository            │
│  - StationProfileRepository │
│  - etc.                     │
└────────────┬────────────────┘
             │ SQL
             ▼
┌─────────────────────────────┐
│  SQLite Database            │
│  (cvarclogger.db)           │
└─────────────────────────────┘
```

## Project Structure

```
CvarcLogger.WebApi/
├── Controllers/
│   ├── QsoController.cs           # Log entry management
│   ├── StationController.cs       # Station profiles
│   ├── CatController.cs           # Radio control
│   ├── LookupController.cs        # Callsign lookups
│   ├── SettingsController.cs      # Configuration
│   └── ReferenceDataController.cs # SOTA/POTA/DXCC
├── Program.cs                      # Dependency injection
├── appsettings.json                # Configuration
├── CvarcLogger.WebApi.csproj      # Project file
└── README.md                       # This file
```

## Getting Started

### Prerequisites
- .NET 9.0 SDK
- CvarcLogger solution already built

### Setup

1. **Build the WebApi project:**
```bash
cd C:\Projects\CvarcLogger
dotnet build src/CvarcLogger.WebApi/CvarcLogger.WebApi.csproj
```

2. **Run the API:**
```bash
cd C:\Projects\CvarcLogger\src\CvarcLogger.WebApi
dotnet run
```

The API will start on `http://localhost:5000`

### Development

For development with hot reload:
```bash
dotnet watch run
```

## Controllers

### QsoController
- Manages QSO (log entry) CRUD operations
- Endpoints: GET, POST, PUT, DELETE
- Used by: Contact Log Grid, Entry Form

### StationController
- Manages station profile CRUD operations
- Endpoints: GET all, GET default, GET by ID, POST, PUT, DELETE
- Used by: Station Configuration Modal

### CatController
- Manages radio connection and status polling
- Endpoints: connect, disconnect, status, config get/set
- Used by: CAT Status Indicator, CAT Control Modal

### LookupController
- Handles callsign lookups via QRZ/QRZCQ/Callook chain
- Endpoints: lookup by callsign, test services, set credentials
- Used by: Lookup Modal, Entry Form auto-lookup

### SettingsController
- Manages application settings and UI preferences
- Endpoints: column visibility, column order, column widths, default station
- Used by: Column Visibility Modal, Settings persistence

### ReferenceDataController
- Provides access to reference data (SOTA, POTA, DXCC)
- Endpoints: get all, search
- Used by: SOTA/POTA reference lookups, award tracking

## Configuration

### appsettings.json

```json
{
  "Logging": {
    "LogLevel": {
      "Default": "Information"
    }
  },
  "AllowedHosts": "*",
  "CvarcLogger": {
    "DataDirectory": "./data"
  }
}
```

### Dependency Injection (Program.cs)

Key services registered:
- `IQsoRepository` → QsoRepository
- `IStationProfileRepository` → StationProfileRepository
- `SettingsService` → Singleton
- `LookupCoordinator` → Scoped
- `InternetCatCoordinator` → Scoped
- CORS policy configured for prototype

## Connecting the Prototype

### JavaScript Setup

Add this to your prototype's HTML:
```javascript
const API_BASE = 'http://localhost:5000/api';

// Example: Load QSOs
async function loadQsos() {
  const response = await fetch(`${API_BASE}/qso`);
  const qsos = await response.json();
  displayInGrid(qsos);
}

// Example: Save QSO
async function saveQso(qsoData) {
  const response = await fetch(`${API_BASE}/qso`, {
    method: 'POST',
    headers: { 'Content-Type': 'application/json' },
    body: JSON.stringify(qsoData)
  });
  return await response.json();
}
```

### CORS Configuration

Currently allows all origins. For production, update Program.cs:
```csharp
options.AddPolicy("AllowPrototype", policy =>
{
    policy.WithOrigins("http://localhost:3000")
          .AllowAnyMethod()
          .AllowAnyHeader();
});
```

## API Response Format

All responses are JSON. Successful responses include data or message:
```json
{ 
  "count": 10,
  "qsos": [...]
}
```

Error responses include an error field:
```json
{
  "error": "QSO not found"
}
```

## Testing the API

### Using Swagger UI
```
http://localhost:5000/swagger
```

### Using curl

```bash
# Get all QSOs
curl http://localhost:5000/api/qso

# Create QSO
curl -X POST http://localhost:5000/api/qso \
  -H "Content-Type: application/json" \
  -d '{"callsign":"W5XYZ","band":"20m","mode":"SSB"}'

# Get CAT status
curl http://localhost:5000/api/cat/status

# Connect to radio
curl -X POST http://localhost:5000/api/cat/connect

# Lookup callsign
curl http://localhost:5000/api/lookup/callsign/W5XYZ
```

## Troubleshooting

### Port Already in Use
If port 5000 is already in use:
```bash
dotnet run --urls "http://localhost:5001"
```

### CORS Errors
If the prototype can't connect:
1. Ensure the API is running on `http://localhost:5000`
2. Check that CORS policy is enabled in Program.cs
3. Verify the prototype is accessing the correct API URL

### Database Connection
Make sure the database file exists at `{DataDirectory}/cvarclogger.db`

## Next Steps

1. ✅ Create Web API project
2. ✅ Create Controllers
3. ✅ Configure dependency injection
4. ⏳ Wire up prototype JavaScript to API endpoints
5. ⏳ Test QSO CRUD operations
6. ⏳ Test CAT connection
7. ⏳ Test Callsign lookups
8. ⏳ Deploy as v2.0

## Resources

- [API Endpoint Reference](../../WEBAPI_ENDPOINTS.md)
- [Backend API Mapping](../../BACKEND_API_MAPPING.md)
- [ASP.NET Core Docs](https://learn.microsoft.com/en-us/aspnet/core/)
- [Protocol Buffers for gRPC](https://developers.google.com/protocol-buffers) (future enhancement)
