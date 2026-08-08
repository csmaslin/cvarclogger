/**
 * CvarcLogger API Client v2.0
 * JavaScript client for connecting the prototype to the Web API backend
 */

class CvarcLoggerAPI {
    constructor(baseUrl = 'http://localhost:5000/api') {
        this.baseUrl = baseUrl;
        this.headers = { 'Content-Type': 'application/json' };
    }

    /**
     * Make HTTP request to API
     */
    async request(endpoint, method = 'GET', body = null) {
        try {
            const options = {
                method,
                headers: this.headers
            };

            if (body) {
                options.body = JSON.stringify(body);
            }

            const response = await fetch(`${this.baseUrl}${endpoint}`, options);

            if (!response.ok) {
                const error = await response.json().catch(() => ({ error: response.statusText }));
                throw new Error(error.error || `HTTP ${response.status}`);
            }

            return await response.json();
        } catch (error) {
            console.error(`API Error [${method} ${endpoint}]:`, error);
            throw error;
        }
    }

    // ===== QSO (Log Entry) Methods =====

    async getAllQsos() {
        return this.request('/qso');
    }

    async getQsoById(id) {
        return this.request(`/qso/${id}`);
    }

    async createQso(qsoData) {
        return this.request('/qso', 'POST', qsoData);
    }

    async updateQso(id, qsoData) {
        return this.request(`/qso/${id}`, 'PUT', qsoData);
    }

    async deleteQso(id) {
        return this.request(`/qso/${id}`, 'DELETE');
    }

    async deleteAllQsos() {
        return this.request('/qso/clear-all', 'DELETE');
    }

    // ===== Station Profile Methods =====

    async getAllStations() {
        return this.request('/station');
    }

    async getDefaultStation() {
        return this.request('/station/default');
    }

    async getStationById(id) {
        return this.request(`/station/${id}`);
    }

    async createStation(stationData) {
        return this.request('/station', 'POST', stationData);
    }

    async updateStation(id, stationData) {
        return this.request(`/station/${id}`, 'PUT', stationData);
    }

    async deleteStation(id) {
        return this.request(`/station/${id}`, 'DELETE');
    }

    // ===== CAT Control Methods =====

    async getCatStatus() {
        return this.request('/cat/status');
    }

    async connectCat() {
        return this.request('/cat/connect', 'POST');
    }

    async disconnectCat() {
        return this.request('/cat/disconnect', 'POST');
    }

    async getCatConfig() {
        return this.request('/cat/config');
    }

    async setCatConfig(config) {
        return this.request('/cat/config', 'POST', config);
    }

    // ===== Lookup Methods =====

    async lookupCallsign(callsign) {
        return this.request(`/lookup/callsign/${encodeURIComponent(callsign)}`);
    }

    async testQrz() {
        return this.request('/lookup/qrz/test', 'POST');
    }

    async testQrzCq() {
        return this.request('/lookup/qrzcq/test', 'POST');
    }

    async testCallook() {
        return this.request('/lookup/callook/test', 'POST');
    }

    async setQrzCredentials(username, password) {
        return this.request('/lookup/credentials/qrz', 'POST', { username, password });
    }

    async setQrzCqCredentials(username, password) {
        return this.request('/lookup/credentials/qrzcq', 'POST', { username, password });
    }

    // ===== Settings Methods =====

    async getColumnVisibility() {
        return this.request('/settings/column-visibility');
    }

    async setColumnVisibility(hiddenColumns) {
        return this.request('/settings/column-visibility', 'POST', { hiddenColumns });
    }

    async getColumnOrder() {
        return this.request('/settings/column-order');
    }

    async setColumnOrder(columnOrder) {
        return this.request('/settings/column-order', 'POST', { columnOrder });
    }

    async getColumnWidths() {
        return this.request('/settings/column-widths');
    }

    async setColumnWidths(columnWidths) {
        return this.request('/settings/column-widths', 'POST', { columnWidths });
    }

    async getDefaultStationId() {
        return this.request('/settings/station/default');
    }

    async setDefaultStationId(stationProfileId) {
        return this.request('/settings/station/default', 'POST', { stationProfileId });
    }

    // ===== Reference Data Methods =====

    async getAllDxcc() {
        return this.request('/referencedata/dxcc');
    }

    async searchDxcc(query) {
        return this.request(`/referencedata/dxcc/search?query=${encodeURIComponent(query)}`);
    }

    async getAllSota() {
        return this.request('/referencedata/sota');
    }

    async searchSota(query) {
        return this.request(`/referencedata/sota/search?query=${encodeURIComponent(query)}`);
    }

    async getAllPota() {
        return this.request('/referencedata/pota');
    }

    async searchPota(query) {
        return this.request(`/referencedata/pota/search?query=${encodeURIComponent(query)}`);
    }
}

// Export for use in HTML/JS
const api = new CvarcLoggerAPI();
