// auth.js - Authentication module
class AuthManager {
    constructor() {
        this.token = localStorage.getItem('jwtToken');
        this.user = JSON.parse(localStorage.getItem('user') || '{}');
        this.refreshTokenInterval = null;
    }

    isAuthenticated() {
        return !!this.token && !this.isTokenExpired();
    }

    isTokenExpired() {
        if (!this.token) return true;

        try {
            const payload = this.parseJwt(this.token);
            const expiryTime = payload.exp * 1000; // Convert to milliseconds
            return Date.now() >= expiryTime;
        } catch (error) {
            return true;
        }
    }

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
            return null;
        }
    }

    getUser() {
        return this.user;
    }

    hasRole(role) {
        return this.user.role === role;
    }

    hasPermission(permission) {
        return this.user.permissions && this.user.permissions.includes(permission);
    }

    startTokenRefresh() {
        // Refresh token every 15 minutes
        this.refreshTokenInterval = setInterval(async () => {
            if (this.isAuthenticated()) {
                try {
                    await this.refreshToken();
                } catch (error) {
                    console.error('Token refresh failed:', error);
                    this.logout();
                }
            }
        }, 15 * 60 * 1000);
    }

    stopTokenRefresh() {
        if (this.refreshTokenInterval) {
            clearInterval(this.refreshTokenInterval);
            this.refreshTokenInterval = null;
        }
    }

    async refreshToken() {
        const response = await fetch('/api/v1/auth/refresh', {
            method: 'POST',
            headers: {
                'Authorization': `Bearer ${this.token}`,
                'Content-Type': 'application/json'
            }
        });

        if (response.ok) {
            const data = await response.json();
            this.token = data.token;
            localStorage.setItem('jwtToken', this.token);
            return this.token;
        } else {
            throw new Error('Token refresh failed');
        }
    }

    logout() {
        this.stopTokenRefresh();
        localStorage.removeItem('jwtToken');
        localStorage.removeItem('user');
        window.location.href = '/login.html';
    }
}

window.authManager = new AuthManager();