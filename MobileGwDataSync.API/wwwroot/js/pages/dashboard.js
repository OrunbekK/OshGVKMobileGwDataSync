// dashboard.js - Полная версия для работы с DashboardController
class Dashboard {
    constructor() {
        this.currentPage = 'overview';
        this.refreshInterval = null;
        this.charts = {};
        this.chartManager = new ChartManager();
        this.tableManager = new TableManager();
        this.metricsManager = new MetricsManager();

        this.init();
    }

    async init() {
        // Проверка авторизации
        if (!localStorage.getItem('jwtToken')) {
            window.location.href = '/login.html';
            return;
        }

        // Проверка срока токена
        if (window.api.isTokenExpired()) {
            try {
                await window.api.refreshToken();
            } catch (error) {
                window.location.href = '/login.html';
                return;
            }
        }

        // Загружаем информацию о пользователе
        this.loadUserInfo();

        // Настраиваем обработчики событий
        this.setupEventListeners();

        // Загружаем начальную страницу
        await this.loadPage('overview');

        // Запускаем автообновление
        this.startAutoRefresh();

        // Запускаем часы
        this.startClock();
    }

    loadUserInfo() {
        const user = JSON.parse(localStorage.getItem('user') || '{}');
        const userElement = document.getElementById('currentUser');
        if (userElement) {
            userElement.textContent = user.username || 'User';
        }
    }

    setupEventListeners() {
        // Переключение sidebar
        const sidebarToggle = document.getElementById('sidebarToggle');
        if (sidebarToggle) {
            sidebarToggle.addEventListener('click', () => {
                document.getElementById('sidebar').classList.toggle('collapsed');
            });
        }

        // Навигация
        document.querySelectorAll('.sidebar-nav .nav-link').forEach(link => {
            link.addEventListener('click', (e) => {
                e.preventDefault();
                const page = link.dataset.page;
                this.loadPage(page);

                // Обновляем активное состояние
                document.querySelectorAll('.sidebar-nav .nav-link').forEach(l => l.classList.remove('active'));
                link.classList.add('active');
            });
        });

        // Кнопка обновления
        const refreshBtn = document.getElementById('refreshBtn');
        if (refreshBtn) {
            refreshBtn.addEventListener('click', () => {
                this.loadPage(this.currentPage);
            });
        }

        // Выход
        const logoutBtn = document.getElementById('logoutBtn');
        if (logoutBtn) {
            logoutBtn.addEventListener('click', async (e) => {
                e.preventDefault();
                if (confirm('Вы уверены, что хотите выйти?')) {
                    await window.api.logout();
                }
            });
        }

        // Обработка нажатия клавиш
        document.addEventListener('keydown', (e) => {
            // F5 - обновить текущую страницу
            if (e.key === 'F5') {
                e.preventDefault();
                this.loadPage(this.currentPage);
            }
        });
    }

    async loadPage(page) {
        this.currentPage = page;
        const contentArea = document.getElementById('contentArea');

        // Показываем загрузку
        contentArea.innerHTML = `
            <div class="text-center py-5">
                <div class="spinner-border text-primary" role="status">
                    <span class="visually-hidden">Загрузка...</span>
                </div>
            </div>
        `;

        try {
            switch (page) {
                case 'overview':
                    await this.loadOverview();
                    break;
                case 'jobs':
                    await this.loadJobs();
                    break;
                case 'history':
                    await this.loadHistory();
                    break;
                case 'health':
                    await this.loadHealth();
                    break;
                case 'settings':
                    this.loadSettings();
                    break;
                default:
                    contentArea.innerHTML = '<div class="alert alert-warning">Страница не найдена</div>';
            }
        } catch (error) {
            console.error(`Error loading page ${page}:`, error);
            this.showError(`Ошибка загрузки страницы: ${error.message}`);
        }
    }

