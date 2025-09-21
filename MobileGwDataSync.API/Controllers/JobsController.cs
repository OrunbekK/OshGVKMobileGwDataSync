using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using MobileGwDataSync.API.Models.Requests;
using MobileGwDataSync.API.Models.Responses.Jobs;
using MobileGwDataSync.Data.Context;
using MobileGwDataSync.Data.Entities;
using Newtonsoft.Json;
using Quartz;

namespace MobileGwDataSync.API.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class JobsController : ControllerBase
    {
        private readonly ServiceDbContext _context;
        private readonly ISchedulerFactory _schedulerFactory;
        private readonly ILogger<JobsController> _logger;

        public JobsController(
            ServiceDbContext context,
            ISchedulerFactory schedulerFactory,
            ILogger<JobsController> logger)
        {
            _context = context;
            _schedulerFactory = schedulerFactory;
            _logger = logger;
        }

        /// <summary>
        /// Получить список всех задач синхронизации
        /// </summary>
        [HttpGet]
        public async Task<ActionResult<IEnumerable<SyncJobDTO>>> GetJobs()
        {
            var jobs = await _context.SyncJobs
                .OrderBy(j => j.Priority)
                .ThenBy(j => j.Name)
                .Select(j => new SyncJobDTO
                {
                    Id = j.Id,
                    Name = j.Name,
                    JobType = j.JobType,
                    CronExpression = j.CronExpression,
                    IsEnabled = j.IsEnabled,
                    DependsOnJobId = j.DependsOnJobId,
                    IsExclusive = j.IsExclusive,
                    Priority = j.Priority,
                    OneCEndpoint = j.OneCEndpoint,
                    TargetTable = j.TargetTable,
                    TargetProcedure = j.TargetProcedure,
                    LastRunAt = j.LastRunAt,
                    NextRunAt = j.NextRunAt,
                    CreatedAt = j.CreatedAt,
                    UpdatedAt = j.UpdatedAt
                })
                .ToListAsync();

            // Обновляем NextRunAt из Quartz
            var scheduler = await _schedulerFactory.GetScheduler();
            foreach (var job in jobs)
            {
                var trigger = await scheduler.GetTrigger(new TriggerKey($"{job.Id}-trigger"));
                if (trigger != null)
                {
                    job.NextRunAt = trigger.GetNextFireTimeUtc()?.LocalDateTime;
                    job.IsRunning = (await scheduler.GetCurrentlyExecutingJobs())
                        .Any(j => j.JobDetail.Key.Name == job.Id);
                }
            }

            return Ok(jobs);
        }

        /// <summary>
        /// Получить задачу по ID
        /// </summary>
        [HttpGet("{id}")]
        public async Task<ActionResult<SyncJobDetailDTO>> GetJob(string id)
        {
            var job = await _context.SyncJobs
                .Include(j => j.Runs.OrderByDescending(r => r.StartTime).Take(10))
                .FirstOrDefaultAsync(j => j.Id == id);

            if (job == null)
            {
                return NotFound(new { message = $"Job with ID '{id}' not found" });
            }

            var dto = new SyncJobDetailDTO
            {
                Id = job.Id,
                Name = job.Name,
                JobType = job.JobType,
                CronExpression = job.CronExpression,
                IsEnabled = job.IsEnabled,
                DependsOnJobId = job.DependsOnJobId,
                IsExclusive = job.IsExclusive,
                Priority = job.Priority,
                OneCEndpoint = job.OneCEndpoint,
                TargetTable = job.TargetTable,
                TargetProcedure = job.TargetProcedure,
                LastRunAt = job.LastRunAt,
                NextRunAt = job.NextRunAt,
                CreatedAt = job.CreatedAt,
                UpdatedAt = job.UpdatedAt,
                Configuration = string.IsNullOrEmpty(job.Configuration)
                    ? new Dictionary<string, string>()
                    : JsonConvert.DeserializeObject<Dictionary<string, string>>(job.Configuration),
                RecentRuns = job.Runs.Select(r => new SyncRunSummaryDTO
                {
                    Id = r.Id,
                    StartTime = r.StartTime,
                    EndTime = r.EndTime,
                    Status = r.Status,
                    RecordsProcessed = r.RecordsProcessed,
                    ErrorMessage = r.ErrorMessage
                }).ToList()
            };

            // Получаем информацию из Quartz
            var scheduler = await _schedulerFactory.GetScheduler();
            var trigger = await scheduler.GetTrigger(new TriggerKey($"{id}-trigger"));
            if (trigger != null)
            {
                dto.NextRunAt = trigger.GetNextFireTimeUtc()?.LocalDateTime;
                dto.PreviousFireTime = trigger.GetPreviousFireTimeUtc()?.LocalDateTime;
            }

            dto.IsRunning = (await scheduler.GetCurrentlyExecutingJobs())
                .Any(j => j.JobDetail.Key.Name == id);

            return Ok(dto);
        }

        /// <summary>
        /// Создать новую задачу синхронизации
        /// </summary>
        [HttpPost]
        public async Task<ActionResult<SyncJobDTO>> CreateJob([FromBody] CreateJobRequest request)
        {
            // Валидация cron expression
            if (!CronExpression.IsValidExpression(request.CronExpression))
            {
                return BadRequest(new { message = "Invalid cron expression" });
            }

            // Проверка уникальности имени
            if (await _context.SyncJobs.AnyAsync(j => j.Name == request.Name))
            {
                return Conflict(new { message = $"Job with name '{request.Name}' already exists" });
            }

            var job = new SyncJobEntity
            {
                Id = Guid.NewGuid().ToString(),
                Name = request.Name,
                JobType = request.JobType,
                CronExpression = request.CronExpression,
                IsEnabled = request.IsEnabled,
                DependsOnJobId = request.DependsOnJobId,
                IsExclusive = request.IsExclusive ?? false,
                Priority = request.Priority ?? 0,
                OneCEndpoint = request.OneCEndpoint,
                TargetTable = request.TargetTable,
                TargetProcedure = request.TargetProcedure,
                CreatedAt = DateTime.UtcNow,
                Configuration = request.Configuration != null
                    ? JsonConvert.SerializeObject(request.Configuration)
                    : null
            };

            _context.SyncJobs.Add(job);
            await _context.SaveChangesAsync();

            // Регистрируем в Quartz если включена
            if (job.IsEnabled)
            {
                await RegisterJobInQuartz(job);
            }

            _logger.LogInformation("Created new sync job: {JobId} - {JobName}", job.Id, job.Name);

            return CreatedAtAction(nameof(GetJob), new { id = job.Id }, MapToDto(job));
        }

        /// <summary>
        /// Обновить задачу синхронизации
        /// </summary>
        [HttpPut("{id}")]
        public async Task<ActionResult<SyncJobDTO>> UpdateJob(string id, [FromBody] UpdateJobRequest request)
        {
            var job = await _context.SyncJobs.FindAsync(id);
            if (job == null)
            {
                return NotFound(new { message = $"Job with ID '{id}' not found" });
            }

            // Валидация cron expression если изменен
            if (!string.IsNullOrEmpty(request.CronExpression) &&
                !CronExpression.IsValidExpression(request.CronExpression))
            {
                return BadRequest(new { message = "Invalid cron expression" });
            }

            // Проверка уникальности имени
            if (!string.IsNullOrEmpty(request.Name) && request.Name != job.Name)
            {
                if (await _context.SyncJobs.AnyAsync(j => j.Name == request.Name))
                {
                    return Conflict(new { message = $"Job with name '{request.Name}' already exists" });
                }
            }

            // Обновляем поля
            if (!string.IsNullOrEmpty(request.Name))
                job.Name = request.Name;
            if (!string.IsNullOrEmpty(request.JobType))
                job.JobType = request.JobType;
            if (!string.IsNullOrEmpty(request.CronExpression))
                job.CronExpression = request.CronExpression;
            if (request.IsEnabled.HasValue)
                job.IsEnabled = request.IsEnabled.Value;
            if (request.DependsOnJobId != null)
                job.DependsOnJobId = string.IsNullOrEmpty(request.DependsOnJobId) ? null : request.DependsOnJobId;
            if (request.IsExclusive.HasValue)
                job.IsExclusive = request.IsExclusive.Value;
            if (request.Priority.HasValue)
                job.Priority = request.Priority.Value;
            if (!string.IsNullOrEmpty(request.OneCEndpoint))
                job.OneCEndpoint = request.OneCEndpoint;
            if (request.TargetTable != null)
                job.TargetTable = request.TargetTable;
            if (request.TargetProcedure != null)
                job.TargetProcedure = request.TargetProcedure;
            if (request.Configuration != null)
                job.Configuration = JsonConvert.SerializeObject(request.Configuration);

            job.UpdatedAt = DateTime.UtcNow;

            await _context.SaveChangesAsync();

            // Обновляем в Quartz
            await UpdateJobInQuartz(job);

            _logger.LogInformation("Updated sync job: {JobId}", job.Id);

            return Ok(MapToDto(job));
        }

        /// <summary>
        /// Удалить задачу синхронизации
        /// </summary>
        [HttpDelete("{id}")]
        public async Task<IActionResult> DeleteJob(string id)
        {
            var job = await _context.SyncJobs.FindAsync(id);
            if (job == null)
            {
                return NotFound(new { message = $"Job with ID '{id}' not found" });
            }

            // Проверяем, нет ли зависимых задач
            var dependentJobs = await _context.SyncJobs
                .Where(j => j.DependsOnJobId == id)
                .Select(j => j.Name)
                .ToListAsync();

            if (dependentJobs.Any())
            {
                return BadRequest(new
                {
                    message = "Cannot delete job with dependent jobs",
                    dependentJobs = dependentJobs
                });
            }

            // Удаляем из Quartz
            await RemoveJobFromQuartz(id);

            // Удаляем из БД (каскадно удалятся runs, steps, metrics)
            _context.SyncJobs.Remove(job);
            await _context.SaveChangesAsync();

            _logger.LogInformation("Deleted sync job: {JobId} - {JobName}", job.Id, job.Name);

            return NoContent();
        }

        /// <summary>
        /// Включить/выключить задачу
        /// </summary>
        [HttpPatch("{id}/toggle")]
        public async Task<ActionResult<SyncJobDTO>> ToggleJob(string id)
        {
            var job = await _context.SyncJobs.FindAsync(id);
            if (job == null)
            {
                return NotFound(new { message = $"Job with ID '{id}' not found" });
            }

            job.IsEnabled = !job.IsEnabled;
            job.UpdatedAt = DateTime.UtcNow;

            await _context.SaveChangesAsync();

            // Обновляем в Quartz
            if (job.IsEnabled)
            {
                await RegisterJobInQuartz(job);
            }
            else
            {
                await PauseJobInQuartz(id);
            }

            _logger.LogInformation("Toggled sync job: {JobId} - Enabled: {IsEnabled}",
                job.Id, job.IsEnabled);

            return Ok(MapToDto(job));
        }

        /// <summary>
        /// Запустить задачу немедленно
        /// </summary>
        [HttpPost("{id}/trigger")]
        public async Task<IActionResult> TriggerJob(string id)
        {
            var job = await _context.SyncJobs.FindAsync(id);
            if (job == null)
            {
                return NotFound(new { message = $"Job with ID '{id}' not found" });
            }

            var scheduler = await _schedulerFactory.GetScheduler();

            // Проверяем, не выполняется ли задача сейчас
            var isRunning = (await scheduler.GetCurrentlyExecutingJobs())
                .Any(j => j.JobDetail.Key.Name == id);

            if (isRunning)
            {
                return Conflict(new { message = "Job is already running" });
            }

            // Запускаем задачу
            await scheduler.TriggerJob(new JobKey(id));

            _logger.LogInformation("Manually triggered sync job: {JobId} - {JobName}",
                job.Id, job.Name);

            return Ok(new { message = "Job triggered successfully", jobId = id });
        }

        // Вспомогательные методы для работы с Quartz

        private async Task RegisterJobInQuartz(SyncJobEntity jobEntity)
        {
            var scheduler = await _schedulerFactory.GetScheduler();

            var job = JobBuilder.Create<Jobs.DataSyncJob>()
                .WithIdentity(jobEntity.Id)
                .UsingJobData("JobId", jobEntity.Id)
                .Build();

            var trigger = TriggerBuilder.Create()
                .WithIdentity($"{jobEntity.Id}-trigger")
                .WithCronSchedule(jobEntity.CronExpression)
                .Build();

            await scheduler.ScheduleJob(job, trigger);
        }

        private async Task UpdateJobInQuartz(SyncJobEntity jobEntity)
        {
            var scheduler = await _schedulerFactory.GetScheduler();

            // Удаляем старую задачу
            await scheduler.DeleteJob(new JobKey(jobEntity.Id));

            // Регистрируем заново если включена
            if (jobEntity.IsEnabled)
            {
                await RegisterJobInQuartz(jobEntity);
            }
        }

        private async Task PauseJobInQuartz(string jobId)
        {
            var scheduler = await _schedulerFactory.GetScheduler();
            await scheduler.PauseJob(new JobKey(jobId));
        }

        private async Task RemoveJobFromQuartz(string jobId)
        {
            var scheduler = await _schedulerFactory.GetScheduler();
            await scheduler.DeleteJob(new JobKey(jobId));
        }

        private SyncJobDTO MapToDto(SyncJobEntity entity)
        {
            return new SyncJobDTO
            {
                Id = entity.Id,
                Name = entity.Name,
                JobType = entity.JobType,
                CronExpression = entity.CronExpression,
                IsEnabled = entity.IsEnabled,
                DependsOnJobId = entity.DependsOnJobId,
                IsExclusive = entity.IsExclusive,
                Priority = entity.Priority,
                OneCEndpoint = entity.OneCEndpoint,
                TargetTable = entity.TargetTable,
                TargetProcedure = entity.TargetProcedure,
                LastRunAt = entity.LastRunAt,
                NextRunAt = entity.NextRunAt,
                CreatedAt = entity.CreatedAt,
                UpdatedAt = entity.UpdatedAt
            };
        }
    }
}