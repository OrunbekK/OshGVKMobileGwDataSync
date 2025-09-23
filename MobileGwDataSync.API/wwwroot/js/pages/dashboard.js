// dashboard.js - Main dashboard logic
class Dashboard {
    constructor() {
        this.currentPage = 'overview';
        this.refreshInterval = null;
        this.charts = {};

        this.init();
    }

    async init() {
        // Check authentication
        if (!localStorage.getItem('jwtToken')) {
            window.location.href = '/login.html';
            return;
        }

        // Load user info
        this.loadUserInfo();

        // Setup event listeners
        this.setupEventListeners();

        // Load initial page
        await this.loadPage('overview');

        // Start auto-refresh
        this.startAutoRefresh();

        // Start clock
        this.startClock();
    }

    loadUserInfo() {
        const user = JSON.parse(localStorage.getItem('user') || '{}');
        document.getElementById('currentUser').textContent = user.username || 'User';
    }

    setupEventListeners() {
        // Sidebar toggle
        document.getElementById('sidebarToggle').addEventListener('click', () => {
            document.getElementById('sidebar').classList.toggle('collapsed');
        });

        // Navigation
        document.querySelectorAll('.sidebar-nav .nav-link').forEach(link => {
            link.addEventListener('click', (e) => {
                e.preventDefault();
                const page = link.dataset.page;
                this.loadPage(page);

                // Update active state
                document.querySelectorAll('.sidebar-nav .nav-link').forEach(l => l.classList.remove('active'));
                link.classList.add('active');
            });
        });

        // Refresh button
        document.getElementById('refreshBtn').addEventListener('click', () => {
            this.loadPage(this.currentPage);
        });

        // Logout
        document.getElementById('logoutBtn').addEventListener('click', async (e) => {
            e.preventDefault();
            await window.api.logout();
        });
    }