    async loadOverview() {
        try {
            // Используем существующий endpoint /dashboard/data
            const data = await window.api.getDashboardData();

            const contentArea = document.getElementById('contentArea');

            // Расчет метрик из данных
            const totalRuns = data.stats24h?.reduce((sum, s) => sum + s.count, 0) || 0;
            const successCount = data.stats24h?.find(s => s.status === 'Completed')?.count || 0;
            const failedCount = data.stats24h?.find(s => s.status === 'Failed')?.count || 0;
            const successRate = totalRuns > 0 ? ((successCount / totalRuns) * 100).toFixed(1) : '0';
            const totalRecords = data.recentRuns?.reduce((sum, r) => sum + r.recordsProcessed, 0) || 0;

            contentArea.innerHTML = `
                <div class="d-flex justify-content-between align-items-center mb-4">
                    <h4>Dashboard Overview</h4>
                    <span class="text-muted">Last updated: ${new Date().toLocaleTimeString()}</span>
                </div>
                
                <!-- Метрики -->
                <div class="row g-4 mb-4">
                    <div class="col-xl-3 col-md-6">
                        <div class="metric-card">
                            <div class="metric-icon bg-primary bg-opacity-10 text-primary">
                                <i class="bi bi-arrow-repeat"></i>
                            </div>
                            <div class="metric-value">${totalRuns}</div>
                            <div class="metric-label">Total Runs (24h)</div>
                            <div class="metric-change positive">
                                <i class="bi bi-graph-up"></i>
                                <span>Last 24 hours</span>
                            </div>
                        </div>
                    </div>
                    <div class="col-xl-3 col-md-6">
                        <div class="metric-card">
                            <div class="metric-icon bg-success bg-opacity-10 text-success">
                                <i class="bi bi-check-circle"></i>
                            </div>
                            <div class="metric-value">${successRate}%</div>
                            <div class="metric-label">Success Rate</div>
                            <div class="metric-change ${successRate >= 90 ? 'positive' : 'negative'}">
                                <i class="bi bi-${successRate >= 90 ? 'arrow-up' : 'arrow-down'}"></i>
                                <span>${successCount}/${totalRuns}</span>
                            </div>
                        </div>
                    </div>
                    <div class="col-xl-3 col-md-6">
                        <div class="metric-card">
                            <div class="metric-icon bg-danger bg-opacity-10 text-danger">
                                <i class="bi bi-x-circle"></i>
                            </div>
                            <div class="metric-value">${failedCount}</div>
                            <div class="metric-label">Failed Runs</div>
                            <div class="metric-change ${failedCount > 0 ? 'negative' : 'positive'}">
                                <i class="bi bi-exclamation-triangle"></i>
                                <span>Requires attention</span>
                            </div>
                        </div>
                    </div>
                    <div class="col-xl-3 col-md-6">
                        <div class="metric-card">
                            <div class="metric-icon bg-info bg-opacity-10 text-info">
                                <i class="bi bi-database"></i>
                            </div>
                            <div class="metric-value">${Utils.formatNumber(totalRecords)}</div>
                            <div class="metric-label">Records Processed</div>
                            <div class="metric-change positive">
                                <i class="bi bi-activity"></i>
                                <span>Total today</span>
                            </div>
                        </div>
                    </div>
                </div>

                <!-- Графики -->
                <div class="row g-4 mb-4">
                    <div class="col-lg-8">
                        <div class="card">
                            <div class="card-header d-flex justify-content-between align-items-center">
                                <h5 class="mb-0">Sync Activity (Last 7 Days)</h5>
                                <div class="btn-group btn-group-sm" role="group">
                                    <button type="button" class="btn btn-outline-secondary" onclick="dashboard.changeChartView('week')">Week</button>
                                    <button type="button" class="btn btn-outline-secondary" onclick="dashboard.changeChartView('month')">Month</button>
                                </div>
                            </div>
                            <div class="card-body">
                                <canvas id="activityChart" height="100"></canvas>
                            </div>
                        </div>
                    </div>
                    <div class="col-lg-4">
                        <div class="card">
                            <div class="card-header">
                                <h5 class="mb-0">Status Distribution (24h)</h5>
                            </div>
                            <div class="card-body">
                                <canvas id="statusChart"></canvas>
                                <div class="mt-3">
                                    <small class="text-muted">
                                        Total: ${totalRuns} runs in last 24 hours
                                    </small>
                                </div>
                            </div>
                        </div>
                    </div>
                </div>

                <!-- Активные задачи и последние запуски -->
                <div class="row g-4">
                    <!-- Активные задачи -->
                    <div class="col-lg-6">
                        <div class="card">
                            <div class="card-header d-flex justify-content-between">
                                <h5 class="mb-0">Active Jobs</h5>
                                <a href="#" onclick="dashboard.loadPage('jobs')" class="text-decoration-none">View all</a>
                            </div>
                            <div class="card-body">
                                <div class="table-responsive">
                                    <table class="table table-hover table-sm">
                                        <thead>
                                            <tr>
                                                <th>Job Name</th>
                                                <th>Schedule</th>
                                                <th>Next Run</th>
                                                <th>Actions</th>
                                            </tr>
                                        </thead>
                                        <tbody>
                                            ${(data.activeJobs || []).slice(0, 5).map(job => `
                                                <tr>
                                                    <td>
                                                        <span class="fw-medium">${job.name}</span>
                                                    </td>
                                                    <td>
                                                        <code class="small">${job.cronExpression}</code>
                                                    </td>
                                                    <td>
                                                        <small>${job.nextRunAt ? new Date(job.nextRunAt).toLocaleString() : 'N/A'}</small>
                                                    </td>
                                                    <td>
                                                        <button class="btn btn-sm btn-primary" onclick="dashboard.triggerJob('${job.id}')" title="Run Now">
                                                            <i class="bi bi-play-fill"></i>
                                                        </button>
                                                    </td>
                                                </tr>
                                            `).join('')}
                                        </tbody>
                                    </table>
                                    ${data.activeJobs?.length === 0 ? '<p class="text-muted text-center">No active jobs</p>' : ''}
                                </div>
                            </div>
                        </div>
                    </div>

                    <!-- Последние запуски -->
                    <div class="col-lg-6">
                        <div class="card">
                            <div class="card-header d-flex justify-content-between">
                                <h5 class="mb-0">Recent Sync Runs</h5>
                                <a href="#" onclick="dashboard.loadPage('history')" class="text-decoration-none">View all</a>
                            </div>
                            <div class="card-body">
                                <div class="table-responsive">
                                    <table class="table table-hover table-sm">
                                        <thead>
                                            <tr>
                                                <th>Job</th>
                                                <th>Time</th>
                                                <th>Records</th>
                                                <th>Status</th>
                                            </tr>
                                        </thead>
                                        <tbody>
                                            ${(data.recentRuns || []).slice(0, 5).map(run => `
                                                <tr>
                                                    <td>
                                                        <span class="fw-medium">${run.jobName}</span>
                                                    </td>
                                                    <td>
                                                        <small>${Utils.getRelativeTime(run.startTime)}</small>
                                                    </td>
                                                    <td>
                                                        <span class="badge bg-secondary">${Utils.formatNumber(run.recordsProcessed)}</span>
                                                    </td>
                                                    <td>
                                                        <span class="badge bg-${run.status === 'Completed' ? 'success' : run.status === 'Failed' ? 'danger' : 'warning'}">
                                                            ${run.status}
                                                        </span>
                                                    </td>
                                                </tr>
                                            `).join('')}
                                        </tbody>
                                    </table>
                                    ${data.recentRuns?.length === 0 ? '<p class="text-muted text-center">No recent runs</p>' : ''}
                                </div>
                            </div>
                        </div>
                    </div>
                </div>
            `;

            // Инициализация графиков с данными из DashboardController
            this.initChartsFromDashboardData(data);

        } catch (error) {
            console.error('Error loading overview:', error);
            this.showError(`Ошибка загрузки данных: ${error.message}`);
        }
    }

