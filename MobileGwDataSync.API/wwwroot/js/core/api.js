// api.js - С учетом существующего DashboardController
class ApiClient {
    constructor() {
        this.baseUrl = '';
        this.token = localStorage.getItem('jwtToken');
    }

    async request(url, options = {}) {
        const config = {
            ...options,
            headers: {
                'Content-Type': 'application/json',
                ...options.headers
            }
        };

        if (this.token) {
            config.headers['Authorization'] = `Bearer ${this.token}`;
        }

        try {
            const response = await fetch(url, config);

            if (response.status === 401) {
                this.handleUnauthorized();
                throw new Error('Unauthorized');
            }

            if (response.status === 204) {
                return { success: true };
            }

            const contentType = response.headers.get('content-type');
            if (contentType && contentType.includes('application/json')) {
                const data = await response.json();

                if (!response.ok) {
                    throw new Error(data.message || data.error || `HTTP ${response.status}`);
                }

                return data;
            } else {
                if (!response.ok) {
                    throw new Error(`HTTP ${response.status}`);
                }
                return await response.text();
            }
        } catch (error) {
            console.error('API Request failed:', error);
            throw error;
        }
    }

    setToken(token) {
        this.token = token;
        localStorage.setItem('jwtToken', token);
    }

    clearToken() {
        this.token = null;
        localStorage.removeItem('jwtToken');
        localStorage.removeItem('user');
    }

    handleUnauthorized() {
        this.clearToken();
        window.location.href = '/login.html';
    }

    // ===== AUTH ENDPOINTS =====
    async login(username, password) {
        const response = await this.request('/api/auth/login', {
            method: 'POST',
            body: JSON.stringify({ username, password })
        });

        if (response.token) {
            this.setToken(response.token);
            localStorage.setItem('user', JSON.stringify(response.user));
        }

        return response;
    }

    async logout() {
        try {
            await this.request('/api/auth/logout', { method: 'POST' });
        } finally {
            this.clearToken();
            window.location.href = '/login.html';
        }
    }

    async verifyAuth() {
        return this.request('/api/auth/verify');
    }

    // ===== DASHBOARD ENDPOINTS (из DashboardController) =====
    async getDashboardData() {
        // Использует существующий /dashboard/data endpoint
        return this.request('/dashboard/data');
    }

    async getHealthStatus() {
        // Использует /dashboard/health-status
        return this.request('/dashboard/health-status');
    }

    async triggerJob(jobId) {
        // Использует /dashboard/trigger/{jobId}
        return this.request(`/dashboard/trigger/${jobId}`, { method: 'POST' });
    }

    async getRunLogs(runId) {
        // Использует /dashboard/logs/{runId}
        return this.request(`/dashboard/logs/${runId}`);
    }

    // ===== ДОПОЛНИТЕЛЬНЫЕ API ENDPOINTS (из других контроллеров) =====

    // Из MetricsController
    async getMetrics() {
        return this.request('/api/v1/metrics/performance');
    }

    // Из SyncController  
    async getSyncHistory(limit = 50) {
        return this.request(`/api/v1/sync/history?limit=${limit}`);
    }

    // Из JobsController
    async getJobs() {
        return this.request('/api/v1/jobs');
    }

    async updateJob(jobId, data) {
        return this.request(`/api/v1/jobs/${jobId}`, {
            method: 'PUT',
            body: JSON.stringify(data)
        });
    }

    // Из HealthController
    async getHealth() {
        return this.request('/api/v1/health');
    }

    // Вспомогательные методы
    parseJwt(token) {
        try {
            const base64Url = token.split('.')[1];
            const base64 = base64Url.replace(/-/g, '+').replace(/_/g, '/');
            const jsonPayload = decodeURIComponent(atob(base64).split('').map(c => {
                return '%' + ('00' + c.charCodeAt(0).toString(16)).slice(-2);
            }).join(''));
            return JSON.parse(jsonPayload);
        } catch (error) {
            console.error('Failed to parse JWT:', error);
            return {};
        }
    }

    isTokenExpired() {
        if (!this.token) return true;

        const tokenData = this.parseJwt(this.token);
        const expiryTime = tokenData.exp * 1000;
        return Date.now() >= expiryTime;
    }
}

window.api = new ApiClient();