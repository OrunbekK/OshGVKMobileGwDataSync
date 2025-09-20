using Microsoft.Data.SqlClient;
using Microsoft.Extensions.Logging;
using MobileGwDataSync.Core.Exceptions;
using MobileGwDataSync.Core.Interfaces;
using MobileGwDataSync.Core.Models.Configuration;
using MobileGwDataSync.Core.Models.DTO;
using System.Data;
using System.Diagnostics;

namespace MobileGwDataSync.Data.SqlServer
{
    public class SqlServerDataTarget : IDataTarget
    {
        private readonly string _connectionString;
        private readonly ILogger<SqlServerDataTarget> _logger;
        private readonly SyncSettings _syncSettings;
        private SqlConnection? _connection;
        private DataTable? _dataTable;
        private int _recordsToProcess;

        public string TargetName => "SQL Server Business Database";

        public SqlServerDataTarget(
            AppSettings appSettings,
            ILogger<SqlServerDataTarget> logger)
        {
            _connectionString = appSettings.ConnectionStrings.SqlServer;
            _syncSettings = appSettings.SyncSettings;
            _logger = logger;
        }

        public async Task<bool> PrepareTargetAsync(CancellationToken cancellationToken = default)
        {
            try
            {
                _logger.LogInformation("Preparing SQL Server target...");

                // Создаем и открываем соединение
                _connection = new SqlConnection(_connectionString);
                await _connection.OpenAsync(cancellationToken);

                // Тестируем соединение через хранимую процедуру
                using var testCommand = new SqlCommand("USP_MA_TestConnection", _connection)
                {
                    CommandType = CommandType.StoredProcedure
                };
                await testCommand.ExecuteNonQueryAsync(cancellationToken);

                // Создаем DataTable для TVP
                _dataTable = CreateSubscribersTVP();

                _logger.LogInformation("Target prepared successfully. Connection state: {State}",
                    _connection.State);

                return true;
            }
            catch (SqlException ex)
            {
                _logger.LogError(ex, "SQL Server connection failed");
                throw new DataTargetException($"Failed to connect to SQL Server: {ex.Message}", ex);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to prepare target");
                throw new DataTargetException("Failed to prepare SQL Server target", ex);
            }
        }

        public async Task<bool> SaveDataAsync(DataTableDTO data, CancellationToken cancellationToken = default)
        {
            if (_connection == null || _dataTable == null)
            {
                throw new InvalidOperationException("Target not prepared. Call PrepareTargetAsync first.");
            }

            var stopwatch = Stopwatch.StartNew();

            try
            {
                _logger.LogInformation("Processing {Count} records for SQL Server", data.TotalRows);
                _recordsToProcess = data.TotalRows;

                // Заполняем DataTable данными
                PopulateDataTable(data);

                // Выполняем MERGE через хранимую процедуру
                using var command = new SqlCommand("USP_MA_MergeSubscribers", _connection)
                {
                    CommandType = CommandType.StoredProcedure,
                    CommandTimeout = _syncSettings.TimeoutMinutes * 60
                };

                // Добавляем TVP параметр
                var tvpParam = command.Parameters.AddWithValue("@Subscribers", _dataTable);
                tvpParam.SqlDbType = SqlDbType.Structured;
                tvpParam.TypeName = "dbo.SubscriberTVP";

                // Output параметры для статистики
                var processedParam = command.Parameters.Add("@ProcessedCount", SqlDbType.Int);
                processedParam.Direction = ParameterDirection.Output;

                var insertedParam = command.Parameters.Add("@InsertedCount", SqlDbType.Int);
                insertedParam.Direction = ParameterDirection.Output;

                var updatedParam = command.Parameters.Add("@UpdatedCount", SqlDbType.Int);
                updatedParam.Direction = ParameterDirection.Output;

                var deletedParam = command.Parameters.Add("@DeletedCount", SqlDbType.Int);
                deletedParam.Direction = ParameterDirection.Output;

                // Выполняем процедуру
                await command.ExecuteNonQueryAsync(cancellationToken);

                stopwatch.Stop();

                // Получаем результаты
                var processed = (int)(processedParam.Value ?? 0);
                var inserted = (int)(insertedParam.Value ?? 0);
                var updated = (int)(updatedParam.Value ?? 0);
                var deleted = (int)(deletedParam.Value ?? 0);

                _logger.LogInformation(
                    "SQL Server sync completed in {Duration}ms. " +
                    "Processed: {Processed}, Inserted: {Inserted}, Updated: {Updated}, Deleted: {Deleted}",
                    stopwatch.ElapsedMilliseconds,
                    processed, inserted, updated, deleted);

                return true;
            }
            catch (SqlException ex)
            {
                _logger.LogError(ex, "SQL Server operation failed");
                throw new DataTargetException($"Failed to save data to SQL Server: {ex.Message}", ex);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Unexpected error during save operation");
                throw new DataTargetException("Unexpected error saving data", ex);
            }
        }