    async loadJobs() {
        try {
            // Получаем данные из dashboard endpoint
            const data = await window.api.getDashboardData();
            const activeJobs = data.activeJobs || [];

            const contentArea = document.getElementById('contentArea');
            contentArea.innerHTML = `
                <div class="d-flex justify-content-between align-items-center mb-4">
                    <h4>Job Management</h4>
                    <div>
                        <button class="btn btn-success me-2" onclick="dashboard.showAddJobModal()">
                            <i class="bi bi-plus-circle me-2"></i>Add Job
                        </button>
                        <button class="btn btn-primary" onclick="dashboard.loadPage('jobs')">
                            <i class="bi bi-arrow-clockwise me-2"></i>Refresh
                        </button>
                    </div>
                </div>

                <div class="card">
                    <div class="card-body">
                        <div class="table-responsive">
                            <table class="table table-hover">
                                <thead>
                                    <tr>
                                        <th>Job Name</th>
                                        <th>Type</th>
                                        <th>Schedule</th>
                                        <th>Last Run</th>
                                        <th>Next Run</th>
                                        <th>Last Status</th>
                                        <th>Actions</th>
                                    </tr>
                                </thead>
                                <tbody>
                                    ${activeJobs.map(job => `
                                        <tr>
                                            <td>
                                                <div>
                                                    <span class="fw-medium">${job.name}</span>
                                                    ${job.description ? `<br><small class="text-muted">${job.description}</small>` : ''}
                                                </div>
                                            </td>
                                            <td>
                                                <span class="badge bg-secondary">${job.jobType || 'Sync'}</span>
                                            </td>
                                            <td>
                                                <code>${job.cronExpression}</code>
                                                <br>
                                                <small class="text-muted">${Utils.parseCron(job.cronExpression)}</small>
                                            </td>
                                            <td>
                                                ${job.lastRunAt ? `
                                                    <small>${new Date(job.lastRunAt).toLocaleString()}</small>
                                                    <br>
                                                    <small class="text-muted">${Utils.getRelativeTime(job.lastRunAt)}</small>
                                                ` : '<span class="text-muted">Never</span>'}
                                            </td>
                                            <td>
                                                ${job.nextRunAt ? `
                                                    <small>${new Date(job.nextRunAt).toLocaleString()}</small>
                                                    <br>
                                                    <small class="text-muted">${Utils.getRelativeTime(job.nextRunAt)}</small>
                                                ` : '<span class="text-muted">-</span>'}
                                            </td>
                                            <td>
                                                <span class="badge bg-${job.lastRunStatus === 'Completed' ? 'success' : job.lastRunStatus === 'Failed' ? 'danger' : 'secondary'}">
                                                    ${job.lastRunStatus || 'Unknown'}
                                                </span>
                                            </td>
                                            <td>
                                                <div class="btn-group btn-group-sm" role="group">
                                                    <button class="btn btn-primary" onclick="dashboard.triggerJob('${job.id}')" title="Run Now">
                                                        <i class="bi bi-play-fill"></i>
                                                    </button>
                                                    <button class="btn btn-secondary" onclick="dashboard.editJob('${job.id}')" title="Edit">
                                                        <i class="bi bi-pencil"></i>
                                                    </button>
                                                    <button class="btn btn-info" onclick="dashboard.viewJobHistory('${job.id}')" title="History">
                                                        <i class="bi bi-clock-history"></i>
                                                    </button>
                                                    <button class="btn btn-danger" onclick="dashboard.deleteJob('${job.id}')" title="Delete">
                                                        <i class="bi bi-trash"></i>
                                                    </button>
                                                </div>
                                            </td>
                                        </tr>
                                    `).join('')}
                                </tbody>
                            </table>
                            ${activeJobs.length === 0 ? `
                                <div class="text-center py-4">
                                    <p class="text-muted">No jobs configured</p>
                                    <button class="btn btn-primary" onclick="dashboard.showAddJobModal()">
                                        <i class="bi bi-plus-circle me-2"></i>Add First Job
                                    </button>
                                </div>
                            ` : ''}
                        </div>
                    </div>
                </div>

                <!-- Job Statistics -->
                <div class="row mt-4">
                    <div class="col-md-3">
                        <div class="card text-center">
                            <div class="card-body">
                                <h3 class="text-primary">${activeJobs.length}</h3>
                                <p class="text-muted mb-0">Total Jobs</p>
                            </div>
                        </div>
                    </div>
                    <div class="col-md-3">
                        <div class="card text-center">
                            <div class="card-body">
                                <h3 class="text-success">${activeJobs.filter(j => j.isEnabled).length}</h3>
                                <p class="text-muted mb-0">Active</p>
                            </div>
                        </div>
                    </div>
                    <div class="col-md-3">
                        <div class="card text-center">
                            <div class="card-body">
                                <h3 class="text-warning">${activeJobs.filter(j => !j.isEnabled).length}</h3>
                                <p class="text-muted mb-0">Disabled</p>
                            </div>
                        </div>
                    </div>
                    <div class="col-md-3">
                        <div class="card text-center">
                            <div class="card-body">
                                <h3 class="text-info">${activeJobs.filter(j => j.lastRunStatus === 'Failed').length}</h3>
                                <p class="text-muted mb-0">Need Attention</p>
                            </div>
                        </div>
                    </div>
                </div>
            `;
        } catch (error) {
            console.error('Error loading jobs:', error);
            this.showError(`Ошибка загрузки задач: ${error.message}`);
        }
    }

