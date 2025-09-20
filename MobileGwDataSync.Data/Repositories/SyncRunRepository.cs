using Microsoft.EntityFrameworkCore;
using MobileGwDataSync.Core.Interfaces;
using MobileGwDataSync.Core.Models.Domain;
using MobileGwDataSync.Data.Context;
using MobileGwDataSync.Data.Entities;
using Newtonsoft.Json;

namespace MobileGwDataSync.Data.Repositories
{
    public class SyncRunRepository : ISyncRunRepository
    {
        private readonly ServiceDbContext _context;

        public SyncRunRepository(ServiceDbContext context)
        {
            _context = context;
        }

        public async Task<SyncRun> CreateRunAsync(string jobId, CancellationToken cancellationToken = default)
        {
            var entity = new SyncRunEntity
            {
                Id = Guid.NewGuid(),
                JobId = jobId,
                StartTime = DateTime.UtcNow,
                Status = SyncStatus.InProgress.ToString(),
                RecordsProcessed = 0,
                RecordsFetched = 0
            };

            _context.SyncRuns.Add(entity);
            await _context.SaveChangesAsync(cancellationToken);

            return MapToSyncRun(entity);
        }

        public async Task<SyncRun?> GetRunAsync(Guid runId, CancellationToken cancellationToken = default)
        {
            var entity = await _context.SyncRuns
                .Include(r => r.Steps)
                .FirstOrDefaultAsync(r => r.Id == runId, cancellationToken);

            return entity != null ? MapToSyncRun(entity) : null;
        }

        public async Task UpdateRunAsync(SyncRun run, CancellationToken cancellationToken = default)
        {
            var entity = await _context.SyncRuns.FindAsync(new object[] { run.Id }, cancellationToken);

            if (entity == null)
                throw new InvalidOperationException($"SyncRun with ID {run.Id} not found");

            entity.Status = run.Status.ToString();
            entity.RecordsProcessed = run.RecordsProcessed;
            entity.RecordsFetched = run.RecordsFetched;
            entity.EndTime = run.EndTime;
            entity.ErrorMessage = run.ErrorMessage;

            if (run.Metadata != null && run.Metadata.Any())
            {
                entity.Metadata = JsonConvert.SerializeObject(run.Metadata);
            }

            await _context.SaveChangesAsync(cancellationToken);
        }

        public async Task<IEnumerable<SyncRun>> GetRunHistoryAsync(string jobId, int limit = 100, CancellationToken cancellationToken = default)
        {
            var entities = await _context.SyncRuns
                .Where(r => r.JobId == jobId)
                .OrderByDescending(r => r.StartTime)
                .Take(limit)
                .Include(r => r.Steps)
                .ToListAsync(cancellationToken);

            return entities.Select(MapToSyncRun);
        }

        public async Task<SyncRunStep> CreateStepAsync(Guid runId, string stepName, CancellationToken cancellationToken = default)
        {
            var entity = new SyncRunStepEntity
            {
                Id = Guid.NewGuid(),
                RunId = runId,
                StepName = stepName,
                StartTime = DateTime.UtcNow,
                Status = SyncStatus.InProgress.ToString()
            };

            _context.SyncRunSteps.Add(entity);
            await _context.SaveChangesAsync(cancellationToken);

            return MapToSyncRunStep(entity);
        }

        public async Task UpdateStepAsync(SyncRunStep step, CancellationToken cancellationToken = default)
        {
            var entity = await _context.SyncRunSteps.FindAsync(new object[] { step.Id }, cancellationToken);

            if (entity == null)
                throw new InvalidOperationException($"SyncRunStep with ID {step.Id} not found");

            entity.Status = step.Status.ToString();
            entity.Details = step.Details;
            entity.EndTime = step.EndTime;
            entity.DurationMs = step.DurationMs;

            if (step.Metrics != null && step.Metrics.Any())
            {
                entity.Metrics = JsonConvert.SerializeObject(step.Metrics);
            }

            await _context.SaveChangesAsync(cancellationToken);
        }

        private SyncRun MapToSyncRun(SyncRunEntity entity)
        {
            var run = new SyncRun
            {
                Id = entity.Id,
                JobId = entity.JobId,
                StartTime = entity.StartTime,
                EndTime = entity.EndTime,
                Status = Enum.Parse<SyncStatus>(entity.Status),
                RecordsProcessed = entity.RecordsProcessed,
                RecordsFetched = entity.RecordsFetched,
                ErrorMessage = entity.ErrorMessage,
                Steps = entity.Steps?.Select(MapToSyncRunStep).ToList() ?? new List<SyncRunStep>()
            };

            if (!string.IsNullOrEmpty(entity.Metadata))
            {
                run.Metadata = JsonConvert.DeserializeObject<Dictionary<string, object>>(entity.Metadata)
                    ?? new Dictionary<string, object>();
            }

            return run;
        }

        private SyncRunStep MapToSyncRunStep(SyncRunStepEntity entity)
        {
            var step = new SyncRunStep
            {
                Id = entity.Id,
                RunId = entity.RunId,
                StepName = entity.StepName,
                StartTime = entity.StartTime,
                EndTime = entity.EndTime,
                Status = Enum.Parse<SyncStatus>(entity.Status),
                Details = entity.Details,
                DurationMs = entity.DurationMs
            };

            if (!string.IsNullOrEmpty(entity.Metrics))
            {
                step.Metrics = JsonConvert.DeserializeObject<Dictionary<string, object>>(entity.Metrics)
                    ?? new Dictionary<string, object>();
            }

            return step;
        }
    }
}