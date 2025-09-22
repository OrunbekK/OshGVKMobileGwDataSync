using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using MobileGwDataSync.Core.Interfaces;
using MobileGwDataSync.Data.Context;
using System.Security.Claims;

namespace MobileGwDataSync.API.Controllers
{
    [ApiController]
    [Route("[controller]")]
    public class DashboardController : ControllerBase
    {
        private readonly ServiceDbContext _context;
        private readonly IConfiguration _configuration;
        private readonly ISyncService _syncService;
        private readonly ILogger<DashboardController> _logger;

        public DashboardController(
            ServiceDbContext context,
            IConfiguration configuration,
            ISyncService syncService,
            ILogger<DashboardController> logger)
        {
            _context = context;
            _configuration = configuration;
            _syncService = syncService;
            _logger = logger;
        }

        [HttpGet]
        [AllowAnonymous] // HTML страница доступна всем
        [Produces("text/html")]
        public IActionResult Index()
        {
            var html = GetDashboardHtml();
            return Content(html, "text/html");
        }

        [HttpGet("data")]
        [Authorize] // Требует JWT токен
        public async Task<IActionResult> GetDashboardData()
        {
            // Логируем пользователя
            var username = User.Identity?.Name;
            _logger.LogInformation("Dashboard data requested by user: {User}", username);

            var now = DateTime.UtcNow;
            var last24Hours = now.AddHours(-24);
            var last7Days = now.AddDays(-7);

            // Последние запуски
            var recentRuns = await _context.SyncRuns
                .Where(r => r.StartTime >= last24Hours)
                .OrderByDescending(r => r.StartTime)
                .Take(10)
                .Select(r => new
                {
                    r.Id,
                    r.JobId,
                    JobName = r.Job.Name,
                    r.StartTime,
                    r.EndTime,
                    r.Status,
                    r.RecordsProcessed,
                    Duration = r.EndTime.HasValue
                        ? (r.EndTime.Value - r.StartTime).TotalSeconds
                        : 0
                })
                .ToListAsync();

            // Статистика за 24 часа
            var stats24h = await _context.SyncRuns
                .Where(r => r.StartTime >= last24Hours)
                .GroupBy(r => r.Status)
                .Select(g => new
                {
                    Status = g.Key,
                    Count = g.Count()
                })
                .ToListAsync();

            // График за 7 дней
            var chartData = await _context.SyncRuns
                .Where(r => r.StartTime >= last7Days)
                .GroupBy(r => r.StartTime.Date)
                .Select(g => new
                {
                    Date = g.Key,
                    Total = g.Count(),
                    Success = g.Count(r => r.Status == "Completed"),
                    Failed = g.Count(r => r.Status == "Failed"),
                    Records = g.Sum(r => r.RecordsProcessed)
                })
                .OrderBy(x => x.Date)
                .ToListAsync();

            // Активные задачи
            var activeJobs = await _context.SyncJobs
                .Where(j => j.IsEnabled)
                .Select(j => new
                {
                    j.Id,
                    j.Name,
                    j.CronExpression,
                    j.LastRunAt,
                    j.NextRunAt,
                    LastRunStatus = j.Runs
                        .OrderByDescending(r => r.StartTime)
                        .Select(r => r.Status)
                        .FirstOrDefault()
                })
                .ToListAsync();

            return Ok(new
            {
                recentRuns,
                stats24h,
                chartData,
                activeJobs,
                serverTime = now,
                user = username
            });
        }

