// dashboard.js - Полная версия для работы с DashboardController
class Dashboard {
    constructor() {
        this.currentPage = 'overview';
        this.refreshInterval = null;
        this.charts = {};
        this.chartManager = new ChartManager();
        this.tableManager = new TableManager();
        this.metricsManager = new MetricsManager();
        this.isRedirecting = false;

        this.init();
    }

    async init() {
        // Проверка авторизации
        const token = localStorage.getItem('jwtToken');

        if (!token) {
            this.redirectToLogin();
            return;
        }

        // Проверяем валидность токена
        try {
            const response = await fetch('/dashboard/data', {
                method: 'GET',
                headers: {
                    'Authorization': `Bearer ${token}`
                }
            });

            if (!response.ok) {
                if (response.status === 401) {
                    // Токен невалидный
                    //console.log('Token invalid or expired');
                    localStorage.removeItem('jwtToken');
                    localStorage.removeItem('user');
                    this.redirectToLogin();
                    return;
                }
            }
        } catch (error) {
            console.error('Auth check failed:', error);
            // При ошибке сети позволяем остаться на странице
            this.showToast('Ошибка проверки авторизации', 'warning');
        }

        // Если все ок, продолжаем инициализацию
        this.loadUserInfo();
        this.setupEventListeners();
        await this.loadPage('overview');
        this.startAutoRefresh();
        this.startClock();
    }

    redirectToLogin() {
        if (!this.isRedirecting) {
            this.isRedirecting = true;
            window.location.href = '/login.html';
        }
    }

