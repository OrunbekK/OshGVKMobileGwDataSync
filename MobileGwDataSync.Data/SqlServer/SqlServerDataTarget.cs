using Microsoft.Data.SqlClient;
using Microsoft.Extensions.Logging;
using MobileGwDataSync.Core.Interfaces;
using MobileGwDataSync.Core.Models.Configuration;
using MobileGwDataSync.Core.Models.DTO;

namespace MobileGwDataSync.Data.SqlServer
{
    public class SqlServerDataTarget : IDataTarget
    {
        private readonly string _connectionString;
        private readonly ILogger<SqlServerDataTarget> _logger;
        private readonly SyncSettings _syncSettings;
        private SqlTransaction? _transaction;
        private SqlConnection? _connection;

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
                _connection = new SqlConnection(_connectionString);
                await _connection.OpenAsync(cancellationToken);
                _transaction = _connection.BeginTransaction();

                // TODO: Очистка staging таблиц
                // await ClearStagingTablesAsync(cancellationToken);

                _logger.LogInformation("Target prepared successfully");
                return true;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to prepare target");
                throw;
            }
        }

        public async Task<bool> SaveDataAsync(DataTableDTO data, CancellationToken cancellationToken = default)
        {
            try
            {
                if (_connection == null || _transaction == null)
                    throw new InvalidOperationException("Target not prepared. Call PrepareTargetAsync first.");

                // TODO: Реализовать загрузку через SqlBulkCopy
                _logger.LogInformation("Processing {Count} records", data.TotalRows);

                // Временная заглушка
                await Task.Delay(100, cancellationToken);

                return true;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to save data");
                throw;
            }
        }

        public async Task<bool> FinalizeTargetAsync(bool success, CancellationToken cancellationToken = default)
        {
            try
            {
                if (_transaction != null)
                {
                    if (success)
                    {
                        await _transaction.CommitAsync(cancellationToken);
                        _logger.LogInformation("Transaction committed successfully");
                    }
                    else
                    {
                        await _transaction.RollbackAsync(cancellationToken);
                        _logger.LogWarning("Transaction rolled back");
                    }
                }

                _connection?.Close();
                _connection?.Dispose();
                _transaction?.Dispose();

                return true;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to finalize target");
                throw;
            }
        }
    }
}
