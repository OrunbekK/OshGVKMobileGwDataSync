using Microsoft.Extensions.Logging;
using MobileGwDataSync.Core.Constants;
using MobileGwDataSync.Core.Exceptions;
using MobileGwDataSync.Core.Interfaces;
using MobileGwDataSync.Core.Models.Domain;
using MobileGwDataSync.Core.Models.DTO;
using MobileGwDataSync.Data.Context;
using MobileGwDataSync.Data.Entities;
using System.Diagnostics;
using Newtonsoft.Json;

namespace MobileGwDataSync.Core.Services
{
    public class SyncOrchestrator : ISyncService
    {
        private readonly IDataSource _dataSource;
        private readonly IDataTarget _dataTarget;
        private readonly ServiceDbContext _dbContext;
        private readonly ILogger<SyncOrchestrator> _logger;
        private readonly IMetricsService? _metricsService;

        public SyncOrchestrator(
            IDataSource dataSource,
            IDataTarget dataTarget,
            ServiceDbContext dbContext,
            ILogger<SyncOrchestrator> logger,
            IMetricsService? metricsService = null)
        {
            _dataSource = dataSource;
            _dataTarget = dataTarget;
            _dbContext = dbContext;
            _logger = logger;
            _metricsService = metricsService;
        }

        public async Task<SyncResultDTO> ExecuteSyncAsync(string jobId, CancellationToken cancellationToken = default)
        {
            var stopwatch = Stopwatch.StartNew();
            var runId = Guid.NewGuid();
            SyncRunEntity? syncRun = null;

            _logger.LogInformation("Starting sync for job {JobId}, RunId: {RunId}", jobId, runId);
            _metricsService?.RecordSyncStart(jobId);

            try
            {
                // Создаем запись о запуске
                syncRun = await CreateSyncRunAsync(jobId, runId, cancellationToken);

                // Шаг 1: Инициализация
                await LogStepAsync(runId, StepNames.Initialize, async () =>
                {
                    _logger.LogInformation("Initializing sync process...");

                    // Тестируем соединения
                    var sourceTest = await _dataSource.TestConnectionAsync(cancellationToken);
                    if (!sourceTest)
                        throw new DataSourceException("Failed to connect to data source");

                    var targetPrepared = await _dataTarget.PrepareTargetAsync(cancellationToken);
                    if (!targetPrepared)
                        throw new DataTargetException("Failed to prepare target");

                    return "Connections established";
                }, cancellationToken);

                // Шаг 2: Получение данных из 1С
                DataTableDTO? fetchedData = null;
                await LogStepAsync(runId, StepNames.FetchData, async () =>
                {
                    _logger.LogInformation("Fetching data from {Source}...", _dataSource.SourceName);

                    var parameters = new Dictionary<string, string>
                    {
                        ["endpoint"] = "/gbill/hs/api/subscribers"
                    };

                    fetchedData = await _dataSource.FetchDataAsync(parameters, cancellationToken);

                    if (fetchedData == null || fetchedData.TotalRows == 0)
                    {
                        _logger.LogWarning("No data received from source");
                    }

                    return $"Fetched {fetchedData?.TotalRows ?? 0} records";
                }, cancellationToken);

                if (fetchedData == null)
                {
                    throw new SyncException("No data received from source");
                }

                // Обновляем количество полученных записей
                syncRun.RecordsFetched = fetchedData.TotalRows;

                // Шаг 3: Валидация данных
                await LogStepAsync(runId, StepNames.ValidateData, async () =>
                {
                    _logger.LogInformation("Validating {Count} records...", fetchedData.TotalRows);

                    // Проверяем обязательные поля
                    var invalidRows = fetchedData.Rows
                        .Where(r => string.IsNullOrEmpty(r.GetValueOrDefault("Account", string.Empty)?.ToString()))
                        .ToList();

                    if (invalidRows.Any())
                    {
                        _logger.LogWarning("Found {Count} invalid rows (missing Account)", invalidRows.Count);

                        // Удаляем невалидные строки
                        foreach (var row in invalidRows)
                        {
                            fetchedData.Rows.Remove(row);
                        }
                    }

                    await Task.CompletedTask;
                    return $"Validated {fetchedData.TotalRows} records";
                }, cancellationToken);

                // Шаг 4: Сохранение данных
                await LogStepAsync(runId, StepNames.TransferData, async () =>
                {
                    _logger.LogInformation("Transferring data to {Target}...", _dataTarget.TargetName);

                    var saveResult = await _dataTarget.SaveDataAsync(fetchedData, cancellationToken);

                    if (!saveResult)
                        throw new DataTargetException("Failed to save data to target");

                    return $"Transferred {fetchedData.TotalRows} records";
                }, cancellationToken);

                // Шаг 5: Финализация
                await LogStepAsync(runId, StepNames.FinalizeTarget, async () =>
                {
                    _logger.LogInformation("Finalizing target...");

                    await _dataTarget.FinalizeTargetAsync(true, cancellationToken);

                    return "Target finalized successfully";
                }, cancellationToken);

                // Обновляем статус запуска
                syncRun.Status = SyncStatus.Completed.ToString();
                syncRun.RecordsProcessed = fetchedData.TotalRows;
                syncRun.EndTime = DateTime.UtcNow;

                await _dbContext.SaveChangesAsync(cancellationToken);

                stopwatch.Stop();

                // Записываем метрики
                _metricsService?.RecordSyncComplete(jobId, true, fetchedData.TotalRows, stopwatch.Elapsed);

                _logger.LogInformation(
                    "Sync completed successfully. RunId: {RunId}, Duration: {Duration}s, Records: {Records}",
                    runId, stopwatch.Elapsed.TotalSeconds, fetchedData.TotalRows);

                return new SyncResultDTO
                {
                    Success = true,
                    RecordsProcessed = fetchedData.TotalRows,
                    RecordsFailed = 0,
                    Duration = stopwatch.Elapsed,
                    Metrics = new Dictionary<string, object>
                    {
                        ["RunId"] = runId,
                        ["RecordsFetched"] = fetchedData.TotalRows,
                        ["Source"] = _dataSource.SourceName,
                        ["Target"] = _dataTarget.TargetName
                    }
                };
            }
            catch (OperationCanceledException)
            {
                _logger.LogWarning("Sync cancelled for job {JobId}", jobId);

                if (syncRun != null)
                {
                    syncRun.Status = SyncStatus.Cancelled.ToString();
                    syncRun.EndTime = DateTime.UtcNow;
                    await _dbContext.SaveChangesAsync(CancellationToken.None);
                }

                // Финализируем target при отмене
                await _dataTarget.FinalizeTargetAsync(false, CancellationToken.None);

                throw;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Sync failed for job {JobId}", jobId);

                if (syncRun != null)
                {
                    syncRun.Status = SyncStatus.Failed.ToString();
                    syncRun.EndTime = DateTime.UtcNow;
                    syncRun.ErrorMessage = ex.Message;
                    await _dbContext.SaveChangesAsync(CancellationToken.None);
                }

                // Финализируем target при ошибке
                await _dataTarget.FinalizeTargetAsync(false, CancellationToken.None);

                _metricsService?.RecordSyncError(jobId, ex.GetType().Name);
                _metricsService?.RecordSyncComplete(jobId, false, 0, stopwatch.Elapsed);

                return new SyncResultDTO
                {
                    Success = false,
                    RecordsProcessed = syncRun?.RecordsProcessed ?? 0,
                    Duration = stopwatch.Elapsed,
                    Errors = new List<string> { ex.Message }
                };
            }
        }

