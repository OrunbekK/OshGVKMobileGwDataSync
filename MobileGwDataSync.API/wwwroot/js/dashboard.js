"use strict";

// --- Глобальное состояние ---
let token = localStorage.getItem('token');
let autoRefresh = true;
let refreshInterval;
const charts = { activityChart: null, statusChart: null };
let logsModalInstance = null;

// --- Инициализация ---

document.addEventListener('DOMContentLoaded', () => {
    logsModalInstance = new bootstrap.Modal(document.getElementById('logsModal'));
    checkAuth();
});

// --- Логика Аутентификации ---

async function checkAuth() {
    if (!token) {
        window.location.href = 'login.html'; // Если токена нет, на страницу входа
        return;
    }

    try {
        const response = await fetch('/api/auth/verify', {
            headers: { 'Authorization': `Bearer ${token}` }
        });
        if (response.ok) {
            initializeDashboard(); // Если токен валиден, инициализируем дашборд
        } else {
            logout(); // Если токен невалиден, выходим
        }
    } catch (error) {
        console.error('Ошибка проверки авторизации:', error);
        logout();
    }
}

function initializeDashboard() {
    document.getElementById('username').textContent = localStorage.getItem('user');
    initializeClock();
    loadAllData();
    startAutoRefresh();
}

function logout() {
    fetch('/api/auth/logout', { method: 'POST', headers: { 'Authorization': `Bearer ${token}` } });
    localStorage.clear();
    window.location.href = 'login.html'; // После выхода на страницу входа
}

// --- Загрузка данных (API) ---

async function fetchData(url) {
    try {
        const response = await fetch(url, { headers: { 'Authorization': `Bearer ${token}` } });
        if (response.status === 401) { logout(); return null; }
        if (!response.ok) throw new Error(`HTTP ошибка! Статус: ${response.status}`);
        return await response.json();
    } catch (error) {
        console.error(`Не удалось получить данные с ${url}:`, error);
        return null;
    }
}

async function loadAllData() { await Promise.all([loadDashboardData(), loadHealthStatus()]); }
async function loadDashboardData() {
    const data = await fetchData('/dashboard/data');
    if (data) { updateMetrics(data); updateCharts(data); updateTables(data); }
}
async function loadHealthStatus() {
    const health = await fetchData('/dashboard/health-status');
    if (health) { updateHealthCards(health); }
}

// --- Обновление UI (все функции рендеринга) ---

function updateMetrics(data) {
    const { stats24h, recentRuns, activeJobs } = data;
    const totalRuns = stats24h?.reduce((s, c) => s + c.count, 0) || 0;
    const successCount = stats24h?.find(s => s.status === 'Completed')?.count || 0;
    const successRate = totalRuns > 0 ? (successCount / totalRuns * 100).toFixed(1) : "0.0";
    const records = recentRuns?.reduce((s, r) => s + r.recordsProcessed, 0) || 0;

    document.getElementById('metrics-row').innerHTML = `
        <div class='col-md-6 col-lg-3'><div class='metric-card'><div class='metric-icon bg-primary bg-opacity-10 text-primary'><i class='bi bi-arrow-repeat'></i></div><div class='metric-value'>${totalRuns}</div><div class='metric-label'>Запусков (24ч)</div></div></div>
        <div class='col-md-6 col-lg-3'><div class='metric-card'><div class='metric-icon bg-success bg-opacity-10 text-success'><i class='bi bi-check-circle'></i></div><div class='metric-value'>${successRate}%</div><div class='metric-label'>Успешность</div></div></div>
        <div class='col-md-6 col-lg-3'><div class='metric-card'><div class='metric-icon bg-info bg-opacity-10 text-info'><i class='bi bi-database'></i></div><div class='metric-value'>${records.toLocaleString()}</div><div class='metric-label'>Записей сегодня</div></div></div>
        <div class='col-md-6 col-lg-3'><div class='metric-card'><div class='metric-icon bg-warning bg-opacity-10 text-warning'><i class='bi bi-lightning'></i></div><div class='metric-value'>${activeJobs?.length || 0}</div><div class='metric-label'>Активных задач</div></div></div>`;
}