    async loadPage(page) {
        this.currentPage = page;
        const contentArea = document.getElementById('contentArea');

        // Show loading
        contentArea.innerHTML = `
            <div class="text-center py-5">
                <div class="spinner-border text-primary" role="status">
                    <span class="visually-hidden">Loading...</span>
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
                    contentArea.innerHTML = '<p>Page not found</p>';
            }
        } catch (error) {
            this.showError(error.message);
        }
    }

    async loadOverview() {
        const [stats, history] = await Promise.all([
            window.api.getDashboardStats(),
            window.api.getSyncHistory(10)
        ]);

        const contentArea = document.getElementById('contentArea');
        contentArea.innerHTML = `
            <div class="row g-4 mb-4">
                <!-- Metrics Cards -->
                <div class="col-xl-3 col-md-6">
                    <div class="metric-card">
                        <div class="metric-icon bg-primary bg-opacity-10 text-primary">
                            <i class="bi bi-arrow-repeat"></i>
                        </div>
                        <div class="metric-value">${stats.syncMetrics?.totalRuns || 0}</div>
                        <div class="metric-label">Total Runs (24h)</div>
                    </div>
                </div>
                <div class="col-xl-3 col-md-6">
                    <div class="metric-card">
                        <div class="metric-icon bg-success bg-opacity-10 text-success">
                            <i class="bi bi-check-circle"></i>
                        </div>
                        <div class="metric-value">${stats.syncMetrics?.successfulRuns || 0}</div>
                        <div class="metric-label">Successful</div>
                    </div>
                </div>
                <div class="col-xl-3 col-md-6">
                    <div class="metric-card">
                        <div class="metric-icon bg-danger bg-opacity-10 text-danger">
                            <i class="bi bi-x-circle"></i>
                        </div>
                        <div class="metric-value">${stats.syncMetrics?.failedRuns || 0}</div>
                        <div class="metric-label">Failed</div>
                    </div>
                </div>
                <div class="col-xl-3 col-md-6">
                    <div class="metric-card">
                        <div class="metric-icon bg-info bg-opacity-10 text-info">
                            <i class="bi bi-database"></i>
                        </div>
                        <div class="metric-value">${this.formatNumber(stats.syncMetrics?.totalRecordsProcessed || 0)}</div>
                        <div class="metric-label">Records Processed</div>
                    </div>
                </div>
            </div>

            <div class="row g-4">
                <!-- Activity Chart -->
                <div class="col-lg-8">
                    <div class="table-container">
                        <h5 class="mb-3">Sync Activity</h5>
                        <canvas id="activityChart" height="100"></canvas>
                    </div>
                </div>
                
                <!-- Status Chart -->
                <div class="col-lg-4">
                    <div class="table-container">
                        <h5 class="mb-3">Status Distribution</h5>
                        <canvas id="statusChart"></canvas>
                    </div>
                </div>
            </div>

            <!-- Recent Runs Table -->
            <div class="row g-4 mt-1">
                <div class="col-12">
                    <div class="table-container">
                        <h5 class="mb-3">Recent Sync Runs</h5>
                        <div class="table-responsive">
                            <table class="table table-hover">
                                <thead>
                                    <tr>
                                        <th>Job Name</th>
                                        <th>Start Time</th>
                                        <th>Duration</th>
                                        <th>Records</th>
                                        <th>Status</th>
                                    </tr>
                                </thead>
                                <tbody>
                                    ${history.map(run => `
                                        <tr>
                                            <td>${run.jobName}</td>
                                            <td>${new Date(run.startTime).toLocaleString()}</td>
                                            <td>${this.formatDuration(run.duration)}</td>
                                            <td>${this.formatNumber(run.recordsProcessed)}</td>
                                            <td><span class="badge badge-status-${run.status.toLowerCase()}">${run.status}</span></td>
                                        </tr>
                                    `).join('')}
                                </tbody>
                            </table>
                        </div>
                    </div>
                </div>
            </div>
        `;

        // Initialize charts
        this.initCharts(stats);
    }

    async loadJobs() {
        const jobs = await window.api.getJobs();

        const contentArea = document.getElementById('contentArea');
        contentArea.innerHTML = `
            <div class="d-flex justify-content-between align-items-center mb-4">
                <h4>Job Management</h4>
                <button class="btn btn-primary" onclick="dashboard.showAddJobModal()">
                    <i class="bi bi-plus-circle me-2"></i>Add Job
                </button>
            </div>

            <div class="table-container">
                <div class="table-responsive">
                    <table class="table table-hover">
                        <thead>
                            <tr>
                                <th>Job Name</th>
                                <th>Type</th>
                                <th>Schedule</th>
                                <th>Last Run</th>
                                <th>Next Run</th>
                                <th>Status</th>
                                <th>Actions</th>
                            </tr>
                        </thead>
                        <tbody>
                            ${jobs.map(job => `
                                <tr>
                                    <td>${job.name}</td>
                                    <td>${job.jobType}</td>
                                    <td><code>${job.cronExpression}</code></td>
                                    <td>${job.lastRunAt ? new Date(job.lastRunAt).toLocaleString() : 'Never'}</td>
                                    <td>${job.nextRunAt ? new Date(job.nextRunAt).toLocaleString() : '-'}</td>
                                    <td>
                                        <div class="form-check form-switch">
                                            <input class="form-check-input" type="checkbox" 
                                                ${job.isEnabled ? 'checked' : ''} 
                                                onchange="dashboard.toggleJob('${job.id}')">
                                        </div>
                                    </td>
                                    <td>
                                        <button class="btn btn-sm btn-primary" onclick="dashboard.triggerJob('${job.id}')">
                                            <i class="bi bi-play-fill"></i>
                                        </button>
                                        <button class="btn btn-sm btn-secondary" onclick="dashboard.editJob('${job.id}')">
                                            <i class="bi bi-pencil"></i>
                                        </button>
                                    </td>
                                </tr>
                            `).join('')}
                        </tbody>
                    </table>
                </div>
            </div>
        `;
    }

    async loadHistory() {
        const history = await window.api.getSyncHistory(100);

        const contentArea = document.getElementById('contentArea');
        contentArea.innerHTML = `
            <h4 class="mb-4">Sync History</h4>

            <div class="table-container">
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
                                    <td><code>${run.id.substring(0, 8)}</code></td>
                                    <td>${run.jobName}</td>
                                    <td>${new Date(run.startTime).toLocaleString()}</td>
                                    <td>${run.endTime ? new Date(run.endTime).toLocaleString() : '-'}</td>
                                    <td>${this.formatDuration(run.duration)}</td>
                                    <td>${this.formatNumber(run.recordsFetched)}</td>
                                    <td>${this.formatNumber(run.recordsProcessed)}</td>
                                    <td><span class="badge badge-status-${run.status.toLowerCase()}">${run.status}</span></td>
                                    <td>
                                        <button class="btn btn-sm btn-info" onclick="dashboard.viewRunDetails('${run.id}')">
                                            <i class="bi bi-eye"></i>
                                        </button>
                                    </td>
                                </tr>
                            `).join('')}
                        </tbody>
                    </table>
                </div>
            </div>
        `;
    }