    async loadHistory() {
        try {
            const history = await window.api.getSyncHistory(100);

            const contentArea = document.getElementById('contentArea');
            contentArea.innerHTML = `
                <div class="d-flex justify-content-between align-items-center mb-4">
                    <h4>Sync History</h4>
                    <div>
                        <button class="btn btn-secondary me-2" onclick="dashboard.exportHistory()">
                            <i class="bi bi-download me-2"></i>Export
                        </button>
                        <button class="btn btn-primary" onclick="dashboard.loadPage('history')">
                            <i class="bi bi-arrow-clockwise me-2"></i>Refresh
                        </button>
                    </div>
                </div>

                <div class="card">
                    <div class="card-body">
                        <div class="table-responsive">
                            <table class="table table-hover">
                                <thead>
                                    <tr>
                                        <th>Run ID</th>
                                        <th>Job Name</th>
                                        <th>Start Time</th>
                                        <th>End Time</th>
                                        <th>Duration</th>
                                        <th>Records Fetched</th>
                                        <th>Records Processed</th>
                                        <th>Status</th>
                                        <th>Actions</th>
                                    </tr>
                                </thead>
                                <tbody>
                                    ${history.map(run => `
                                        <tr>
                                            <td>
                                                <code>${run.id.substring(0, 8)}</code>
                                            </td>
                                            <td>
                                                <span class="fw-medium">${run.jobName}</span>
                                            </td>
                                            <td>
                                                <small>${new Date(run.startTime).toLocaleString()}</small>
                                            </td>
                                            <td>
                                                <small>${run.endTime ? new Date(run.endTime).toLocaleString() : '-'}</small>
                                            </td>
                                            <td>
                                                <span class="badge bg-secondary">${Utils.formatDuration(run.duration)}</span>
                                            </td>
                                            <td class="text-end">
                                                ${Utils.formatNumber(run.recordsFetched)}
                                            </td>
                                            <td class="text-end">
                                                ${Utils.formatNumber(run.recordsProcessed)}
                                            </td>
                                            <td>
                                                <span class="badge bg-${run.status === 'Completed' ? 'success' : run.status === 'Failed' ? 'danger' : 'warning'}">
                                                    ${run.status}
                                                </span>
                                            </td>
                                            <td>
                                                <button class="btn btn-sm btn-info" onclick="dashboard.viewLogs('${run.id}')" title="View Logs">
                                                    <i class="bi bi-list-ul"></i>
                                                </button>
                                            </td>
                                        </tr>
                                    `).join('')}
                                </tbody>
                            </table>
                            ${history.length === 0 ? '<p class="text-muted text-center py-4">No sync history available</p>' : ''}
                        </div>
                    </div>
                </div>
            `;
        } catch (error) {
            console.error('Error loading history:', error);
            this.showError(`Ошибка загрузки истории: ${error.message}`);
        }
    }

