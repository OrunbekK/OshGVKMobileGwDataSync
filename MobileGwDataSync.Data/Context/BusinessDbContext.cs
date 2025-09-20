using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using System.Data;

namespace MobileGwDataSync.Data.Context
{
    /// <summary>
    /// Контекст для работы с бизнес БД SQL Server
    /// Используется для загрузки данных из 1С
    /// </summary>
    public class BusinessDbContext : DbContext
    {
        private readonly string _connectionString;

        public BusinessDbContext(DbContextOptions<BusinessDbContext> options)
            : base(options)
        {
            _connectionString = Database.GetConnectionString() ?? string.Empty;
        }

        /// <summary>
        /// Получить подключение для Dapper
        /// </summary>
        public IDbConnection CreateConnection()
            => new SqlConnection(_connectionString);

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            base.OnModelCreating(modelBuilder);

            // Здесь не создаем модели, так как работаем через 
            // хранимые процедуры и TVP с существующей БД
            // Все операции через Dapper и SqlBulkCopy
        }
    }
}