        public async Task<SyncRun> GetSyncRunAsync(Guid runId, CancellationToken cancellationToken = default)
        {
            var entity = await _dbContext.SyncRuns.FindAsync(new object[] { runId }, cancellationToken);

            if (entity == null)
                throw new SyncException($"Sync run {runId} not found");

            return MapToSyncRun(entity);
        }

        public async Task<IEnumerable<SyncRun>> GetSyncHistoryAsync(
            string jobId,
            int limit = 100,
            CancellationToken cancellationToken = default)
        {
            var runs = _dbContext.SyncRuns
                .Where(r => r.JobId == jobId)
                .OrderByDescending(r => r.StartTime)
                .Take(limit)
                .Select(r => MapToSyncRun(r));

            return await Task.FromResult(runs);
        }

        public async Task<bool> CancelSyncAsync(Guid runId, CancellationToken cancellationToken = default)
        {
            var syncRun = await _dbContext.SyncRuns.FindAsync(new object[] { runId }, cancellationToken);

            if (syncRun == null)
                return false;

            if (syncRun.Status == SyncStatus.InProgress.ToString())
            {
                syncRun.Status = SyncStatus.Cancelled.ToString();
                syncRun.EndTime = DateTime.UtcNow;
                await _dbContext.SaveChangesAsync(cancellationToken);

                _logger.LogInformation("Sync run {RunId} cancelled", runId);
                return true;
            }

            return false;
        }

