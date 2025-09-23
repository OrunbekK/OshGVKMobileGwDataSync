// login.js - Логика страницы авторизации
class LoginPage {
    constructor() {
        this.form = document.getElementById('loginForm');
        this.alertContainer = document.getElementById('alertContainer');
        this.loginBtn = document.getElementById('loginBtn');
        this.loginText = document.getElementById('loginText');
        this.loginSpinner = document.getElementById('loginSpinner');

        this.init();
    }

    async init() {
        // Проверяем валидность если токен есть
        const token = localStorage.getItem('jwtToken');
        if (token) {
            try {
                // Проверяем валидность токена
                const response = await fetch('/dashboard/data', {
                    method: 'GET',
                    headers: {
                        'Authorization': `Bearer ${token}`
                    }
                });

                if (response.ok) {
                    // Токен валидный - переходим на dashboard
                    window.location.href = '/dashboard.html';
                    return;
                } else if (response.status === 401) {
                    // Токен невалидный - очищаем
                    localStorage.removeItem('jwtToken');
                    localStorage.removeItem('user');
                }
            } catch (error) {
                console.error('Token validation error:', error);
                // При ошибке остаемся на странице логина
            }
        }

        // Настраиваем обработчики
        this.form.addEventListener('submit', (e) => this.handleSubmit(e));

        // Загружаем сохраненное имя пользователя
        const savedUsername = localStorage.getItem('savedUsername');
        if (savedUsername) {
            document.getElementById('username').value = savedUsername;
            document.getElementById('rememberMe').checked = true;
        }
    }

    async handleSubmit(e) {
        e.preventDefault();
        
        const username = document.getElementById('username').value;
        const password = document.getElementById('password').value;
        const rememberMe = document.getElementById('rememberMe').checked;
        
        this.setLoading(true);
        this.hideAlert();

        try {
            const response = await window.api.login(username, password);
            
            if (rememberMe) {
                localStorage.setItem('savedUsername', username);
            } else {
                localStorage.removeItem('savedUsername');
            }
            
            // Redirect to dashboard
            window.location.href = '/dashboard.html';
        } catch (error) {
            this.showAlert(error.message || 'Invalid credentials', 'danger');
            this.setLoading(false);
        }
    }

    setLoading(loading) {
        this.loginBtn.disabled = loading;
        this.loginText.textContent = loading ? 'Signing in...' : 'Sign In';
        this.loginSpinner.classList.toggle('d-none', !loading);
    }

    showAlert(message, type = 'danger') {
        this.alertContainer.innerHTML = `
            <div class="alert alert-${type} alert-dismissible fade show" role="alert">
                <i class="bi bi-exclamation-triangle-fill me-2"></i>
                ${message}
                <button type="button" class="btn-close" data-bs-dismiss="alert"></button>
            </div>
        `;
    }

    hideAlert() {
        this.alertContainer.innerHTML = '';
    }
}

// Initialize on page load
document.addEventListener('DOMContentLoaded', () => {
    new LoginPage();
});