    async loadHealth() {
        const health = await window.api.getHealth();

        const contentArea = document.getElementById('contentArea');
        contentArea.innerHTML = `
            <h4 class="mb-4">System Health</h4>

            <div class="row g-4">
                ${Object.entries(health.checks || {}).map(([key, value]) => `
                    <div class="col-md-4">
                        <div class="metric-card">
                            <div class="d-flex justify-content-between align-items-start mb-3">
                                <h6 class="text-uppercase">${key}</h6>
                                <span class="badge bg-${this.getHealthBadgeClass(value.status)}">${value.status}</span>
                            </div>
                            <p class="text-muted mb-2">${value.description}</p>
                            <small class="text-muted">Response time: ${value.responseTime}ms</small>
                        </div>
                    </div>
                `).join('')}
            </div>
        `;
    }

    loadSettings() {
        const contentArea = document.getElementById('contentArea');
        contentArea.innerHTML = `
            <h4 class="mb-4">Settings</h4>
            <div class="table-container">
                <p>Settings page coming soon...</p>
            </div>
        `;
    }

    // Helper methods
    initCharts(stats) {
        // Activity Chart
        const activityCtx = document.getElementById('activityChart')?.getContext('2d');
        if (activityCtx) {
            this.charts.activity = new Chart(activityCtx, {
                type: 'line',
                data: {
                    labels: ['Mon', 'Tue', 'Wed', 'Thu', 'Fri', 'Sat', 'Sun'],
                    datasets: [{
                        label: 'Successful',
                        data: [12, 19, 3, 5, 2, 3, 10],
                        borderColor: 'rgb(75, 192, 192)',
                        tension: 0.1
                    }, {
                        label: 'Failed',
                        data: [2, 3, 1, 0, 1, 0, 2],
                        borderColor: 'rgb(255, 99, 132)',
                        tension: 0.1
                    }]
                },
                options: {
                    responsive: true,
                    maintainAspectRatio: false
                }
            });
        }

        // Status Chart
        const statusCtx = document.getElementById('statusChart')?.getContext('2d');
        if (statusCtx) {
            this.charts.status = new Chart(statusCtx, {
                type: 'doughnut',
                data: {
                    labels: ['Completed', 'Failed', 'In Progress'],
                    datasets: [{
                        data: [
                            stats.syncMetrics?.successfulRuns || 0,
                            stats.syncMetrics?.failedRuns || 0,
                            0
                        ],
                        backgroundColor: [
                            'rgba(40, 167, 69, 0.8)',
                            'rgba(220, 53, 69, 0.8)',
                            'rgba(255, 193, 7, 0.8)'
                        ]
                    }]
                },
                options: {
                    responsive: true,
                    maintainAspectRatio: true
                }
            });
        }
    }

    async triggerJob(jobId) {
        if (!confirm('Are you sure you want to trigger this job?')) return;

        try {
            const result = await window.api.triggerJob(jobId);
            this.showToast('Job triggered successfully', 'success');
            this.loadPage(this.currentPage);
        } catch (error) {
            this.showToast(error.message, 'danger');
        }
    }

    formatNumber(num) {
        return new Intl.NumberFormat().format(num);
    }

    formatDuration(seconds) {
        if (!seconds) return '-';
        if (seconds < 60) return `${seconds.toFixed(1)}s`;
        if (seconds < 3600) return `${(seconds / 60).toFixed(1)}m`;
        return `${(seconds / 3600).toFixed(1)}h`;
    }

    getHealthBadgeClass(status) {
        const map = {
            'Healthy': 'success',
            'Degraded': 'warning',
            'Unhealthy': 'danger'
        };
        return map[status] || 'secondary';
    }

    showToast(message, type = 'info') {
        const toastContainer = document.getElementById('toastContainer');
        const toastId = `toast-${Date.now()}`;

        toastContainer.innerHTML += `
            <div class="toast" role="alert" id="${toastId}">
                <div class="toast-header">
                    <strong class="me-auto">Notification</strong>
                    <button type="button" class="btn-close" data-bs-dismiss="toast"></button>
                </div>
                <div class="toast-body">
                    ${message}
                </div>
            </div>
        `;

        const toast = new bootstrap.Toast(document.getElementById(toastId));
        toast.show();
    }

    showError(message) {
        const contentArea = document.getElementById('contentArea');
        contentArea.innerHTML = `
            <div class="alert alert-danger" role="alert">
                <i class="bi bi-exclamation-triangle-fill me-2"></i>
                ${message}
            </div>
        `;
    }

    startAutoRefresh() {
        // Refresh every 30 seconds
        this.refreshInterval = setInterval(() => {
            this.loadPage(this.currentPage);
        }, 30000);
    }

    startClock() {
        setInterval(() => {
            document.getElementById('serverTime').textContent = new Date().toLocaleTimeString();
        }, 1000);
    }
}

// Initialize dashboard on page load
let dashboard;
document.addEventListener('DOMContentLoaded', () => {
    dashboard = new Dashboard();
});