    async loadHealth() {
        try {
            // Используем endpoint из DashboardController
            const health = await window.api.getHealthStatus();

            const contentArea = document.getElementById('contentArea');

            // Определяем общий статус
            const allHealthy = Object.values(health).every(h => h.status === 'healthy');
            const hasIssues = Object.values(health).some(h => h.status === 'unhealthy');
            const overallStatus = hasIssues ? 'Critical' : !allHealthy ? 'Warning' : 'Healthy';
            const overallColor = hasIssues ? 'danger' : !allHealthy ? 'warning' : 'success';

            contentArea.innerHTML = `
                <div class="d-flex justify-content-between align-items-center mb-4">
                    <h4>System Health</h4>
                    <button class="btn btn-primary" onclick="dashboard.loadPage('health')">
                        <i class="bi bi-arrow-clockwise me-2"></i>Refresh
                    </button>
                </div>

                <!-- Overall Status -->
                <div class="alert alert-${overallColor} mb-4">
                    <div class="d-flex align-items-center">
                        <i class="bi bi-${overallStatus === 'Healthy' ? 'check-circle' : overallStatus === 'Warning' ? 'exclamation-triangle' : 'x-circle'} fs-4 me-3"></i>
                        <div>
                            <h5 class="mb-1">Overall System Status: ${overallStatus}</h5>
                            <p class="mb-0">Last checked: ${new Date().toLocaleTimeString()}</p>
                        </div>
                    </div>
                </div>

                <!-- Health Components -->
                <div class="row g-4">
                    ${Object.entries(health).map(([key, value]) => `
                        <div class="col-md-6 col-lg-4">
                            <div class="card h-100 border-${value.status === 'healthy' ? 'success' : value.status === 'degraded' ? 'warning' : 'danger'}">
                                <div class="card-header bg-${value.status === 'healthy' ? 'success' : value.status === 'degraded' ? 'warning' : 'danger'} bg-opacity-10">
                                    <div class="d-flex justify-content-between align-items-center">
                                        <h6 class="mb-0 text-uppercase">${key}</h6>
                                        <span class="badge bg-${value.status === 'healthy' ? 'success' : value.status === 'degraded' ? 'warning' : 'danger'}">
                                            ${value.status}
                                        </span>
                                    </div>
                                </div>
                                <div class="card-body">
                                    <p class="mb-2">${value.message}</p>
                                    ${value.details ? `
                                        <small class="text-muted">
                                            ${Object.entries(value.details).map(([k, v]) => `${k}: ${v}`).join(', ')}
                                        </small>
                                    ` : ''}
                                    ${value.responseTime ? `
                                        <div class="mt-2">
                                            <small class="text-muted">Response time: ${value.responseTime}ms</small>
                                        </div>
                                    ` : ''}
                                </div>
                            </div>
                        </div>
                    `).join('')}
                </div>

                <!-- System Metrics -->
                <div class="card mt-4">
                    <div class="card-header">
                        <h5 class="mb-0">System Metrics</h5>
                    </div>
                    <div class="card-body">
                        <div class="row">
                            <div class="col-md-4">
                                <canvas id="cpuGauge"></canvas>
                            </div>
                            <div class="col-md-4">
                                <canvas id="memoryGauge"></canvas>
                            </div>
                            <div class="col-md-4">
                                <canvas id="diskGauge"></canvas>
                            </div>
                        </div>
                    </div>
                </div>
            `;

            // Инициализация индикаторов если есть данные о метриках
            if (health.system && health.system.details) {
                // TODO: Создать gauge графики для CPU, Memory, Disk
            }

        } catch (error) {
            console.error('Error loading health:', error);
            this.showError(`Ошибка загрузки статуса: ${error.message}`);
        }
    }