        private async Task<SyncRunEntity> CreateSyncRunAsync(
            string jobId,
            Guid runId,
            CancellationToken cancellationToken)
        {
            var syncRun = new SyncRunEntity
            {
                Id = runId,
                JobId = jobId,
                StartTime = DateTime.UtcNow,
                Status = SyncStatus.InProgress.ToString(),
                RecordsProcessed = 0,
                RecordsFetched = 0,
                Metadata = JsonConvert.SerializeObject(new
                {
                    Source = _dataSource.SourceName,
                    Target = _dataTarget.TargetName,
                    Timestamp = DateTime.UtcNow
                })
            };

            _dbContext.SyncRuns.Add(syncRun);
            await _dbContext.SaveChangesAsync(cancellationToken);

            return syncRun;
        }

        private async Task LogStepAsync(
            Guid runId,
            string stepName,
            Func<Task<string>> action,
            CancellationToken cancellationToken)
        {
            var step = new SyncRunStepEntity
            {
                Id = Guid.NewGuid(),
                RunId = runId,
                StepName = stepName,
                StartTime = DateTime.UtcNow,
                Status = SyncStatus.InProgress.ToString()
            };

            _dbContext.SyncRunSteps.Add(step);
            await _dbContext.SaveChangesAsync(cancellationToken);

            var stopwatch = Stopwatch.StartNew();

            try
            {
                var result = await action();

                step.Status = SyncStatus.Completed.ToString();
                step.Details = result;
                step.EndTime = DateTime.UtcNow;
                step.DurationMs = stopwatch.ElapsedMilliseconds;

                _logger.LogInformation("Step {StepName} completed in {Duration}ms: {Result}",
                    stepName, stopwatch.ElapsedMilliseconds, result);
            }
            catch (Exception ex)
            {
                step.Status = SyncStatus.Failed.ToString();
                step.Details = ex.Message;
                step.EndTime = DateTime.UtcNow;
                step.DurationMs = stopwatch.ElapsedMilliseconds;

                _logger.LogError(ex, "Step {StepName} failed after {Duration}ms",
                    stepName, stopwatch.ElapsedMilliseconds);

                throw;
            }
            finally
            {
                await _dbContext.SaveChangesAsync(CancellationToken.None);
            }
        }

        private SyncRun MapToSyncRun(SyncRunEntity entity)
        {
            return new SyncRun
            {
                Id = entity.Id,
                JobId = entity.JobId,
                StartTime = entity.StartTime,
                EndTime = entity.EndTime,
                Status = Enum.Parse<SyncStatus>(entity.Status),
                RecordsProcessed = entity.RecordsProcessed,
                RecordsFetched = entity.RecordsFetched,
                ErrorMessage = entity.ErrorMessage,
                Metadata = string.IsNullOrEmpty(entity.Metadata)
                    ? new Dictionary<string, object>()
                    : JsonConvert.DeserializeObject<Dictionary<string, object>>(entity.Metadata)
                      ?? new Dictionary<string, object>()
            };
        }
    }
}