        public async Task<bool> FinalizeTargetAsync(bool success, CancellationToken cancellationToken = default)
        {
            try
            {
                if (success)
                {
                    _logger.LogInformation("Finalizing target - operation completed successfully");
                }
                else
                {
                    _logger.LogWarning("Finalizing target - operation failed or was cancelled");
                }

                // Очищаем ресурсы
                _dataTable?.Clear();
                _dataTable?.Dispose();
                _dataTable = null;

                if (_connection != null)
                {
                    if (_connection.State == ConnectionState.Open)
                    {
                        await _connection.CloseAsync();
                    }
                    await _connection.DisposeAsync();
                    _connection = null;
                }

                _logger.LogInformation("Target finalized successfully");
                return true;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to finalize target");
                // Не выбрасываем исключение при финализации
                return false;
            }
        }

        /// <summary>
        /// Создает структуру DataTable для TVP
        /// </summary>
        private DataTable CreateSubscribersTVP()
        {
            var table = new DataTable();

            table.Columns.Add("Account", typeof(string));
            table.Columns.Add("Subscriber", typeof(string));
            table.Columns.Add("Address", typeof(string));
            table.Columns.Add("Balance", typeof(decimal));

            // Устанавливаем primary key для оптимизации
            table.PrimaryKey = new[] { table.Columns["Account"]! };

            return table;
        }

        /// <summary>
        /// Заполняет DataTable данными из DTO
        /// </summary>
        private void PopulateDataTable(DataTableDTO data)
        {
            if (_dataTable == null)
                throw new InvalidOperationException("DataTable not initialized");

            _dataTable.Clear();

            foreach (var row in data.Rows)
            {
                var dataRow = _dataTable.NewRow();

                // Мапим поля с проверкой
                dataRow["Account"] = row.GetValueOrDefault("Account", string.Empty);
                dataRow["Subscriber"] = row.GetValueOrDefault("Subscriber", string.Empty);  // FIO уже преобразовано в Subscriber
                dataRow["Address"] = row.GetValueOrDefault("Address", string.Empty);
                dataRow["Balance"] = Convert.ToDecimal(row.GetValueOrDefault("Balance", 0m));

                _dataTable.Rows.Add(dataRow);
            }

            _logger.LogDebug("Populated DataTable with {Count} rows", _dataTable.Rows.Count);
        }

        /// <summary>
        /// Тестирует соединение с SQL Server
        /// </summary>
        public static async Task<bool> TestConnectionAsync(string connectionString, CancellationToken cancellationToken = default)
        {
            try
            {
                using var connection = new SqlConnection(connectionString);
                await connection.OpenAsync(cancellationToken);

                using var command = new SqlCommand("USP_MA_TestConnection", connection)
                {
                    CommandType = CommandType.StoredProcedure
                };

                await command.ExecuteNonQueryAsync(cancellationToken);
                return true;
            }
            catch
            {
                return false;
            }
        }
    }
}