        [HttpPost("trigger/{jobId}")]
        [Authorize(Policy = "DashboardUser")] // Требует роль Dashboard пользователя
        public async Task<IActionResult> TriggerJob(string jobId)
        {
            // Проверяем роль пользователя
            var userRole = User.FindFirst(ClaimTypes.Role)?.Value;
            if (userRole == "Viewer")
            {
                return Forbid("Viewers cannot trigger jobs");
            }

            try
            {
                var job = await _context.SyncJobs.FindAsync(jobId);
                if (job == null)
                    return NotFound(new { message = "Job not found" });

                // Очистка зависших задач
                var staleTime = DateTime.UtcNow.AddMinutes(-30);
                var staleJobs = await _context.SyncRuns
                    .Where(r => r.JobId == jobId && r.Status == "InProgress" && r.StartTime < staleTime)
                    .ToListAsync();

                foreach (var staleJob in staleJobs)
                {
                    staleJob.Status = "Failed";
                    staleJob.EndTime = DateTime.UtcNow;
                    staleJob.ErrorMessage = "Job terminated due to timeout";
                }

                await _context.SaveChangesAsync();

                // Проверка актуальных задач
                var runningJob = await _context.SyncRuns
                    .Where(r => r.JobId == jobId && r.Status == "InProgress")
                    .AnyAsync();

                if (runningJob)
                    return BadRequest(new { message = "Job is already running" });

                _logger.LogInformation("User {User} triggered job {JobId}", User.Identity?.Name, jobId);

                // Запускаем синхронизацию
                var result = await _syncService.ExecuteSyncAsync(jobId);

                return Ok(new
                {
                    message = "Job completed",
                    success = result.Success,
                    recordsProcessed = result.RecordsProcessed,
                    errors = result.Errors
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to trigger job {JobId}", jobId);
                return StatusCode(500, new { message = ex.Message });
            }
        }

        [HttpGet("logs/{runId}")]
        [Authorize]
        public async Task<IActionResult> GetRunLogs(Guid runId)
        {
            var steps = await _context.SyncRunSteps
                .Where(s => s.RunId == runId)
                .OrderBy(s => s.StartTime)
                .Select(s => new
                {
                    s.StepName,
                    s.StartTime,
                    s.EndTime,
                    s.Status,
                    s.Details,
                    s.DurationMs
                })
                .ToListAsync();

            return Ok(steps);
        }

        [HttpGet("health-status")]
        [Authorize]
        public async Task<IActionResult> GetHealthStatus([FromServices] IDataSource dataSource)
        {
            var checks = new Dictionary<string, object>();

            // SQLite check
            try
            {
                await _context.Database.CanConnectAsync();
                var jobCount = await _context.SyncJobs.CountAsync();
                checks["sqlite"] = new
                {
                    status = "healthy",
                    message = $"Connected, {jobCount} jobs configured"
                };
            }
            catch (Exception ex)
            {
                checks["sqlite"] = new { status = "unhealthy", message = ex.Message };
            }

            // 1C check
            try
            {
                var oneCHealthy = await dataSource.TestConnectionAsync();
                checks["onec"] = new
                {
                    status = oneCHealthy ? "healthy" : "unhealthy",
                    message = oneCHealthy ? "1C API accessible" : "Cannot connect to 1C"
                };
            }
            catch (Exception ex)
            {
                checks["onec"] = new { status = "unhealthy", message = ex.Message };
            }

            // Memory check
            var process = System.Diagnostics.Process.GetCurrentProcess();
            var memoryMB = process.WorkingSet64 / (1024 * 1024);
            checks["memory"] = new
            {
                status = memoryMB < 500 ? "healthy" : memoryMB < 1000 ? "degraded" : "unhealthy",
                message = $"{memoryMB} MB used",
                value = memoryMB
            };

            return Ok(checks);
        }

        private string GetDashboardHtml()
        {
            return @"
<!DOCTYPE html>
<html lang='en'>
<head>
    <meta charset='UTF-8'>
    <meta name='viewport' content='width=device-width, initial-scale=1.0'>
    <title>MobileGW Data Sync Dashboard</title>
    <link href='https://cdn.jsdelivr.net/npm/bootstrap@5.3.0/dist/css/bootstrap.min.css' rel='stylesheet'>
    <script src='https://cdn.jsdelivr.net/npm/chart.js'></script>
    <style>
        body { background: #f5f7fa; }
        .card { border: none; box-shadow: 0 2px 4px rgba(0,0,0,0.1); margin-bottom: 20px; }
        .status-badge { padding: 4px 12px; border-radius: 20px; font-size: 12px; font-weight: 600; }
        .status-completed { background: #d4edda; color: #155724; }
        .status-failed { background: #f8d7da; color: #721c24; }
        .status-inprogress { background: #fff3cd; color: #856404; }
        .metric-card { text-align: center; padding: 20px; }
        .metric-value { font-size: 36px; font-weight: bold; }
        .metric-label { color: #6c757d; font-size: 14px; }
        #liveTime { font-family: monospace; }
        
        .login-container {
            position: fixed;
            top: 0;
            left: 0;
            right: 0;
            bottom: 0;
            background: linear-gradient(135deg, #667eea 0%, #764ba2 100%);
            display: flex;
            align-items: center;
            justify-content: center;
        }
        
        .login-card {
            background: white;
            padding: 2rem;
            border-radius: 10px;
            box-shadow: 0 10px 40px rgba(0,0,0,0.2);
            width: 100%;
            max-width: 400px;
        }
    </style>
</head>
<body>
    <!-- Login Screen -->
    <div id='loginScreen' class='login-container'>
        <div class='login-card'>
            <h3 class='text-center mb-4'>MobileGW Sync Dashboard</h3>
            <form id='loginForm'>
                <div class='mb-3'>
                    <label class='form-label'>Username</label>
                    <input type='text' class='form-control' id='loginUsername' required>
                </div>
                <div class='mb-3'>
                    <label class='form-label'>Password</label>
                    <input type='password' class='form-control' id='loginPassword' required>
                </div>
                <button type='submit' class='btn btn-primary w-100'>Login</button>
            </form>
            <div id='loginError' class='alert alert-danger mt-3' style='display:none'></div>
        </div>
    </div>

    <!-- Dashboard -->
    <div id='dashboardScreen' style='display:none'>
        <nav class='navbar navbar-dark bg-primary'>
            <div class='container-fluid'>
                <span class='navbar-brand mb-0 h1'>📊 MobileGW Data Sync Dashboard</span>
                <span class='navbar-text text-white'>
                    <span id='username'></span> | 
                    <span id='liveTime'></span> | 
                    <a href='#' onclick='logout()' class='text-white text-decoration-none'>Logout</a>
                </span>
            </div>
        </nav>

        <div class='container-fluid mt-4'>
            <!-- Metrics Row -->
            <div class='row' id='metricsRow'>
                <div class='col-md-3'>
                    <div class='card metric-card'>
                        <div class='metric-value text-primary' id='totalRuns'>0</div>
                        <div class='metric-label'>Total Runs (24h)</div>
                    </div>
                </div>
                <div class='col-md-3'>
                    <div class='card metric-card'>
                        <div class='metric-value text-success' id='successRate'>0%</div>
                        <div class='metric-label'>Success Rate</div>
                    </div>
                </div>
                <div class='col-md-3'>
                    <div class='card metric-card'>
                        <div class='metric-value text-info' id='recordsProcessed'>0</div>
                        <div class='metric-label'>Records Processed</div>
                    </div>
                </div>
                <div class='col-md-3'>
                    <div class='card metric-card'>
                        <div class='metric-value text-warning' id='activeJobs'>0</div>
                        <div class='metric-label'>Active Jobs</div>
                    </div>
                </div>
            </div>

            <!-- Charts Row -->
            <div class='row mt-4'>
                <div class='col-md-8'>
                    <div class='card'>
                        <div class='card-header'>
                            <h5 class='mb-0'>Sync Activity (Last 7 Days)</h5>
                        </div>
                        <div class='card-body'>
                            <canvas id='activityChart'></canvas>
                        </div>
                    </div>
                </div>
                <div class='col-md-4'>
                    <div class='card'>
                        <div class='card-header'>
                            <h5 class='mb-0'>Status Distribution (24h)</h5>
                        </div>
                        <div class='card-body'>
                            <canvas id='statusChart'></canvas>
                        </div>
                    </div>
                </div>
            </div>

            <!-- Tables Row -->
            <div class='row mt-4'>
                <div class='col-md-6'>
                    <div class='card'>
                        <div class='card-header d-flex justify-content-between align-items-center'>
                            <h5 class='mb-0'>Recent Sync Runs</h5>
                            <button class='btn btn-sm btn-primary' onclick='loadDashboardData()'>Refresh</button>
                        </div>
                        <div class='card-body'>
                            <table class='table table-sm'>
                                <thead>
                                    <tr>
                                        <th>Job</th>
                                        <th>Start Time</th>
                                        <th>Duration</th>
                                        <th>Records</th>
                                        <th>Status</th>
                                    </tr>
                                </thead>
                                <tbody id='recentRunsTable'></tbody>
                            </table>
                        </div>
                    </div>
                </div>
                <div class='col-md-6'>
                    <div class='card'>
                        <div class='card-header'>
                            <h5 class='mb-0'>Active Jobs</h5>
                        </div>
                        <div class='card-body'>
                            <table class='table table-sm'>
                                <thead>
                                    <tr>
                                        <th>Job Name</th>
                                        <th>Schedule</th>
                                        <th>Last Run</th>
                                        <th>Action</th>
                                    </tr>
                                </thead>
                                <tbody id='activeJobsTable'></tbody>
                            </table>
                        </div>
                    </div>
                </div>
            </div>

            <!-- Health Status Row -->
            <div class='row mt-4'>
                <div class='col-md-12'>
                    <div class='card'>
                        <div class='card-header'>
                            <h5 class='mb-0'>System Health</h5>
                        </div>
                        <div class='card-body'>
                            <div class='row' id='healthStatus'></div>
                        </div>
                    </div>
                </div>
            </div>
        </div>
    </div>

    <script>
        let token = localStorage.getItem('token');
        let activityChart, statusChart;
        let refreshInterval;

        // Check authentication on load
        if (token) {
            checkAuth();
        }

        // Login form handler
        document.getElementById('loginForm').addEventListener('submit', async (e) => {
            e.preventDefault();
            
            const username = document.getElementById('loginUsername').value;
            const password = document.getElementById('loginPassword').value;
            
            try {
                const response = await fetch('/api/auth/login', {
                    method: 'POST',
                    headers: {'Content-Type': 'application/json'},
                    body: JSON.stringify({ username, password })
                });

                if (response.ok) {
                    const data = await response.json();
                    token = data.token;
                    localStorage.setItem('token', token);
                    localStorage.setItem('user', data.user.username);
                    localStorage.setItem('role', data.user.role);
                    
                    showDashboard(data.user.username);
                } else {
                    showLoginError('Invalid username or password');
                }
            } catch (error) {
                console.error('Login failed:', error);
                showLoginError('Connection error. Please try again.');
            }
        });

        async function checkAuth() {
            try {
                const response = await fetch('/api/auth/verify', {
                    headers: {'Authorization': `Bearer ${token}`}
                });

                if (response.ok) {
                    const data = await response.json();
                    showDashboard(data.user || localStorage.getItem('user'));
                } else {
                    localStorage.clear();
                    showLogin();
                }
            } catch (error) {
                console.error('Auth check failed:', error);
                localStorage.clear();
                showLogin();
            }
        }

        function showLogin() {
            document.getElementById('loginScreen').style.display = 'flex';
            document.getElementById('dashboardScreen').style.display = 'none';
            clearInterval(refreshInterval);
        }

        function showDashboard(username) {
            document.getElementById('loginScreen').style.display = 'none';
            document.getElementById('dashboardScreen').style.display = 'block';
            document.getElementById('username').textContent = username;
            
            updateTime();
            setInterval(updateTime, 1000);
            
            loadDashboardData();
            loadHealthStatus();
            
            // Auto-refresh every 10 seconds
            refreshInterval = setInterval(() => {
                loadDashboardData();
                loadHealthStatus();
            }, 10000);
        }

        function showLoginError(message) {
            const errorDiv = document.getElementById('loginError');
            errorDiv.textContent = message;
            errorDiv.style.display = 'block';
            setTimeout(() => errorDiv.style.display = 'none', 5000);
        }

        function logout() {
            fetch('/api/auth/logout', {
                method: 'POST',
                headers: {'Authorization': `Bearer ${token}`}
            });
            
            localStorage.clear();
            location.reload();
        }

        function updateTime() {
            document.getElementById('liveTime').textContent = new Date().toLocaleString();
        }

        function formatDuration(seconds) {
            if (seconds < 60) return seconds.toFixed(1) + 's';
            if (seconds < 3600) return (seconds / 60).toFixed(1) + 'm';
            return (seconds / 3600).toFixed(1) + 'h';
        }

        function formatNumber(num) {
            return new Intl.NumberFormat().format(num);
        }

        function getStatusBadge(status) {
            const statusClass = status.toLowerCase().replace(' ', '');
            return `<span class='status-badge status-${statusClass}'>${status}</span>`;
        }

        async function loadDashboardData() {
            try {
                const response = await fetch('/dashboard/data', {
                    headers: {'Authorization': `Bearer ${token}`}
                });
                
                if (!response.ok) {
                    if (response.status === 401) {
                        localStorage.clear();
                        location.reload();
                    }
                    return;
                }

                const data = await response.json();

                // Update metrics
                const totalRuns = data.stats24h.reduce((sum, s) => sum + s.count, 0);
                const successCount = data.stats24h.find(s => s.status === 'Completed')?.count || 0;
                const successRate = totalRuns > 0 ? (successCount / totalRuns * 100).toFixed(1) : 0;
                const totalRecords = data.recentRuns.reduce((sum, r) => sum + r.recordsProcessed, 0);

                document.getElementById('totalRuns').textContent = formatNumber(totalRuns);
                document.getElementById('successRate').textContent = successRate + '%';
                document.getElementById('recordsProcessed').textContent = formatNumber(totalRecords);
                document.getElementById('activeJobs').textContent = data.activeJobs.length;

                // Update charts
                updateCharts(data);

                // Update tables
                updateTables(data);

            } catch (error) {
                console.error('Failed to load dashboard data:', error);
            }
        }

        function updateCharts(data) {
            // Activity chart
            if (activityChart) {
                activityChart.data.labels = data.chartData.map(d => 
                    new Date(d.date).toLocaleDateString('en-US', { weekday: 'short', month: 'short', day: 'numeric' })
                );
                activityChart.data.datasets[0].data = data.chartData.map(d => d.success);
                activityChart.data.datasets[1].data = data.chartData.map(d => d.failed);
                activityChart.update();
            } else {
                const ctx = document.getElementById('activityChart').getContext('2d');
                activityChart = new Chart(ctx, {
                    type: 'bar',
                    data: {
                        labels: data.chartData.map(d => 
                            new Date(d.date).toLocaleDateString('en-US', { weekday: 'short', month: 'short', day: 'numeric' })
                        ),
                        datasets: [{
                            label: 'Success',
                            data: data.chartData.map(d => d.success),
                            backgroundColor: 'rgba(40, 167, 69, 0.8)'
                        }, {
                            label: 'Failed',
                            data: data.chartData.map(d => d.failed),
                            backgroundColor: 'rgba(220, 53, 69, 0.8)'
                        }]
                    },
                    options: {
                        responsive: true,
                        scales: {
                            y: {
                                beginAtZero: true,
                                ticks: { stepSize: 1 }
                            }
                        }
                    }
                });
            }

            // Status chart
            const statusData = {
                Completed: data.stats24h.find(s => s.status === 'Completed')?.count || 0,
                Failed: data.stats24h.find(s => s.status === 'Failed')?.count || 0,
                InProgress: data.stats24h.find(s => s.status === 'InProgress')?.count || 0
            };

            if (statusChart) {
                statusChart.data.datasets[0].data = Object.values(statusData);
                statusChart.update();
            } else {
                const ctx = document.getElementById('statusChart').getContext('2d');
                statusChart = new Chart(ctx, {
                    type: 'doughnut',
                    data: {
                        labels: Object.keys(statusData),
                        datasets: [{
                            data: Object.values(statusData),
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

        function updateTables(data) {
            // Recent runs table
            const recentRunsHtml = data.recentRuns.map(run => `
                <tr>
                    <td>${run.jobName}</td>
                    <td>${new Date(run.startTime).toLocaleTimeString()}</td>
                    <td>${formatDuration(run.duration)}</td>
                    <td>${formatNumber(run.recordsProcessed)}</td>
                    <td>${getStatusBadge(run.status)}</td>
                </tr>
            `).join('');
            document.getElementById('recentRunsTable').innerHTML = recentRunsHtml || '<tr><td colspan=""5"" class=""text-center"">No recent runs</td></tr>';

            // Active jobs table
            const userRole = localStorage.getItem('role');
            const activeJobsHtml = data.activeJobs.map(job => `
                <tr>
                    <td>${job.name}</td>
                    <td><code style=""font-size: 11px"">${job.cronExpression}</code></td>
                    <td>${job.lastRunAt ? new Date(job.lastRunAt).toLocaleString() : 'Never'}</td>
                    <td>
                        ${userRole !== 'Viewer' ? 
                            `<button class='btn btn-sm btn-primary' onclick='triggerJob(""${job.id}"")''>Run</button>` : 
                            '<span class=""text-muted"">View only</span>'}
                    </td>
                </tr>
            `).join('');
            document.getElementById('activeJobsTable').innerHTML = activeJobsHtml || '<tr><td colspan=""4"" class=""text-center"">No active jobs</td></tr>';
        }

        async function triggerJob(jobId) {
            if (!confirm('Are you sure you want to trigger this job?')) return;
            
            try {
                const response = await fetch(`/dashboard/trigger/${jobId}`, {
                    method: 'POST',
                    headers: {'Authorization': `Bearer ${token}`}
                });

                if (response.ok) {
                    const result = await response.json();
                    alert(`Job triggered successfully! Processed ${result.recordsProcessed} records.`);
                    loadDashboardData();
                } else {
                    const error = await response.json();
                    alert(`Failed to trigger job: ${error.message}`);
                }
            } catch (error) {
                console.error('Failed to trigger job:', error);
                alert('Failed to trigger job');
            }
        }

        async function loadHealthStatus() {
            try {
                const response = await fetch('/dashboard/health-status', {
                    headers: {'Authorization': `Bearer ${token}`}
                });

                if (response.ok) {
                    const health = await response.json();
                    const healthHtml = Object.entries(health).map(([key, value]) => `
                        <div class='col-md-3'>
                            <div class='alert alert-${value.status === 'healthy' ? 'success' : value.status === 'degraded' ? 'warning' : 'danger'} mb-0'>
                                <strong>${key.toUpperCase()}</strong><br>
                                <small>${value.message}</small>
                            </div>
                        </div>
                    `).join('');
                    document.getElementById('healthStatus').innerHTML = healthHtml;
                }
            } catch (error) {
                console.error('Failed to load health status:', error);
            }
        }
    </script>
</body>
</html>";
        }
    }
}