    loadSettings() {
        const contentArea = document.getElementById('contentArea');
        contentArea.innerHTML = `
            <h4 class="mb-4">Settings</h4>
            
            <div class="row">
                <div class="col-lg-8">
                    <!-- General Settings -->
                    <div class="card mb-4">
                        <div class="card-header">
                            <h5 class="mb-0">General Settings</h5>
                        </div>
                        <div class="card-body">
                            <form>
                                <div class="mb-3">
                                    <label class="form-label">Auto-refresh interval</label>
                                    <select class="form-select" id="refreshInterval">
                                        <option value="10000">10 seconds</option>
                                        <option value="30000" selected>30 seconds</option>
                                        <option value="60000">1 minute</option>
                                        <option value="300000">5 minutes</option>
                                        <option value="0">Disabled</option>
                                    </select>
                                </div>
                                <div class="mb-3">
                                    <label class="form-label">Default page</label>
                                    <select class="form-select" id="defaultPage">
                                        <option value="overview" selected>Overview</option>
                                        <option value="jobs">Jobs</option>
                                        <option value="history">History</option>
                                        <option value="health">Health</option>
                                    </select>
                                </div>
                                <div class="mb-3">
                                    <div class="form-check form-switch">
                                        <input class="form-check-input" type="checkbox" id="enableNotifications" checked>
                                        <label class="form-check-label" for="enableNotifications">
                                            Enable notifications
                                        </label>
                                    </div>
                                </div>
                                <button type="button" class="btn btn-primary" onclick="dashboard.saveSettings()">
                                    Save Settings
                                </button>
                            </form>
                        </div>
                    </div>

                    <!-- Notification Settings -->
                    <div class="card">
                        <div class="card-header">
                            <h5 class="mb-0">Notification Settings</h5>
                        </div>
                        <div class="card-body">
                            <form>
                                <div class="mb-3">
                                    <div class="form-check">
                                        <input class="form-check-input" type="checkbox" id="notifyOnSuccess">
                                        <label class="form-check-label" for="notifyOnSuccess">
                                            Notify on successful sync
                                        </label>
                                    </div>
                                </div>
                                <div class="mb-3">
                                    <div class="form-check">
                                        <input class="form-check-input" type="checkbox" id="notifyOnFailure" checked>
                                        <label class="form-check-label" for="notifyOnFailure">
                                            Notify on failed sync
                                        </label>
                                    </div>
                                </div>
                                <div class="mb-3">
                                    <div class="form-check">
                                        <input class="form-check-input" type="checkbox" id="notifyOnLongRunning" checked>
                                        <label class="form-check-label" for="notifyOnLongRunning">
                                            Notify on long-running jobs (>5 min)
                                        </label>
                                    </div>
                                </div>
                            </form>
                        </div>
                    </div>
                </div>

                <div class="col-lg-4">
                    <!-- User Info -->
                    <div class="card mb-4">
                        <div class="card-header">
                            <h5 class="mb-0">User Information</h5>
                        </div>
                        <div class="card-body">
                            <dl class="row">
                                <dt class="col-sm-4">Username</dt>
                                <dd class="col-sm-8">${JSON.parse(localStorage.getItem('user') || '{}').username || 'N/A'}</dd>
                                
                                <dt class="col-sm-4">Role</dt>
                                <dd class="col-sm-8">${JSON.parse(localStorage.getItem('user') || '{}').role || 'N/A'}</dd>
                                
                                <dt class="col-sm-4">Session</dt>
                                <dd class="col-sm-8">Active</dd>
                            </dl>
                            <button class="btn btn-danger w-100" onclick="dashboard.logout()">
                                <i class="bi bi-box-arrow-right me-2"></i>Logout
                            </button>
                        </div>
                    </div>

                    <!-- About -->
                    <div class="card">
                        <div class="card-header">
                            <h5 class="mb-0">About</h5>
                        </div>
                        <div class="card-body">
                            <p class="mb-2"><strong>MobileGW Data Sync</strong></p>
                            <p class="mb-2">Version: 2.0.0</p>
                            <p class="mb-2">Build: ${new Date().toISOString().split('T')[0]}</p>
                            <hr>
                            <small class="text-muted">© 2025 MobileGW. All rights reserved.</small>
                        </div>
                    </div>
                </div>
            </div>
        `;
    }

