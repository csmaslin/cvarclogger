# CvarcLogger Web API v2.0 - Endpoint Reference

Base URL: `http://localhost:5000/api`

---

## QSO (Log Entry) Endpoints

### Get All QSOs
```
GET /api/qso
Response: List<Qso>
```

### Get QSO by ID
```
GET /api/qso/{id}
Response: Qso
```

### Create QSO
```
POST /api/qso
Body: Qso
Response: Qso (with assigned ID)
```

### Update QSO
```
PUT /api/qso/{id}
Body: Qso
Response: { message: "QSO updated successfully" }
```

### Delete QSO
```
DELETE /api/qso/{id}
Response: { message: "QSO deleted successfully" }
```

### Clear All QSOs
```
DELETE /api/qso/clear-all
Response: { message: "Deleted {count} QSOs" }
```

---

## Station Profile Endpoints

### Get All Stations
```
GET /api/station
Response: List<StationProfile>
```

### Get Default Station
```
GET /api/station/default
Response: StationProfile
```

### Get Station by ID
```
GET /api/station/{id}
Response: StationProfile
```

### Create Station
```
POST /api/station
Body: StationProfile
Response: StationProfile (with assigned ID)
```

### Update Station
```
PUT /api/station/{id}
Body: StationProfile
Response: { message: "Station profile updated successfully" }
```

### Delete Station
```
DELETE /api/station/{id}
Response: { message: "Station profile deleted successfully" }
```

---

## CAT (Radio Control) Endpoints

### Get CAT Status
```
GET /api/cat/status
Response: {
  status: "Connected|Disconnected|...",
  connected: boolean,
  frequency: number,
  mode: string,
  power: number
}
```

### Connect to Radio
```
POST /api/cat/connect
Response: { message: "Connected to radio", status: "connected" }
```

### Disconnect from Radio
```
POST /api/cat/disconnect
Response: { message: "Disconnected from radio", status: "disconnected" }
```

### Get CAT Config
```
GET /api/cat/config
Response: {
  enabled: boolean,
  host: string,
  port: number
}
```

### Set CAT Config
```
POST /api/cat/config
Body: {
  enabled: boolean,
  host: string,
  port: number
}
Response: { message: "CAT configuration updated" }
```

---

## Lookup Endpoints

### Lookup Callsign
```
GET /api/lookup/callsign/{callsign}
Response: {
  found: boolean,
  name: string,
  gridSquare: string,
  city: string,
  state: string,
  county: string,
  country: string,
  dxccEntityCode: string,
  latitude: number,
  longitude: number
}
```

### Test QRZ
```
POST /api/lookup/qrz/test
Response: { success: boolean, message: string }
```

### Test QRZCQ
```
POST /api/lookup/qrzcq/test
Response: { success: boolean, message: string }
```

### Test Callook
```
POST /api/lookup/callook/test
Response: { success: boolean, message: string }
```

### Set QRZ Credentials
```
POST /api/lookup/credentials/qrz
Body: {
  username: string,
  password: string
}
Response: { message: "QRZ credentials configured" }
```

### Set QRZCQ Credentials
```
POST /api/lookup/credentials/qrzcq
Body: {
  username: string,
  password: string
}
Response: { message: "QRZCQ credentials configured" }
```

---

## Settings Endpoints

### Get Column Visibility
```
GET /api/settings/column-visibility
Response: { hiddenColumns: string[] }
```

### Set Column Visibility
```
POST /api/settings/column-visibility
Body: { hiddenColumns: string[] }
Response: { message: "Column visibility updated" }
```

### Get Column Order
```
GET /api/settings/column-order
Response: { columnOrder: { [key]: int } }
```

### Set Column Order
```
POST /api/settings/column-order
Body: { columnOrder: { [key]: int } }
Response: { message: "Column order updated" }
```

### Get Column Widths
```
GET /api/settings/column-widths
Response: { columnWidths: { [key]: number } }
```

### Set Column Widths
```
POST /api/settings/column-widths
Body: { columnWidths: { [key]: number } }
Response: { message: "Column widths updated" }
```

### Get Default Station
```
GET /api/settings/station/default
Response: { stationProfileId: int }
```

### Set Default Station
```
POST /api/settings/station/default
Body: { stationProfileId: int }
Response: { message: "Default station updated" }
```

---

## Reference Data Endpoints

### Get All DXCC Entities
```
GET /api/referencedata/dxcc
Response: { count: int, entities: DxccEntity[] }
```

### Search DXCC Entities
```
GET /api/referencedata/dxcc/search?query={text}
Response: { count: int, results: DxccEntity[] }
```

### Get All SOTA Summits
```
GET /api/referencedata/sota
Response: { count: int, summits: SotaActivation[] }
```

### Search SOTA Summits
```
GET /api/referencedata/sota/search?query={text}
Response: { count: int, results: SotaActivation[] }
```

### Get All POTA Parks
```
GET /api/referencedata/pota
Response: { count: int, parks: PotaActivation[] }
```

### Search POTA Parks
```
GET /api/referencedata/pota/search?query={text}
Response: { count: int, results: PotaActivation[] }
```

---

## Usage Example (JavaScript/Prototype)

```javascript
// Fetch all QSOs
fetch('http://localhost:5000/api/qso')
  .then(r => r.json())
  .then(qsos => {
    // Display QSOs in contact log grid
    displayQsos(qsos);
  });

// Create new QSO
fetch('http://localhost:5000/api/qso', {
  method: 'POST',
  headers: { 'Content-Type': 'application/json' },
  body: JSON.stringify({
    callsign: 'W5XYZ',
    qsoDateTimeOnUtc: new Date().toISOString(),
    band: '20m',
    mode: 'SSB',
    frequency: 14.200
  })
})
.then(r => r.json())
.then(qso => {
  console.log('QSO saved:', qso);
  // Refresh grid
  loadQsos();
});

// Connect to radio
fetch('http://localhost:5000/api/cat/connect', { method: 'POST' })
  .then(r => r.json())
  .then(data => updateCatStatus(data));

// Lookup callsign
fetch('http://localhost:5000/api/lookup/callsign/W5XYZ')
  .then(r => r.json())
  .then(result => {
    if (result.found) {
      // Populate form fields
      document.getElementById('name').value = result.name;
      document.getElementById('grid').value = result.gridSquare;
    }
  });
```

---

## Error Responses

All error responses follow this format:

```json
{
  "error": "Description of what went wrong"
}
```

HTTP Status Codes:
- `200 OK` - Success
- `201 Created` - Resource created
- `400 Bad Request` - Invalid input
- `404 Not Found` - Resource not found
- `500 Internal Server Error` - Server error

---

## Running the API

```bash
cd C:\Projects\CvarcLogger\src\CvarcLogger.WebApi
dotnet run
```

The API will be available at `http://localhost:5000`
Swagger UI: `http://localhost:5000/swagger`

---

## CORS Configuration

The API is configured with CORS to allow requests from any origin, method, or header. This allows the prototype to make cross-origin requests during development.

For production, update the CORS policy in `Program.cs` to restrict to specific origins.
