"use strict";

document.addEventListener('DOMContentLoaded', () => {
    // Если пользователь уже авторизован, перенаправляем его на дашборд
    if (localStorage.getItem('token')) {
        window.location.href = 'dashboard.html';
        return;
    }

    const loginForm = document.getElementById('loginForm');
    if (loginForm) {
        loginForm.addEventListener('submit', handleLogin);
    }

    const usernameField = document.getElementById('loginUsername');
    if (usernameField) {
        usernameField.focus();
    }
});

async function handleLogin(e) {
    e.preventDefault();
    const username = document.getElementById('loginUsername').value;
    const password = document.getElementById('loginPassword').value;

    try {
        const response = await fetch('/api/auth/login', {
            method: 'POST',
            headers: { 'Content-Type': 'application/json' },
            body: JSON.stringify({ username, password })
        });

        if (response.ok) {
            const data = await response.json();
            localStorage.setItem('token', data.token);
            localStorage.setItem('user', data.user.username);
            localStorage.setItem('role', data.user.role);
            window.location.href = 'dashboard.html'; // Перенаправление на дашборд
        } else {
            showLoginError('Неверное имя пользователя или пароль');
        }
    } catch (error) {
        console.error('Ошибка входа:', error);
        showLoginError('Ошибка подключения. Попробуйте снова.');
    }
}

function showLoginError(message) {
    const errorDiv = document.getElementById('loginError');
    errorDiv.textContent = message;
    errorDiv.style.display = 'block';
    setTimeout(() => errorDiv.style.display = 'none', 5000);
}