    loadUserInfo() {
        const user = JSON.parse(localStorage.getItem('user') || '{}');
        // Не отображаем имя пользователя в sidebar, так как sidebar-footer удален
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

        // Кнопка выхода в header
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
            // Ctrl+K - фокус на поиске (если будет реализован)
            if (e.ctrlKey && e.key === 'k') {
                e.preventDefault();
                // TODO: Фокус на поиске
            }
        });

        // Закрытие sidebar при клике вне его на мобильных устройствах
        document.addEventListener('click', (e) => {
            const sidebar = document.getElementById('sidebar');
            const sidebarToggle = document.getElementById('sidebarToggle');

            if (window.innerWidth < 992) {
                if (!sidebar.contains(e.target) && !sidebarToggle.contains(e.target)) {
                    sidebar.classList.remove('show');
                }
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
            
            <!-- Верхний ряд: 3 метрики + круговая диаграмма -->
            <div class="row g-4 mb-4">
                <div class="col-xl-3 col-lg-6 col-md-6">
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
                
                <div class="col-xl-3 col-lg-6 col-md-6">
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
                
                <div class="col-xl-3 col-lg-6 col-md-6">
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
                
                <!-- Status Distribution как 4-й элемент верхнего ряда -->
                <div class="col-xl-3 col-lg-6 col-md-6">
                    <div class="card h-100">
                        <div class="card-body p-3">
                            <h6 class="mb-2">Status Distribution (24h)</h6>
                            <canvas id="statusChart" style="max-height: 150px;"></canvas>
                            <div class="mt-2 text-center">
                                <small class="text-muted">Total: ${totalRuns} runs</small>
                            </div>
                        </div>
                    </div>
                </div>
            </div>

            <!-- График активности на всю ширину -->
            <div class="row g-4 mb-4">
                <div class="col-12">
                    <div class="card">
                        <div class="card-header d-flex justify-content-between align-items-center">
                            <h5 class="mb-0">Sync Activity (Last 7 Days)</h5>
                            <div class="btn-group btn-group-sm" role="group">
                                <button type="button" class="btn btn-outline-secondary active" onclick="dashboard.changeChartView('week')">Week</button>
                                <button type="button" class="btn btn-outline-secondary" onclick="dashboard.changeChartView('month')">Month</button>
                            </div>
                        </div>
                        <div class="card-body">
                            <canvas id="activityChart" height="80"></canvas>
                        </div>
                    </div>
                </div>
            </div>

            <!-- Три таблицы в ряд -->
            <div class="row g-4">
                <!-- Active Jobs -->
                <div class="col-lg-4">
                    <div class="card h-100">
                        <div class="card-header d-flex justify-content-between">
                            <h6 class="mb-0">Active Jobs</h6>
                            <a href="#" onclick="dashboard.loadPage('jobs'); return false;" class="text-decoration-none small">View all</a>
                        </div>
                        <div class="card-body">
                            <div class="table-responsive">
                                <table class="table table-hover table-sm">
                                    <thead>
                                        <tr>
                                            <th>Job</th>
                                            <th>Next Run</th>
                                            <th></th>
                                        </tr>
                                    </thead>
                                    <tbody>
                                        ${(data.activeJobs || []).slice(0, 5).map(job => `
                                            <tr>
                                                <td>
                                                    <small class="fw-medium">${job.name}</small>
                                                </td>
                                                <td>
                                                    <small class="text-muted">${job.nextRunAt ? Utils.getRelativeTime(job.nextRunAt) : 'N/A'}</small>
                                                </td>
                                                <td>
                                                    <button class="btn btn-sm btn-primary p-1" onclick="dashboard.triggerJob('${job.id}')" title="Run">
                                                        <i class="bi bi-play-fill"></i>
                                                    </button>
                                                </td>
                                            </tr>
                                        `).join('')}
                                    </tbody>
                                </table>
                                ${data.activeJobs?.length === 0 ? '<p class="text-muted text-center small">No active jobs</p>' : ''}
                            </div>
                        </div>
                    </div>
                </div>

                <!-- Recent Sync Runs -->
                <div class="col-lg-4">
                    <div class="card h-100">
                        <div class="card-header d-flex justify-content-between">
                            <h6 class="mb-0">Recent Runs</h6>
                            <a href="#" onclick="dashboard.loadPage('history'); return false;" class="text-decoration-none small">View all</a>
                        </div>
                        <div class="card-body">
                            <div class="table-responsive">
                                <table class="table table-hover table-sm">
                                    <thead>
                                        <tr>
                                            <th>Job</th>
                                            <th>Records</th>
                                            <th>Status</th>
                                        </tr>
                                    </thead>
                                    <tbody>
                                        ${(data.recentRuns || []).slice(0, 5).map(run => `
                                            <tr>
                                                <td>
                                                    <small class="fw-medium">${run.jobName}</small>
                                                    <br>
                                                    <small class="text-muted">${Utils.getRelativeTime(run.startTime)}</small>
                                                </td>
                                                <td>
                                                    <small>${Utils.formatNumber(run.recordsProcessed)}</small>
                                                </td>
                                                <td>
                                                    <span class="badge bg-${run.status === 'Completed' ? 'success' : run.status === 'Failed' ? 'danger' : 'warning'} small">
                                                        ${run.status}
                                                    </span>
                                                </td>
                                            </tr>
                                        `).join('')}
                                    </tbody>
                                </table>
                                ${data.recentRuns?.length === 0 ? '<p class="text-muted text-center small">No recent runs</p>' : ''}
                            </div>
                        </div>
                    </div>
                </div>

                <!-- Failed Runs -->
                <div class="col-lg-4">
                    <div class="card h-100">
                        <div class="card-header d-flex justify-content-between">
                            <h6 class="mb-0">Failed Runs</h6>
                            <span class="badge bg-danger">${failedCount}</span>
                        </div>
                        <div class="card-body">
                            <div class="table-responsive">
                                <table class="table table-hover table-sm">
                                    <thead>
                                        <tr>
                                            <th>Job</th>
                                            <th>Time</th>
                                            <th>Error</th>
                                        </tr>
                                    </thead>
                                    <tbody>
                                        ${(() => {
                                    const failedRuns = (data.recentRuns || []).filter(r => r.status === 'Failed');

                                    if (failedRuns.length > 0) {
                                        return failedRuns.slice(0, 5).map(run => `
                                                    <tr>
                                                        <td>
                                                            <small class="fw-medium">${run.jobName}</small>
                                                        </td>
                                                        <td>
                                                            <small class="text-muted">${Utils.getRelativeTime(run.startTime)}</small>
                                                        </td>
                                                        <td>
                                                            <small class="text-danger" title="${run.errorMessage || 'Unknown error'}">
                                                                ${run.errorMessage ? (run.errorMessage.substring(0, 25) + '...') : 'Unknown'}
                                                            </small>
                                                        </td>
                                                    </tr>
                                                `).join('');
                                    } else if (failedCount > 0) {
                                        // Есть ошибки за 24 часа, но не в последних 10 запусках
                                        return `
                                                    <tr>
                                                        <td colspan="3" class="text-center text-muted small py-3">
                                                            <i class="bi bi-info-circle me-1"></i>
                                                            ${failedCount} failed runs in last 24h,<br>
                                                            but none in recent 10 runs
                                                        </td>
                                                    </tr>
                                                `;
                                    } else {
                                        // Нет ошибок вообще
                                        return `
                                                    <tr>
                                                        <td colspan="3" class="text-center text-success small py-3">
                                                            <i class="bi bi-check-circle me-1"></i>
                                                            No failed runs
                                                        </td>
                                                    </tr>
                                                `;
                                    }
                                })()}
                                    </tbody>
                                </table>
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
                                <div class="text-center">
                                    <h6>CPU Usage</h6>
                                    <canvas id="cpuGauge"></canvas>
                                </div>
                            </div>
                            <div class="col-md-4">
                                <div class="text-center">
                                    <h6>Memory Usage</h6>
                                    <canvas id="memoryGauge"></canvas>
                                </div>
                            </div>
                            <div class="col-md-4">
                                <div class="text-center">
                                    <h6>Disk Usage</h6>
                                    <canvas id="diskGauge"></canvas>
                                </div>
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
        const user = JSON.parse(localStorage.getItem('user') || '{}');

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
                                <dt class="col-sm-5">Username:</dt>
                                <dd class="col-sm-7">${user.username || 'N/A'}</dd>
                                
                                <dt class="col-sm-5">Role:</dt>
                                <dd class="col-sm-7">
                                    <span class="badge bg-primary">${user.role || 'N/A'}</span>
                                </dd>
                                
                                <dt class="col-sm-5">Session:</dt>
                                <dd class="col-sm-7">
                                    <span class="badge bg-success">Active</span>
                                </dd>
                            </dl>
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
                            <small class="text-muted">© 2024 MobileGW. All rights reserved.</small>
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

    async checkQueueStatus() {
        try {
            const response = await fetch('/dashboard/queue-status', {
                headers: {
                    'Authorization': `Bearer ${localStorage.getItem('jwtToken')}`
                }
            });

            if (response.ok) {
                const queueInfo = await response.json();
                //console.log('Queue status:', queueInfo);
            }
        } catch (error) {
            console.error('Error checking queue status:', error);
        }
    }

    async triggerJob(jobId) {
        //console.log('=== TRIGGER JOB DEBUG ===');
        //console.log('Job ID:', jobId);
        //console.log('Token exists:', !!localStorage.getItem('jwtToken'));
        const user = JSON.parse(localStorage.getItem('user') || '{}');
        //console.log('User role:', user.role);

        if (!jobId) {
            this.showToast('Error: Job ID is missing', 'danger');
            return;
        }

        if (!confirm('Запустить эту задачу сейчас?')) return;

        // Получаем имя задачи для мониторинга
        const jobName = document.querySelector(`[data-job-id="${jobId}"] .job-name`)?.textContent || jobId;

        try {
            //console.log('Sending request to:', `/dashboard/trigger/${jobId}`);
            const result = await window.api.triggerJob(jobId);
            //console.log('Response:', result);

            if (result.success) {
                // Разные уведомления в зависимости от метода выполнения
                if (result.method === 'redis_queue') {
                    this.showToast('Задача поставлена в очередь выполнения', 'info');

                    // Добавляем задачу в монитор если он доступен
                    if (window.jobMonitor) {
                        window.jobMonitor.addJob(jobId, jobName);
                    }

                    // Начинаем отслеживать статус задачи
                    this.startJobTracking(jobId);

                } else if (result.method === 'direct') {
                    // Прямое выполнение - показываем детали
                    if (result.details) {
                        const message = `Задача выполнена напрямую (Redis недоступен)
                        Обработано: ${result.details.recordsProcessed} записей
                        Ошибки: ${result.details.recordsFailed || 0}
                        Время: ${result.details.duration.toFixed(2)} сек`;

                        this.showToast(message.replace(/\s+/g, ' '), 'success', 5000);

                        // Если были ошибки, показываем их
                        if (result.errors && result.errors.length > 0) {
                            console.error('Execution errors:', result.errors);
                            this.showToast(`Обнаружены ошибки: ${result.errors.length}`, 'warning');
                        }
                    } else {
                        this.showToast('Задача выполнена успешно', 'success');
                    }

                    // Обновляем дашборд через 2 секунды
                    setTimeout(() => this.loadPage(this.currentPage), 2000);
                }
            } else {
                // Обработка неуспешного результата
                this.showToast(result.message || 'Ошибка выполнения задачи', 'danger');

                if (result.errors && result.errors.length > 0) {
                    console.error('Job errors:', result.errors);
                    // Показываем первую ошибку пользователю
                    if (result.errors[0]) {
                        this.showToast(`Ошибка: ${result.errors[0]}`, 'danger', 5000);
                    }
                }
            }

        } catch (error) {
            console.error('Full error:', error);

            // Обработка различных типов ошибок
            if (error.message.includes('Forbidden') || error.message.includes('403')) {
                this.showToast('У вас нет прав для запуска задач', 'warning');
            } else if (error.message.includes('already running')) {
                this.showToast('Задача уже выполняется', 'warning');
            } else if (error.message.includes('Unauthorized') || error.message.includes('401')) {
                this.showToast('Сессия истекла. Необходима повторная авторизация', 'danger');
                setTimeout(() => window.location.href = '/login', 2000);
            } else {
                this.showToast(`Ошибка: ${error.message}`, 'danger');
            }
        }
    }

    // Новый метод для отслеживания статуса задачи
    async startJobTracking(jobId) {
        let attempts = 0;
        const maxAttempts = 30; // Максимум 30 попыток (1 минута при интервале 2 сек)

        const checkInterval = setInterval(async () => {
            attempts++;

            try {
                const response = await fetch(`/dashboard/status/${jobId}`, {
                    headers: {
                        'Authorization': `Bearer ${localStorage.getItem('jwtToken')}`
                    }
                });

                if (response.ok) {
                    const status = await response.json();
                    //console.log(`Job ${jobId} status:`, status.status);

                    // Обновляем UI если есть элемент задачи
                    const jobElement = document.querySelector(`[data-job-id="${jobId}"]`);
                    if (jobElement) {
                        const statusBadge = jobElement.querySelector('.status-badge, .job-status');
                        if (statusBadge) {
                            statusBadge.textContent = status.status;
                            statusBadge.className = `status-badge status-${status.status}`;
                        }
                    }

                    // Проверяем завершение
                    if (status.status === 'idle' && status.lastRun?.EndTime) {
                        clearInterval(checkInterval);

                        // Показываем результат
                        if (status.lastRun.Status === 'Completed') {
                            this.showToast(
                                `Задача завершена! Обработано: ${status.lastRun.RecordsProcessed} записей`,
                                'success'
                            );
                        } else if (status.lastRun.Status === 'Failed') {
                            this.showToast(
                                `Задача завершена с ошибкой: ${status.lastRun.ErrorMessage || 'Неизвестная ошибка'}`,
                                'danger'
                            );
                        }

                        // Обновляем страницу
                        this.loadPage(this.currentPage);
                    }

                    // Останавливаем проверку если превысили лимит попыток
                    if (attempts >= maxAttempts) {
                        clearInterval(checkInterval);
                        //console.log('Job tracking timeout reached');
                    }
                }
            } catch (error) {
                console.error('Error checking job status:', error);
                // Продолжаем проверку даже при ошибке
            }
        }, 2000); // Проверяем каждые 2 секунды
    }

    // Улучшенный метод показа уведомлений с поддержкой длительности
    showToast(message, type = 'info', duration = 3000) {
        const toastContainer = document.getElementById('toastContainer');
        const toastId = `toast-${Date.now()}`;

        const toastHtml = `
        <div id="${toastId}" class="toast job-notification" role="alert" data-bs-autohide="true" data-bs-delay="${duration}">
            <div class="toast-header bg-${type === 'danger' ? 'danger' : type === 'success' ? 'success' : type === 'warning' ? 'warning' : 'info'} text-white">
                <i class="bi bi-${type === 'danger' ? 'exclamation-circle' : type === 'success' ? 'check-circle' : type === 'warning' ? 'exclamation-triangle' : 'info-circle'} me-2"></i>
                <strong class="me-auto">System</strong>
                <small>${new Date().toLocaleTimeString()}</small>
                <button type="button" class="btn-close btn-close-white" data-bs-dismiss="toast"></button>
            </div>
            <div class="toast-body">
                ${message}
            </div>
        </div>
    `;

        toastContainer.insertAdjacentHTML('beforeend', toastHtml);

        const toastElement = document.getElementById(toastId);
        const toast = new bootstrap.Toast(toastElement);
        toast.show();

        // Удаляем элемент после скрытия
        toastElement.addEventListener('hidden.bs.toast', () => {
            toastElement.remove();
        });
    }

    updateJobStatusUI(jobId, status) {
        const jobRow = document.querySelector(`[data-job-id="${jobId}"]`);
        if (jobRow) {
            const statusBadge = jobRow.querySelector('.status-badge');
            if (statusBadge) {
                statusBadge.textContent = status.status;
                statusBadge.className = `status-badge status-${status.status}`;
            }
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
        const refreshInterval = document.getElementById('refreshInterval').value;
        const defaultPage = document.getElementById('defaultPage').value;
        const enableNotifications = document.getElementById('enableNotifications').checked;

        // Сохраняем в localStorage
        localStorage.setItem('dashboardSettings', JSON.stringify({
            refreshInterval,
            defaultPage,
            enableNotifications
        }));

        // Применяем настройки
        if (refreshInterval === '0') {
            if (this.refreshInterval) {
                clearInterval(this.refreshInterval);
                this.refreshInterval = null;
            }
        } else {
            this.startAutoRefresh(parseInt(refreshInterval));
        }

        this.showToast('Settings saved successfully', 'success');
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

    startAutoRefresh(interval = null) {
        // Останавливаем предыдущий интервал если есть
        if (this.refreshInterval) {
            clearInterval(this.refreshInterval);
        }

        // Получаем интервал из настроек если не передан
        if (!interval) {
            const settings = JSON.parse(localStorage.getItem('dashboardSettings') || '{}');
            interval = parseInt(settings.refreshInterval) || 30000;
        }

        // Не запускаем если интервал 0 (отключено)
        if (interval === 0) return;

        // Обновление с заданным интервалом
        this.refreshInterval = setInterval(() => {
            // Обновляем только overview и health страницы
            if (this.currentPage === 'overview' || this.currentPage === 'health') {
                //console.log('Auto-refreshing page:', this.currentPage);
                this.loadPage(this.currentPage);
            }
        }, interval);
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

        setInterval(() => this.checkQueueStatus(), 5000);
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

        //console.log('Dashboard destroyed');
    }
}

// Глобальная переменная для dashboard
let dashboard;

// Инициализация при загрузке страницы
document.addEventListener('DOMContentLoaded', () => {
    dashboard = new Dashboard();
});

// Cleanup при выходе
window.addEventListener('beforeunload', () => {
    if (dashboard) {
        dashboard.destroy();
    }
});

// Экспортируем для использования в HTML через onclick
window.dashboard = dashboard;