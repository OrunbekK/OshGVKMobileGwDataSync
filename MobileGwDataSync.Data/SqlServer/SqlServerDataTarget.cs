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
        private string? _currentJobType;
        private string? _targetProcedure;
        private string? _targetTable;

        public string TargetName => "SQL Server Business Database";

        public SqlServerDataTarget(
            AppSettings appSettings,
            ILogger<SqlServerDataTarget> logger)
        {
            _connectionString = appSettings.ConnectionStrings.SqlServer;
            _syncSettings = appSettings.SyncSettings;
            _logger = logger;
        }

        /// <summary>
        /// Устанавливает параметры для текущей операции синхронизации
        /// Вызывается из UniversalOneCConnector через параметры
        /// </summary>
        public void SetSyncParameters(Dictionary<string, string> parameters)
        {
            if (parameters.TryGetValue("targetProcedure", out var procedure))
            {
                _targetProcedure = procedure;
            }

            if (parameters.TryGetValue("targetTable", out var table))
            {
                _targetTable = table;
            }

            _logger.LogDebug("Sync parameters set: Procedure={Procedure}, Table={Table}",
                _targetProcedure, _targetTable);
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
            if (_connection == null)
            {
                throw new InvalidOperationException("Target not prepared. Call PrepareTargetAsync first.");
            }

            var stopwatch = Stopwatch.StartNew();

            try
            {
                // Определяем тип данных из Source (устанавливается в SyncOrchestrator)
                _currentJobType = data.Source?.ToLower() ?? "subscribers";

                // Если процедура не установлена явно, определяем по типу
                if (string.IsNullOrEmpty(_targetProcedure))
                {
                    _targetProcedure = GetDefaultProcedureForType(_currentJobType);
                    _logger.LogWarning("Target procedure not set, using default: {Procedure}", _targetProcedure);
                }

                _logger.LogInformation("Processing {Count} records for SQL Server. Type: {Type}, Procedure: {Procedure}",
                    data.TotalRows, _currentJobType, _targetProcedure);

                // Создаем DataTable для TVP на основе типа
                _dataTable = CreateTVPForType(_currentJobType);

                // Заполняем DataTable данными
                PopulateDataTable(data);

                // Выполняем MERGE через хранимую процедуру
                using var command = new SqlCommand(_targetProcedure, _connection)
                {
                    CommandType = CommandType.StoredProcedure,
                    CommandTimeout = _syncSettings.TimeoutMinutes * 60
                };

                // Определяем имя параметра TVP
                var tvpParamName = GetTVPParameterName(_targetProcedure);

                // Добавляем TVP параметр
                var tvpParam = command.Parameters.AddWithValue(tvpParamName, _dataTable);
                tvpParam.SqlDbType = SqlDbType.Structured;
                tvpParam.TypeName = GetTVPTypeName(_targetProcedure);

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
                _logger.LogError(ex, "SQL Server operation failed for type {Type}, procedure {Procedure}",
                    _currentJobType, _targetProcedure);
                throw new DataTargetException($"Failed to save data to SQL Server: {ex.Message}", ex);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Unexpected error during save operation");
                throw new DataTargetException("Unexpected error saving data", ex);
            }
            finally
            {
                // Очищаем DataTable после использования
                _dataTable?.Clear();
                _dataTable?.Dispose();
                _dataTable = null;
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
                _currentJobType = null;
                _targetProcedure = null;
                _targetTable = null;

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
        /// Создает структуру DataTable для TVP на основе типа задачи
        /// </summary>
        private DataTable CreateTVPForType(string jobType)
        {
            var table = new DataTable();

            switch (jobType.ToLower())
            {
                case "subscribers":
                    table.Columns.Add("Account", typeof(string));
                    table.Columns.Add("Subscriber", typeof(string));
                    table.Columns.Add("Address", typeof(string));
                    table.Columns.Add("Balance", typeof(decimal));
                    table.Columns.Add("Type", typeof(byte));
                    table.Columns.Add("State", typeof(string));
                    table.Columns.Add("ControllerId", typeof(string));
                    table.Columns.Add("RouteId", typeof(string));
                    table.PrimaryKey = new[] { table.Columns["Account"]! };
                    break;

                case "controllers":
                    table.Columns.Add("UID", typeof(Guid));
                    table.Columns.Add("Controller", typeof(string));
                    table.Columns.Add("ControllerId", typeof(string));
                    table.PrimaryKey = new[] { table.Columns["UID"]! };
                    break;

                default:
                    throw new NotSupportedException($"TVP structure not defined for type '{jobType}'");
            }

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

            // Определяем ключевое поле на основе типа
            var keyColumn = _currentJobType?.ToLower() switch
            {
                "subscribers" => "Account",
                "controllers" => "UID",
                _ => data.Columns.FirstOrDefault() ?? "Id"
            };

            // Используем HashSet для отслеживания уже добавленных ключей
            var addedKeys = new HashSet<string>();
            var duplicateCount = 0;

            foreach (var row in data.Rows)
            {
                // Получаем значение ключа
                var keyValue = row.GetValueOrDefault(keyColumn, string.Empty)?.ToString() ?? string.Empty;

                // Пропускаем дубликаты и пустые ключи
                if (string.IsNullOrEmpty(keyValue))
                {
                    _logger.LogWarning("Row has empty key value for column {Column}, skipping", keyColumn);
                    continue;
                }

                if (!addedKeys.Add(keyValue))
                {
                    duplicateCount++;
                    _logger.LogWarning("Skipping duplicate {Key}: {Value}", keyColumn, keyValue);
                    continue;
                }

                var dataRow = _dataTable.NewRow();

                // Мапим поля из DTO в DataTable
                foreach (DataColumn column in _dataTable.Columns)
                {
                    if (row.ContainsKey(column.ColumnName))
                    {
                        var value = row[column.ColumnName];

                        // Преобразуем значение к нужному типу
                        if (value != null && value.ToString() != string.Empty)
                        {
                            try
                            {
                                if (column.DataType == typeof(decimal))
                                {
                                    dataRow[column.ColumnName] = Convert.ToDecimal(value);
                                }
                                else if (column.DataType == typeof(byte))
                                {
                                    dataRow[column.ColumnName] = Convert.ToByte(value);
                                }
                                else if (column.DataType == typeof(int))
                                {
                                    dataRow[column.ColumnName] = Convert.ToInt32(value);
                                }
                                else if (column.DataType == typeof(Guid))
                                {
                                    dataRow[column.ColumnName] = value is Guid guid ? guid : Guid.Parse(value.ToString()!);
                                }
                                else
                                {
                                    dataRow[column.ColumnName] = value.ToString() ?? string.Empty;
                                }
                            }
                            catch (Exception ex)
                            {
                                _logger.LogError(ex, "Failed to convert value for column {Column}: {Value}",
                                    column.ColumnName, value);
                                dataRow[column.ColumnName] = GetDefaultValue(column.DataType);
                            }
                        }
                        else
                        {
                            dataRow[column.ColumnName] = GetDefaultValue(column.DataType);
                        }
                    }
                    else
                    {
                        // Если поле отсутствует в данных, ставим значение по умолчанию
                        dataRow[column.ColumnName] = GetDefaultValue(column.DataType);
                    }
                }

                _dataTable.Rows.Add(dataRow);
            }

            if (duplicateCount > 0)
            {
                _logger.LogWarning("Found and skipped {Count} duplicate entries", duplicateCount);
            }

            _logger.LogDebug("Populated DataTable with {Count} unique rows", _dataTable.Rows.Count);
        }

        /// <summary>
        /// Получает значение по умолчанию для типа данных
        /// </summary>
        private object GetDefaultValue(Type dataType)
        {
            if (dataType == typeof(string))
                return string.Empty;
            if (dataType == typeof(Guid))
                return Guid.Empty;
            if (dataType.IsValueType)
                return Activator.CreateInstance(dataType)!;
            return DBNull.Value;
        }

        /// <summary>
        /// Определяет процедуру по умолчанию для типа
        /// </summary>
        private string GetDefaultProcedureForType(string jobType)
        {
            return jobType.ToLower() switch
            {
                "subscribers" => "USP_MA_MergeSubscribers",
                "controllers" => "USP_MA_MergeControllers",
                _ => throw new NotSupportedException($"Default procedure not defined for type '{jobType}'")
            };
        }

        /// <summary>
        /// Определяет имя параметра TVP для процедуры
        /// </summary>
        private string GetTVPParameterName(string procedureName)
        {
            // Извлекаем тип сущности из имени процедуры
            // USP_MA_MergeSubscribers -> @Subscribers
            // USP_MA_MergeControllers -> @Controllers

            if (procedureName.StartsWith("USP_MA_Merge"))
            {
                var entityName = procedureName.Replace("USP_MA_Merge", "");
                return $"@{entityName}";
            }

            // Fallback
            return "@Data";
        }

        /// <summary>
        /// Определяет имя TVP типа на основе процедуры
        /// </summary>
        private string GetTVPTypeName(string procedureName)
        {
            return procedureName switch
            {
                "USP_MA_MergeSubscribers" => "dbo.SubscriberTVP",
                "USP_MA_MergeControllers" => "dbo.ControllerTVP",
                _ => throw new NotSupportedException($"TVP type not defined for procedure {procedureName}")
            };
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