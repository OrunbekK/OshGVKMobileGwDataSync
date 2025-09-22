// api-client.js - Централизованный клиент для работы с API
class ApiClient {
    constructor() {
        this.baseUrl = window.location.origin;
        this.token = localStorage.getItem('token');
    }

    setToken(token) {
        this.token = token;
        localStorage.setItem('token', token);
    }

    clearToken() {
        this.token = null;
        localStorage.removeItem('token');
        localStorage.removeItem('user');
        localStorage.removeItem('role');
    }

    async request(url, options = {}) {
        const headers = {
            'Content-Type': 'application/json',
            ...options.headers
        };

        if (this.token) {
            headers['Authorization'] = `Bearer ${this.token}`;
        }

        try {
            const response = await fetch(`${this.baseUrl}${url}`, {
                ...options,
                headers
            });

            if (response.status === 401) {
                this.clearToken();
                window.location.href = '/pages/login.html';
                throw new Error('Unauthorized');
            }

            const data = await response.json();

            if (!response.ok) {
                throw new Error(data.message || `HTTP ${response.status}`);
            }

            return data;
        } catch (error) {
            console.error('API request failed:', error);
            throw error;
        }
    }

    // Auth methods
    async login(username, password) {
        const data = await this.request('/api/auth/login', {
            method: 'POST',
            body: JSON.stringify({ username, password })
        });

        this.setToken(data.token);
        localStorage.setItem('user', data.user.username);
        localStorage.setItem('role', data.user.role);

        return data;
    }

    async logout() {
        try {
            await this.request('/api/auth/logout', { method: 'POST' });
        } finally {
            this.clearToken();
        }
    }

    async verifyAuth() {
        return this.request('/api/auth/verify');
    }

    // Dashboard methods
    async getDashboardData() {
        return this.request('/dashboard/data');
    }

    async getHealthStatus() {
        return this.request('/dashboard/health-status');
    }

    async triggerJob(jobId) {
        return this.request(`/dashboard/trigger/${jobId}`, { method: 'POST' });
    }

    async getRunLogs(runId) {
        return this.request(`/dashboard/logs/${runId}`);
    }
}

// Создаем глобальный экземпляр
window.apiClient = new ApiClient();