# CvarcLogger v2.0 Prototype - API Integration Guide

This guide shows how to connect the HTML/JavaScript prototype to the REST API backend.

---

## Quick Start

### 1. Include the API Client

Add this to your prototype HTML before closing `</body>`:

```html
<script src="cvarclogger_api_client.js"></script>
```

The `api` object will be globally available.

### 2. Start the API Server

```bash
cd C:\Projects\CvarcLogger\src\CvarcLogger.WebApi
dotnet run
```

The API will run on `http://localhost:5000`

---

## Integration Points

### Entry Form → Log QSO

**When:** User clicks "Log QSO" button

```javascript
async function logQso() {
    const qsoData = {
        callsign: document.getElementById('callsign').value,
        qsoDateTimeOnUtc: new Date().toISOString(),
        band: document.getElementById('band').value,
        frequency: parseFloat(document.getElementById('freq').value),
        mode: document.getElementById('mode').value,
        rstSent: document.getElementById('rstSent').value,
        rstRcvd: document.getElementById('rstRcvd').value,
        name: document.getElementById('name').value,
        gridSquare: document.getElementById('grid').value,
        city: document.getElementById('city').value,
        state: document.getElementById('state').value,
        country: document.getElementById('country').value,
        comment: document.getElementById('comment').value
    };

    try {
        const result = await api.createQso(qsoData);
        console.log('QSO saved:', result);
        
        // Refresh the log grid
        await loadContactLog();
        
        // Clear the form
        document.getElementById('entry-form').reset();
        
        alert('QSO logged successfully!');
    } catch (error) {
        alert('Error logging QSO: ' + error.message);
    }
}
```

### Contact Log Grid → Load QSOs

**When:** Page loads or after saving a QSO

```javascript
async function loadContactLog() {
    try {
        const qsos = await api.getAllQsos();
        
        // Clear existing rows
        const tbody = document.querySelector('.log-table tbody');
        tbody.innerHTML = '';
        
        // Add QSO rows
        qsos.forEach((qso, index) => {
            const row = `
                <tr>
                    <td>${index + 1}</td>
                    <td>${qso.callsign}</td>
                    <td>${new Date(qso.qsoDateTimeOnUtc).toLocaleDateString()}</td>
                    <td>${qso.frequency}</td>
                    <td>${qso.band}</td>
                    <td>${qso.mode}</td>
                    <td>${qso.rstSent}</td>
                    <td>${qso.name}</td>
                    <td>${qso.gridSquare}</td>
                </tr>
            `;
            tbody.insertAdjacentHTML('beforeend', row);
        });
        
    } catch (error) {
        console.error('Error loading QSOs:', error);
    }
}

// Call on page load
document.addEventListener('DOMContentLoaded', loadContactLog);
```

### Station Modal → Load/Save Stations

**Load Stations:**
```javascript
async function loadStations() {
    try {
        const stations = await api.getAllStations();
        const list = document.getElementById('station-list');
        list.innerHTML = '';
        
        stations.forEach(station => {
            const item = `
                <div class="station-item">
                    <span>${station.callsign}</span>
                    <button onclick="selectStation(${station.id})">Select</button>
                    <button onclick="deleteStation(${station.id})">Delete</button>
                </div>
            `;
            list.insertAdjacentHTML('beforeend', item);
        });
    } catch (error) {
        console.error('Error loading stations:', error);
    }
}
```

**Save New Station:**
```javascript
async function saveStation() {
    const stationData = {
        callsign: document.getElementById('station-callsign').value,
        operatorCallsign: document.getElementById('operator-callsign').value,
        myGridSquare: document.getElementById('my-grid').value,
        myState: document.getElementById('my-state').value,
        myCounty: document.getElementById('my-county').value,
        qth: document.getElementById('qth').value,
        op: document.getElementById('op').value,
        utcOffsetHours: parseInt(document.getElementById('utc-offset').value),
        observesDaylightSavingTime: document.getElementById('dst').checked,
        isDefault: document.getElementById('default').checked
    };

    try {
        const result = await api.createStation(stationData);
        console.log('Station saved:', result);
        await loadStations();
        alert('Station profile saved!');
    } catch (error) {
        alert('Error saving station: ' + error.message);
    }
}
```