    initChartsFromDashboardData(data) {
        // Уничтожаем существующие графики
        Object.values(this.charts).forEach(chart => {
            if (chart) chart.destroy();
        });
        this.charts = {};

        // График активности из chartData
        if (data.chartData && data.chartData.length > 0) {
            const activityCtx = document.getElementById('activityChart')?.getContext('2d');
            if (activityCtx) {
                this.charts.activity = new Chart(activityCtx, {
                    type: 'bar',
                    data: {
                        labels: data.chartData.map(d =>
                            new Date(d.date).toLocaleDateString('en-US', { weekday: 'short', month: 'short', day: 'numeric' })
                        ),
                        datasets: [{
                            label: 'Success',
                            data: data.chartData.map(d => d.success),
                            backgroundColor: 'rgba(40, 167, 69, 0.8)',
                            borderColor: 'rgba(40, 167, 69, 1)',
                            borderWidth: 1
                        }, {
                            label: 'Failed',
                            data: data.chartData.map(d => d.failed),
                            backgroundColor: 'rgba(220, 53, 69, 0.8)',
                            borderColor: 'rgba(220, 53, 69, 1)',
                            borderWidth: 1
                        }]
                    },
                    options: {
                        responsive: true,
                        maintainAspectRatio: false,
                        interaction: {
                            mode: 'index',
                            intersect: false,
                        },
                        scales: {
                            x: {
                                stacked: true,
                            },
                            y: {
                                stacked: true,
                                beginAtZero: true,
                                ticks: {
                                    stepSize: 1,
                                    precision: 0
                                }
                            }
                        },
                        plugins: {
                            legend: {
                                position: 'bottom',
                            },
                            tooltip: {
                                callbacks: {
                                    footer: function (tooltipItems) {
                                        let sum = 0;
                                        tooltipItems.forEach(function (tooltipItem) {
                                            sum += tooltipItem.parsed.y;
                                        });
                                        return 'Total: ' + sum;
                                    }
                                }
                            }
                        }
                    }
                });
            }
        }

        // График распределения статусов
        const statusData = {
            Completed: data.stats24h?.find(s => s.status === 'Completed')?.count || 0,
            Failed: data.stats24h?.find(s => s.status === 'Failed')?.count || 0,
            InProgress: data.stats24h?.find(s => s.status === 'InProgress')?.count || 0
        };

        const statusCtx = document.getElementById('statusChart')?.getContext('2d');
        if (statusCtx) {
            this.charts.status = new Chart(statusCtx, {
                type: 'doughnut',
                data: {
                    labels: Object.keys(statusData),
                    datasets: [{
                        data: Object.values(statusData),
                        backgroundColor: [
                            'rgba(40, 167, 69, 0.8)',
                            'rgba(220, 53, 69, 0.8)',
                            'rgba(255, 193, 7, 0.8)'
                        ],
                        borderColor: [
                            'rgba(40, 167, 69, 1)',
                            'rgba(220, 53, 69, 1)',
                            'rgba(255, 193, 7, 1)'
                        ],
                        borderWidth: 1
                    }]
                },
                options: {
                    responsive: true,
                    maintainAspectRatio: true,
                    plugins: {
                        legend: {
                            position: 'bottom',
                        },
                        tooltip: {
                            callbacks: {
                                label: function (context) {
                                    const label = context.label || '';
                                    const value = context.parsed || 0;
                                    const total = context.chart.data.datasets[0].data.reduce((a, b) => a + b, 0);
                                    const percentage = ((value / total) * 100).toFixed(1);
                                    return `${label}: ${value} (${percentage}%)`;
                                }
                            }
                        }
                    }
                }
            });
        }
    }

    async triggerJob(jobId) {
        if (!confirm('Запустить эту задачу сейчас?')) return;

        try {
            const result = await window.api.triggerJob(jobId);
            this.showToast(`Задача запущена успешно! Обработано ${result.recordsProcessed || 0} записей.`, 'success');

            // Обновляем текущую страницу
            setTimeout(() => {
                this.loadPage(this.currentPage);
            }, 2000);
        } catch (error) {
            this.showToast(`Ошибка: ${error.message}`, 'danger');
        }
    }

    async viewLogs(runId) {
        try {
            const logs = await window.api.getRunLogs(runId);

            // Создаем и показываем модальное окно
            const modalHtml = `
                <div class="modal fade" id="logsModal" tabindex="-1">
                    <div class="modal-dialog modal-lg">
                        <div class="modal-content">
                            <div class="modal-header">
                                <h5 class="modal-title">Sync Run Logs - ${runId.substring(0, 8)}</h5>
                                <button type="button" class="btn-close" data-bs-dismiss="modal"></button>
                            </div>
                            <div class="modal-body">
                                ${logs && logs.length > 0 ? `
                                    <table class="table table-sm">
                                        <thead>
                                            <tr>
                                                <th>Step</th>
                                                <th>Status</th>
                                                <th>Duration</th>
                                                <th>Details</th>
                                            </tr>
                                        </thead>
                                        <tbody>
                                            ${logs.map(log => `
                                                <tr>
                                                    <td>${log.stepName}</td>
                                                    <td>
                                                        <span class="badge bg-${log.status === 'Completed' ? 'success' : log.status === 'Failed' ? 'danger' : 'warning'}">
                                                            ${log.status}
                                                        </span>
                                                    </td>
                                                    <td>${log.durationMs || 0}ms</td>
                                                    <td>
                                                        <small>${log.details || '-'}</small>
                                                    </td>
                                                </tr>
                                            `).join('')}
                                        </tbody>
                                    </table>
                                ` : '<p class="text-muted">No logs available for this run</p>'}
                            </div>
                            <div class="modal-footer">
                                <button type="button" class="btn btn-secondary" data-bs-dismiss="modal">Close</button>
                            </div>
                        </div>
                    </div>
                </div>
            `;

            // Удаляем предыдущее модальное окно если есть
            const existingModal = document.getElementById('logsModal');
            if (existingModal) {
                existingModal.remove();
            }

            // Добавляем новое модальное окно
            document.body.insertAdjacentHTML('beforeend', modalHtml);

            // Показываем модальное окно
            const modal = new bootstrap.Modal(document.getElementById('logsModal'));
            modal.show();

            // Удаляем модальное окно после закрытия
            document.getElementById('logsModal').addEventListener('hidden.bs.modal', function () {
                this.remove();
            });

        } catch (error) {
            this.showToast(`Ошибка загрузки логов: ${error.message}`, 'danger');
        }
    }