function updateCharts(data) {
    // Activity Chart
    const activityLabels = data.chartData.map(d => new Date(d.date).toLocaleDateString('ru-RU', { month: 'short', day: 'numeric' }));
    if (charts.activityChart) {
        charts.activityChart.data.labels = activityLabels;
        charts.activityChart.data.datasets[0].data = data.chartData.map(d => d.success);
        charts.activityChart.data.datasets[1].data = data.chartData.map(d => d.failed);
        charts.activityChart.update();
    } else {
        charts.activityChart = new Chart(document.getElementById('activityChart').getContext('2d'), { type: 'bar', data: { labels: activityLabels, datasets: [{ label: 'Успешно', data: data.chartData.map(d => d.success), backgroundColor: 'rgba(40, 167, 69, 0.8)' }, { label: 'Ошибка', data: data.chartData.map(d => d.failed), backgroundColor: 'rgba(220, 53, 69, 0.8)' }] } });
    }
    // Status Chart
    const statusData = { Completed: data.stats24h.find(s => s.status === 'Completed')?.count || 0, Failed: data.stats24h.find(s => s.status === 'Failed')?.count || 0, InProgress: data.stats24h.find(s => s.status === 'InProgress')?.count || 0 };
    if (charts.statusChart) {
        charts.statusChart.data.datasets[0].data = Object.values(statusData);
        charts.statusChart.update();
    } else {
        charts.statusChart = new Chart(document.getElementById('statusChart').getContext('2d'), { type: 'doughnut', data: { labels: Object.keys(statusData), datasets: [{ data: Object.values(statusData), backgroundColor: ['rgba(40, 167, 69, 0.8)', 'rgba(220, 53, 69, 0.8)', 'rgba(255, 193, 7, 0.8)'] }] } });
    }
}

function updateTables(data) {
    // Jobs Table
    const jobsBody = document.getElementById('jobsTable');
    jobsBody.innerHTML = data.activeJobs.map(job => `<tr><td>${job.name}</td><td><code>${job.cronExpression}</code></td><td>${job.lastRunAt ? new Date(job.lastRunAt).toLocaleString('ru-RU') : 'Никогда'}</td><td>${job.nextRunAt ? new Date(job.nextRunAt).toLocaleString('ru-RU') : '-'}</td><td>${getStatusBadge(job.lastRunStatus)}</td><td>${localStorage.getItem('role') !== 'Viewer' ? `<button class="btn btn-sm btn-primary" onclick="triggerJob('${job.id}')"><i class="bi bi-play-fill"></i></button>` : ''}</td></tr>`).join('') || `<tr><td colspan="6" class="text-center">Нет активных задач</td></tr>`;
    // History Table
    const historyBody = document.getElementById('historyTable');
    historyBody.innerHTML = data.recentRuns.map(run => `<tr><td>${run.jobName}</td><td>${new Date(run.startTime).toLocaleString('ru-RU')}</td><td>${run.duration ? run.duration.toFixed(1) + 's' : '-'}</td><td>${run.recordsProcessed.toLocaleString()}</td><td>${getStatusBadge(run.status)}</td><td><button class="btn btn-sm btn-info" onclick="viewLogs('${run.id}')"><i class="bi bi-list-ul"></i></button></td></tr>`).join('') || `<tr><td colspan="6" class="text-center">Нет недавних запусков</td></tr>`;
}

function updateHealthCards(health) {
    document.getElementById('healthCards').innerHTML = Object.entries(health).map(([key, value]) => `<div class='col-md-4'><div class='metric-card'><h6 class='text-uppercase'>${key}</h6><div class='d-flex align-items-center'><span class='health-indicator ${value.status.toLowerCase()}'></span><span>${value.status.toUpperCase()}</span></div><small class='text-muted'>${value.message}</small></div></div>`).join('');
}

// --- Интерактивность ---

async function triggerJob(jobId) {
    if (!confirm(`Вы уверены, что хотите запустить задачу сейчас?`)) return;
    try {
        const response = await fetch(`/dashboard/trigger/${jobId}`, { method: 'POST', headers: { 'Authorization': `Bearer ${token}` } });
        if (response.ok) { alert('Задача успешно запущена'); setTimeout(loadDashboardData, 1000); }
        else { const err = await response.json(); alert(`Ошибка: ${err.message}`); }
    } catch (error) { console.error('Ошибка запуска задачи:', error); }
}

async function viewLogs(runId) {
    const logs = await fetchData(`/dashboard/logs/${runId}`);
    const logsBody = document.getElementById('logsTableBody');
    if (logs) {
        logsBody.innerHTML = logs.map(log => `<tr><td>${log.stepName}</td><td>${getStatusBadge(log.status)}</td><td>${log.durationMs || 0}ms</td><td>${log.details || '-'}</td></tr>`).join('') || '<tr><td colspan="4">Нет логов</td></tr>';
        logsModalInstance.show();
    }
}

function toggleAutoRefresh() {
    autoRefresh = !autoRefresh;
    document.getElementById('refreshStatus').textContent = `Автообновление: ${autoRefresh ? 'ВКЛ' : 'ВЫКЛ'}`;
    document.getElementById('refreshIcon').className = autoRefresh ? 'bi bi-pause-fill' : 'bi bi-play-fill';
    if (autoRefresh) startAutoRefresh(); else clearInterval(refreshInterval);
}

// --- Утилиты ---
function startAutoRefresh() { clearInterval(refreshInterval); if (autoRefresh) { refreshInterval = setInterval(loadAllData, 10000); } }
function initializeClock() { setInterval(() => { document.getElementById('serverTime').textContent = new Date().toLocaleTimeString('ru-RU'); }, 1000); }
function getStatusBadge(status) { return `<span class="status-badge status-${(status || 'unknown').toLowerCase()}">${status || 'Unknown'}</span>`; }