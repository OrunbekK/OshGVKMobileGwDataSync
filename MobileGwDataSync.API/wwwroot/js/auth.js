// auth.js - Логика авторизации
class AuthManager {
    constructor() {
        this.apiClient = window.apiClient;
        this.initializeEventListeners();
        this.checkExistingAuth();
    }

    initializeEventListeners() {
        const loginForm = document.getElementById('loginForm');
        if (loginForm) {
            loginForm.addEventListener('submit', (e) => this.handleLogin(e));
        }

        // Auto-focus на поле username
        const usernameField = document.getElementById('username');
        if (usernameField) {
            usernameField.focus();
        }
    }

    async checkExistingAuth() {
        if (this.apiClient.token) {
            try {
                await this.apiClient.verifyAuth();
                this.redirectToDashboard();
            } catch (error) {
                console.log('Token invalid, staying on login page');
            }
        }
    }

    async handleLogin(event) {
        event.preventDefault();

        const submitButton = event.target.querySelector('button[type="submit"]');
        const errorDiv = document.getElementById('loginError');

        // Disable button and show loading
        submitButton.disabled = true;
        submitButton.innerHTML = '<span class="spinner"></span> Logging in...';
        errorDiv.classList.remove('show');

        const username = document.getElementById('username').value;
        const password = document.getElementById('password').value;

        try {
            await this.apiClient.login(username, password);
            this.redirectToDashboard();
        } catch (error) {
            this.showError(error.message || 'Invalid credentials');
        } finally {
            submitButton.disabled = false;
            submitButton.innerHTML = 'Login';
        }
    }

    showError(message) {
        const errorDiv = document.getElementById('loginError');
        errorDiv.textContent = message;
        errorDiv.classList.add('show');

        // Hide after 5 seconds
        setTimeout(() => {
            errorDiv.classList.remove('show');
        }, 5000);
    }

    redirectToDashboard() {
        const returnUrl = new URLSearchParams(window.location.search).get('returnUrl');
        window.location.href = returnUrl || '/pages/dashboard.html';
    }
}

// Инициализация при загрузке страницы
document.addEventListener('DOMContentLoaded', () => {
    new AuthManager();
});