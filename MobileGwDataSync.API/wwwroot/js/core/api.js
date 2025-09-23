// api.js - Централизованный API клиент с JWT
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

            if (!response.ok) {
                const error = await response.json().catch(() => ({}));
                throw new Error(error.message || `HTTP ${response.status}`);
            }

            return await response.json();
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
    }

    handleUnauthorized() {
        this.clearToken();
        window.location.href = '/login.html';
    }

    // Auth endpoints
    async login(username, password) {
        const response = await this.request('/api/v1/auth/login', {
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
            await this.request('/api/v1/auth/logout', { method: 'POST' });
        } finally {
            this.clearToken();
            localStorage.removeItem('user');
            window.location.href = '/login.html';
        }
    }

    // Dashboard endpoints
    async getDashboardStats() {
        return this.request('/api/v1/metrics/performance');
    }

    async getSyncHistory(limit = 50) {
        return this.request(`/api/v1/sync/history?limit=${limit}`);
    }

    async getJobs() {
        return this.request('/api/v1/jobs');
    }

    async triggerJob(jobId) {
        return this.request(`/api/v1/jobs/${jobId}/trigger`, { method: 'POST' });
    }

    async updateJob(jobId, data) {
        return this.request(`/api/v1/jobs/${jobId}`, {
            method: 'PUT',
            body: JSON.stringify(data)
        });
    }

    async getHealth() {
        return this.request('/api/v1/health');
    }
}

window.api = new ApiClient();