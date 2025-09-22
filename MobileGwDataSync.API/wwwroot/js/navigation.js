// navigation.js - Компонент навигации между dashboard'ами
class NavigationManager {
    constructor() {
        this.currentUser = localStorage.getItem('user');
        this.currentRole = localStorage.getItem('role');
        this.apiClient = window.apiClient;
        this.initializeNavigation();
    }

    initializeNavigation() {
        this.renderNavigationBar();
        this.attachEventListeners();
        this.startClock();
    }

    renderNavigationBar() {
        const navContainer = document.getElementById('navigationBar');
        if (!navContainer) return;

        const currentPage = window.location.pathname.includes('advanced') ? 'advanced' : 'basic';

        navContainer.innerHTML = `
            <nav class="main-navbar">
                <div class="navbar-brand">
                    <span class="navbar-logo">📊</span>
                    <span class="navbar-title">MobileGW Data Sync</span>
                </div>
                
                <div class="navbar-nav">
                    <a href="/pages/dashboard.html" 
                       class="nav-link ${currentPage === 'basic' ? 'active' : ''}">
                        <i class="bi bi-speedometer2"></i> Dashboard
                    </a>
                    <a href="/pages/dashboard-advanced.html" 
                       class="nav-link ${currentPage === 'advanced' ? 'active' : ''}">
                        <i class="bi bi-grid-3x3-gap"></i> Advanced View
                    </a>
                </div>
                
                <div class="navbar-info">
                    <span class="user-info">
                        <i class="bi bi-person-circle"></i>
                        <span id="currentUser">${this.currentUser}</span>
                        <span class="badge role-badge">${this.currentRole}</span>
                    </span>
                    <span class="divider">|</span>
                    <span id="currentTime" class="time-display">--:--:--</span>
                    <span class="divider">|</span>
                    <a href="#" id="logoutBtn" class="logout-link">
                        <i class="bi bi-box-arrow-right"></i> Logout
                    </a>
                </div>
            </nav>
        `;
    }

    attachEventListeners() {
        const logoutBtn = document.getElementById('logoutBtn');
        if (logoutBtn) {
            logoutBtn.addEventListener('click', (e) => this.handleLogout(e));
        }
    }

    async handleLogout(event) {
        event.preventDefault();

        if (confirm('Are you sure you want to logout?')) {
            try {
                await this.apiClient.logout();
                window.location.href = '/pages/login.html';
            } catch (error) {
                console.error('Logout failed:', error);
                // Force redirect даже при ошибке
                this.apiClient.clearToken();
                window.location.href = '/pages/login.html';
            }
        }
    }

    startClock() {
        const updateTime = () => {
            const timeElement = document.getElementById('currentTime');
            if (timeElement) {
                timeElement.textContent = new Date().toLocaleTimeString();
            }
        };

        updateTime();
        setInterval(updateTime, 1000);
    }
}

// Экспорт для использования в других модулях
window.NavigationManager = NavigationManager;