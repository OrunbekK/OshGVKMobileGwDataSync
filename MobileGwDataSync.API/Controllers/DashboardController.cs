using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using MobileGwDataSync.Data.Context;

namespace MobileGwDataSync.API.Controllers
{
    [ApiController]
    [Route("[controller]")]
    [AllowAnonymous] // Для простоты, потом добавите авторизацию
    public class DashboardController : ControllerBase
    {
        private readonly ServiceDbContext _context;
        private readonly IConfiguration _configuration;

        public DashboardController(ServiceDbContext context, IConfiguration configuration)
        {
            _context = context;
            _configuration = configuration;
        }

        [HttpGet]
        [Produces("text/html")]
        public IActionResult Index()
        {
            var html = GetDashboardHtml();
            return Content(html, "text/html");
        }

        [HttpGet("data")]
        public async Task<IActionResult> GetDashboardData()
        {
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
                serverTime = now
            });
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
    </style>
</head>
<body>
    <nav class='navbar navbar-dark bg-primary'>
        <div class='container-fluid'>
            <span class='navbar-brand mb-0 h1'>📊 MobileGW Data Sync Dashboard</span>
            <span class='navbar-text text-white' id='liveTime'></span>
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
                    <div class='card-header'>
                        <h5 class='mb-0'>Recent Sync Runs</h5>
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
                                    <th>Status</th>
                                </tr>
                            </thead>
                            <tbody id='activeJobsTable'></tbody>
                        </table>
                    </div>
                </div>
            </div>
        </div>
    </div>

    <script>
        let activityChart, statusChart;

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
                const response = await fetch('/dashboard/data');
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

                // Update activity chart
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

                // Update status chart
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

                // Update recent runs table
                const recentRunsHtml = data.recentRuns.map(run => `
                    <tr>
                        <td>${run.jobName}</td>
                        <td>${new Date(run.startTime).toLocaleTimeString()}</td>
                        <td>${formatDuration(run.duration)}</td>
                        <td>${formatNumber(run.recordsProcessed)}</td>
                        <td>${getStatusBadge(run.status)}</td>
                    </tr>
                `).join('');
                document.getElementById('recentRunsTable').innerHTML = recentRunsHtml || '<tr><td colspan=""5"">No recent runs</td></tr>';

                // Update active jobs table
                const activeJobsHtml = data.activeJobs.map(job => `
                    <tr>
                        <td>${job.name}</td>
                        <td><code>${job.cronExpression}</code></td>
                        <td>${job.lastRunAt ? new Date(job.lastRunAt).toLocaleString() : 'Never'}</td>
                        <td>${job.lastRunStatus ? getStatusBadge(job.lastRunStatus) : '-'}</td>
                    </tr>
                `).join('');
                document.getElementById('activeJobsTable').innerHTML = activeJobsHtml || '<tr><td colspan=""4"">No active jobs</td></tr>';

            } catch (error) {
                console.error('Failed to load dashboard data:', error);
            }
        }

        // Initialize
        updateTime();
        setInterval(updateTime, 1000);
        loadDashboardData();
        setInterval(loadDashboardData, 10000); // Refresh every 10 seconds
    </script>
</body>
</html>";
        }
    }
}