### CAT Control → Connect to Radio

**Connect Button:**
```javascript
async function connectCat() {
    try {
        const result = await api.connectCat();
        updateCatStatus();
        alert('Connected to radio!');
    } catch (error) {
        alert('Failed to connect: ' + error.message);
    }
}

// Disconnect button
async function disconnectCat() {
    try {
        await api.disconnectCat();
        updateCatStatus();
        alert('Disconnected from radio');
    } catch (error) {
        alert('Error disconnecting: ' + error.message);
    }
}

// Poll for status updates
async function updateCatStatus() {
    try {
        const status = await api.getCatStatus();
        
        const indicator = document.getElementById('cat-indicator');
        const statusText = document.getElementById('cat-status-text');
        
        if (status.connected) {
            indicator.style.background = '#4CAF50'; // Green
            statusText.textContent = `CAT: Connected (${status.frequency} MHz, ${status.mode})`;
        } else {
            indicator.style.background = '#999'; // Grey
            statusText.textContent = 'CAT: Disconnected';
        }
    } catch (error) {
        console.error('Error updating CAT status:', error);
    }
}

// Poll every 2 seconds
setInterval(updateCatStatus, 2000);
```

### Callsign Auto-Lookup

**When:** User enters callsign and loses focus

```javascript
async function lookupCallsign(callsign) {
    if (!callsign || callsign.length < 2) return;
    
    try {
        const result = await api.lookupCallsign(callsign);
        
        if (result.found) {
            // Populate form fields
            document.getElementById('name').value = result.name || '';
            document.getElementById('grid').value = result.gridSquare || '';
            document.getElementById('city').value = result.city || '';
            document.getElementById('state').value = result.state || '';
            document.getElementById('country').value = result.country || '';
            
            console.log('Lookup successful:', result);
        } else {
            console.log('Callsign not found');
        }
    } catch (error) {
        console.error('Lookup error:', error);
    }
}

// Wire up to callsign input
document.getElementById('callsign').addEventListener('blur', (e) => {
    lookupCallsign(e.target.value);
});
```

### Lookup Modal → Test Services & Save Credentials

**QRZ Test:**
```javascript
async function testQrz() {
    try {
        const result = await api.testQrz();
        if (result.success) {
            alert('✓ QRZ test successful!');
        } else {
            alert('✗ QRZ test failed: ' + result.message);
        }
    } catch (error) {
        alert('Error: ' + error.message);
    }
}

// Save QRZ credentials
async function saveQrzCredentials() {
    const username = document.getElementById('qrz-username').value;
    const password = document.getElementById('qrz-password').value;
    
    try {
        await api.setQrzCredentials(username, password);
        alert('QRZ credentials saved!');
    } catch (error) {
        alert('Error saving credentials: ' + error.message);
    }
}
```

### Column Visibility Modal

**Load Current Settings:**
```javascript
async function loadColumnVisibility() {
    try {
        const settings = await api.getColumnVisibility();
        
        // Get all checkboxes
        const checkboxes = document.querySelectorAll('.column-item input[type="checkbox"]');
        
        checkboxes.forEach(checkbox => {
            const columnName = checkbox.dataset.column;
            // Show all by default, hide only the hidden ones
            checkbox.checked = !settings.hiddenColumns.includes(columnName);
        });
    } catch (error) {
        console.error('Error loading column visibility:', error);
    }
}
```

**Save Column Visibility:**
```javascript
async function saveColumnVisibility() {
    const checkboxes = document.querySelectorAll('.column-item input[type="checkbox"]');
    const hiddenColumns = [];
    
    checkboxes.forEach(checkbox => {
        if (!checkbox.checked) {
            hiddenColumns.push(checkbox.dataset.column);
        }
    });
    
    try {
        await api.setColumnVisibility(hiddenColumns);
        
        // Apply visibility to grid
        applyColumnVisibility(hiddenColumns);
        
        alert('Column settings saved!');
    } catch (error) {
        alert('Error saving columns: ' + error.message);
    }
}
```

