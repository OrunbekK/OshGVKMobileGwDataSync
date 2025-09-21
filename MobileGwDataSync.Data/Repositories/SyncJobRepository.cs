using Microsoft.EntityFrameworkCore;
using MobileGwDataSync.Core.Interfaces;
using MobileGwDataSync.Core.Models.Domain;
using MobileGwDataSync.Data.Context;
using Newtonsoft.Json;

namespace MobileGwDataSync.Data.Services
{
    public class SyncJobRepository : ISyncJobRepository
    {
        private readonly ServiceDbContext _context;

        public SyncJobRepository(ServiceDbContext context)
        {
            _context = context;
        }

        public async Task<SyncJob?> GetJobAsync(string jobId, CancellationToken cancellationToken = default)
        {
            var entity = await _context.SyncJobs
                .FirstOrDefaultAsync(j => j.Id == jobId, cancellationToken);

            if (entity == null)
                return null;

            return MapToSyncJob(entity);
        }

        public async Task<IEnumerable<SyncJob>> GetActiveJobsAsync(CancellationToken cancellationToken = default)
        {
            var entities = await _context.SyncJobs
                .Where(j => j.IsEnabled)
                .ToListAsync(cancellationToken);

            return entities.Select(MapToSyncJob);
        }

        public async Task UpdateJobLastRunAsync(string jobId, DateTime lastRunTime, CancellationToken cancellationToken = default)
        {
            var entity = await _context.SyncJobs
                .FirstOrDefaultAsync(j => j.Id == jobId, cancellationToken);

            if (entity != null)
            {
                entity.LastRunAt = lastRunTime;
                await _context.SaveChangesAsync(cancellationToken);
            }
        }

        private SyncJob MapToSyncJob(Data.Entities.SyncJobEntity entity)
        {
            var job = new SyncJob
            {
                Id = entity.Id,
                Name = entity.Name,
                JobType = Enum.TryParse<SyncJobType>(entity.JobType, out var jobType)
                    ? jobType
                    : SyncJobType.Subscribers,
                CronExpression = entity.CronExpression,
                IsEnabled = entity.IsEnabled,
                DependsOnJobId = entity.DependsOnJobId,
                IsExclusive = entity.IsExclusive,
                Priority = entity.Priority,
                OneCEndpoint = entity.OneCEndpoint,
                TargetProcedure = entity.TargetProcedure ?? string.Empty,
                TargetTable = entity.TargetTable,
                CreatedAt = entity.CreatedAt,
                UpdatedAt = entity.UpdatedAt,
                LastRunAt = entity.LastRunAt,
                NextRunAt = entity.NextRunAt
            };

            if (!string.IsNullOrEmpty(entity.Configuration))
            {
                job.Configuration = JsonConvert.DeserializeObject<Dictionary<string, string>>(entity.Configuration)
                    ?? new Dictionary<string, string>();
            }

            return job;
        }
    }
}
