using Microsoft.Extensions.Logging;
using MobileGwDataSync.Core.Constants;
using MobileGwDataSync.Core.Exceptions;
using MobileGwDataSync.Core.Interfaces;
using MobileGwDataSync.Core.Models.Domain;
using MobileGwDataSync.Core.Models.DTO;
using System.Diagnostics;

namespace MobileGwDataSync.Core.Services
{
    public class SyncOrchestrator : ISyncService
    {
        private readonly IDataSource _dataSource;
        private readonly IDataTarget _dataTarget;
        private readonly ISyncRunRepository _repository;
        private readonly ISyncJobRepository _jobRepository;
        private readonly ILogger<SyncOrchestrator> _logger;
        private readonly IMetricsService? _metricsService;

        public SyncOrchestrator(
            IDataSource dataSource,
            IDataTarget dataTarget,
            ISyncRunRepository repository,
            ISyncJobRepository jobRepository,
            ILogger<SyncOrchestrator> logger,
            IMetricsService? metricsService = null)
        {
            _dataSource = dataSource;
            _dataTarget = dataTarget;
            _repository = repository;
            _jobRepository = jobRepository;
            _logger = logger;
            _metricsService = metricsService;
        }

        public async Task<SyncResultDTO> ExecuteSyncAsync(string jobId, CancellationToken cancellationToken = default)
        {
            var stopwatch = Stopwatch.StartNew();
            SyncRun? syncRun = null;

            _logger.LogInformation("Starting sync for job {JobId}", jobId);
            _metricsService?.RecordSyncStart(jobId);

            try
            {
                // Получаем настройки задачи через интерфейс
                var job = await _jobRepository.GetJobAsync(jobId, cancellationToken);

                if (job == null)
                {
                    throw new SyncException($"Job {jobId} not found in database");
                }

                var oneCEndpoint = job.OneCEndpoint;

                if (string.IsNullOrEmpty(oneCEndpoint))
                {
                    throw new SyncException($"OneCEndpoint not configured for job {jobId}");
                }

                _logger.LogInformation("Using endpoint from job configuration: {Endpoint}", oneCEndpoint);

                // Создаем запись о запуске
                syncRun = await _repository.CreateRunAsync(jobId, cancellationToken);
                var runId = syncRun.Id;

                _logger.LogInformation("Created sync run with ID: {RunId}", runId);

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
                    _logger.LogInformation("Fetching data from {Source} using endpoint: {Endpoint}",
                       _dataSource.SourceName, oneCEndpoint);

                    // Используем endpoint из настроек задачи
                    var parameters = new Dictionary<string, string>
                    {
                        ["endpoint"] = oneCEndpoint
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
                await _repository.UpdateRunAsync(syncRun, cancellationToken);

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
                syncRun.Status = SyncStatus.Completed;
                syncRun.RecordsProcessed = fetchedData.TotalRows;
                syncRun.EndTime = DateTime.UtcNow;
                await _repository.UpdateRunAsync(syncRun, cancellationToken);

                stopwatch.Stop();

                // Записываем метрики
                _metricsService?.RecordSyncComplete(
                    jobId,                      // jobName
                    true,                       // success
                    fetchedData.TotalRows,      // recordsProcessed
                    stopwatch.Elapsed           // duration
                );

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
                    syncRun.Status = SyncStatus.Cancelled;
                    syncRun.EndTime = DateTime.UtcNow;
                    await _repository.UpdateRunAsync(syncRun, CancellationToken.None);
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
                    syncRun.Status = SyncStatus.Failed;
                    syncRun.EndTime = DateTime.UtcNow;
                    syncRun.ErrorMessage = ex.Message;
                    await _repository.UpdateRunAsync(syncRun, CancellationToken.None);
                }

                // Финализируем target при ошибке
                await _dataTarget.FinalizeTargetAsync(false, CancellationToken.None);

                _metricsService?.RecordSyncError(jobId, ex.GetType().Name);

                return new SyncResultDTO
                {
                    Success = false,
                    RecordsProcessed = syncRun?.RecordsProcessed ?? 0,
                    Duration = stopwatch.Elapsed,
                    Errors = new List<string> { ex.Message }
                };
            }
        }

        // ... остальные методы без изменений ...

        private async Task LogStepAsync(
            Guid runId,
            string stepName,
            Func<Task<string>> action,
            CancellationToken cancellationToken)
        {
            var step = await _repository.CreateStepAsync(runId, stepName, cancellationToken);
            var stopwatch = Stopwatch.StartNew();

            try
            {
                var result = await action();

                step.Status = SyncStatus.Completed;
                step.Details = result;
                step.EndTime = DateTime.UtcNow;
                step.DurationMs = stopwatch.ElapsedMilliseconds;

                await _repository.UpdateStepAsync(step, cancellationToken);

                // Записываем метрику шага
                _metricsService?.RecordStepDuration(runId.ToString(), stepName, stopwatch.Elapsed);

                _logger.LogInformation("Step {StepName} completed in {Duration}ms: {Result}",
                    stepName, stopwatch.ElapsedMilliseconds, result);
            }
            catch (Exception ex)
            {
                step.Status = SyncStatus.Failed;
                step.Details = ex.Message;
                step.EndTime = DateTime.UtcNow;
                step.DurationMs = stopwatch.ElapsedMilliseconds;

                await _repository.UpdateStepAsync(step, CancellationToken.None);

                _logger.LogError(ex, "Step {StepName} failed after {Duration}ms",
                    stepName, stopwatch.ElapsedMilliseconds);

                throw;
            }
        }

        public async Task<SyncRun> GetSyncRunAsync(Guid runId, CancellationToken cancellationToken = default)
        {
            var run = await _repository.GetRunAsync(runId, cancellationToken);

            if (run == null)
                throw new SyncException($"Sync run {runId} not found");

            return run;
        }

        public async Task<IEnumerable<SyncRun>> GetSyncHistoryAsync(
            string jobId,
            int limit = 100,
            CancellationToken cancellationToken = default)
        {
            return await _repository.GetRunHistoryAsync(jobId, limit, cancellationToken);
        }

        public async Task<bool> CancelSyncAsync(Guid runId, CancellationToken cancellationToken = default)
        {
            var syncRun = await _repository.GetRunAsync(runId, cancellationToken);

            if (syncRun == null)
                return false;

            if (syncRun.Status == SyncStatus.InProgress)
            {
                syncRun.Status = SyncStatus.Cancelled;
                syncRun.EndTime = DateTime.UtcNow;
                await _repository.UpdateRunAsync(syncRun, cancellationToken);

                _logger.LogInformation("Sync run {RunId} cancelled", runId);
                return true;
            }

            return false;
        }
    }
}