---

## Complete Integration Example

Here's a minimal working example:

```html
<!DOCTYPE html>
<html>
<head>
    <title>CvarcLogger v2.0 Prototype</title>
</head>
<body>
    <button onclick="testApi()">Test API Connection</button>
    <div id="result"></div>
    
    <script src="cvarclogger_api_client.js"></script>
    <script>
        async function testApi() {
            try {
                // Test QSO fetch
                const qsos = await api.getAllQsos();
                document.getElementById('result').innerHTML = 
                    `<p>✓ API Connected! Found ${qsos.length} QSOs</p>`;
            } catch (error) {
                document.getElementById('result').innerHTML = 
                    `<p>✗ API Error: ${error.message}</p>`;
            }
        }
    </script>
</body>
</html>
```

---

## Error Handling

All API calls can throw errors. Handle them consistently:

```javascript
async function apiCall(apiFunction) {
    try {
        const result = await apiFunction();
        return result;
    } catch (error) {
        // Show user-friendly error
        const message = error.message || 'Unknown error';
        console.error('API Error:', message);
        
        // Show error in modal or toast
        showError(`Error: ${message}`);
        
        return null;
    }
}
```

---

## Testing the Integration

### 1. Verify API is Running
```bash
curl http://localhost:5000/api/qso
# Should return JSON array of QSOs
```

### 2. Test in Browser Console
```javascript
// Test connection
api.getAllQsos().then(qsos => console.log(qsos));

// Test create
api.createQso({
    callsign: 'W5TEST',
    band: '20m',
    mode: 'SSB',
    frequency: 14.200,
    qsoDateTimeOnUtc: new Date().toISOString()
}).then(qso => console.log('Created:', qso));

// Test lookup
api.lookupCallsign('W5XYZ').then(result => console.log(result));
```

### 3. Run Integration Tests
```bash
cd C:\Projects\CvarcLogger
dotnet test tests/CvarcLogger.WebApi.Tests/CvarcLogger.WebApi.Tests.csproj
```

---

## Deployment

### Development
- API: `http://localhost:5000`
- Prototype: Open HTML file locally or via local server

### Production
Update API base URL in prototype:
```javascript
const api = new CvarcLoggerAPI('https://yourdomain.com/api');
```

---

## Troubleshooting

### API Connection Failed
- ✓ Ensure API is running: `dotnet run` in WebApi project
- ✓ Check URL: `http://localhost:5000/api`
- ✓ Check browser console for CORS errors

### CORS Errors
- ✓ Verify CORS policy in Program.cs allows prototype origin
- ✓ Check browser console for details

### Data Not Saving
- ✓ Verify request body format matches API documentation
- ✓ Check API logs for validation errors
- ✓ Use browser DevTools Network tab to inspect requests/responses

### Lookup Returns "Not Found"
- ✓ Verify callsign exists in lookup service
- ✓ Check that QRZ/QRZCQ credentials are configured (if needed)
- ✓ Callook.info works as free fallback for US callsigns

---

## Next Steps

1. ✅ Web API created
2. ✅ API client library created
3. ⏳ Integrate prototype with API client (use examples above)
4. ⏳ Test QSO CRUD operations
5. ⏳ Test CAT connection
6. ⏳ Test callsign lookups
7. ⏳ Deploy as v2.0 release

---

## API Client Reference

All methods return Promises. Use `async/await` or `.then()/.catch()`:

```javascript
// async/await
try {
    const qsos = await api.getAllQsos();
} catch (error) {
    console.error(error);
}

// .then() chain
api.getAllQsos()
    .then(qsos => console.log(qsos))
    .catch(error => console.error(error));
```

See `cvarclogger_api_client.js` for full method signatures.