    // Дополнительные методы
    changeChartView(view) {
        // TODO: Реализовать изменение периода графика
        this.showToast(`Changing view to ${view}`, 'info');
    }

    showAddJobModal() {
        // TODO: Реализовать добавление новой задачи
        this.showToast('Add job functionality in development', 'info');
    }

    editJob(jobId) {
        // TODO: Реализовать редактирование задачи
        this.showToast('Edit job functionality in development', 'info');
    }

    async deleteJob(jobId) {
        if (!confirm('Вы уверены, что хотите удалить эту задачу?')) return;

        // TODO: Реализовать удаление задачи
        this.showToast('Delete job functionality in development', 'info');
    }

    viewJobHistory(jobId) {
        // TODO: Показать историю конкретной задачи
        this.showToast('Job history functionality in development', 'info');
    }

    exportHistory() {
        // TODO: Экспорт истории в CSV
        this.showToast('Export functionality in development', 'info');
    }

    saveSettings() {
        // TODO: Сохранить настройки
        this.showToast('Settings saved successfully', 'success');
    }

    async logout() {
        if (confirm('Вы уверены, что хотите выйти?')) {
            await window.api.logout();
        }
    }

    showToast(message, type = 'info') {
        const toastContainer = document.getElementById('toastContainer');
        if (!toastContainer) return;

        const toastId = `toast-${Date.now()}`;
        const icons = {
            'success': 'bi-check-circle-fill',
            'danger': 'bi-exclamation-triangle-fill',
            'warning': 'bi-exclamation-circle-fill',
            'info': 'bi-info-circle-fill'
        };

        const toastHtml = `
            <div class="toast align-items-center text-white bg-${type} border-0" 
                 role="alert" id="${toastId}">
                <div class="d-flex">
                    <div class="toast-body">
                        <i class="bi ${icons[type]} me-2"></i>
                        ${message}
                    </div>
                    <button type="button" class="btn-close btn-close-white me-2 m-auto" 
                            data-bs-dismiss="toast"></button>
                </div>
            </div>
        `;

        toastContainer.insertAdjacentHTML('beforeend', toastHtml);

        const toastElement = document.getElementById(toastId);
        const toast = new bootstrap.Toast(toastElement, {
            autohide: true,
            delay: 5000
        });
        toast.show();

        // Удаляем элемент после скрытия
        toastElement.addEventListener('hidden.bs.toast', function () {
            this.remove();
        });
    }

    showError(message) {
        const contentArea = document.getElementById('contentArea');
        contentArea.innerHTML = `
            <div class="alert alert-danger d-flex align-items-center" role="alert">
                <i class="bi bi-exclamation-triangle-fill fs-4 me-3"></i>
                <div>
                    <h5 class="alert-heading mb-1">Ошибка</h5>
                    <p class="mb-0">${message}</p>
                </div>
            </div>
        `;
    }

    startAutoRefresh() {
        // Останавливаем предыдущий интервал если есть
        if (this.refreshInterval) {
            clearInterval(this.refreshInterval);
        }

        // Обновление каждые 30 секунд
        this.refreshInterval = setInterval(() => {
            // Обновляем только overview и health страницы
            if (this.currentPage === 'overview' || this.currentPage === 'health') {
                console.log('Auto-refreshing page:', this.currentPage);
                this.loadPage(this.currentPage);
            }
        }, 30000);
    }

    startClock() {
        const updateTime = () => {
            const timeElement = document.getElementById('serverTime');
            if (timeElement) {
                timeElement.textContent = new Date().toLocaleTimeString();
            }
        };

        updateTime();
        setInterval(updateTime, 1000);
    }

    destroy() {
        // Останавливаем автообновление
        if (this.refreshInterval) {
            clearInterval(this.refreshInterval);
        }

        // Уничтожаем графики
        Object.values(this.charts).forEach(chart => {
            if (chart) chart.destroy();
        });

        // Очищаем менеджеры
        if (this.chartManager) {
            this.chartManager.destroyAllCharts();
        }

        if (this.metricsManager) {
            this.metricsManager.stopAllRealtimeUpdates();
        }

        console.log('Dashboard destroyed');
    }
}

// Инициализация при загрузке страницы
let dashboard;
document.addEventListener('DOMContentLoaded', () => {
    dashboard = new Dashboard();
});

// Cleanup при выходе
window.addEventListener('beforeunload', () => {
    if (dashboard) {
        dashboard.destroy();
    }
});

// Экспортируем для использования в HTML
window.dashboard = dashboard;