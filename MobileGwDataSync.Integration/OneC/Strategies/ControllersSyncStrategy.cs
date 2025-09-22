using Microsoft.Extensions.Logging;
using MobileGwDataSync.Core.Exceptions;
using MobileGwDataSync.Core.Models.DTO;
using MobileGwDataSync.Integration.Models;
using Newtonsoft.Json;
using System.Data;

namespace MobileGwDataSync.Integration.OneC.Strategies
{
    public class ControllersSyncStrategy : BaseSyncStrategy
    {
        public override string EntityName => "Controllers";
        public override string Endpoint => "getControllers";

        public ControllersSyncStrategy(ILogger<ControllersSyncStrategy> logger) : base(logger) { }

        public override string GetTargetProcedure() => "USP_MA_MergeControllers";

        public override DataTableDTO ParseResponse(string jsonResponse)
        {
            // Проверяем что jsonResponse не null
            if (string.IsNullOrEmpty(jsonResponse))
            {
                _logger.LogWarning("Received empty response from 1C");
                return new DataTableDTO
                {
                    Source = EntityName,
                    FetchedAt = DateTime.UtcNow,
                    Columns = new List<string> { "UID", "Controller", "ControllerId" }
                };
            }

            _logger.LogDebug("Raw response length: {Length} chars", jsonResponse.Length);

            OneCControllersResponse? response;
            try
            {
                response = JsonConvert.DeserializeObject<OneCControllersResponse>(jsonResponse);
            }
            catch (JsonException ex)
            {
                _logger.LogError(ex, "Failed to deserialize controllers response");
                throw new DataSourceException("Invalid JSON response from 1C", ex);
            }

            if (response == null || !response.Success)
            {
                _logger.LogWarning("1C returned unsuccessful response or null");
                throw new DataSourceException("1C returned unsuccessful response");
            }

            var dataTable = new DataTableDTO
            {
                Source = EntityName,
                FetchedAt = DateTime.UtcNow,
                Columns = new List<string> { "UID", "Controller", "ControllerId" }
            };

            if (response.Controllers != null && response.Controllers.Any())
            {
                foreach (var controller in response.Controllers)
                {
                    // Проверяем и логируем каждый контроллер
                    if (controller == null)
                    {
                        _logger.LogWarning("Null controller in response, skipping");
                        continue;
                    }

                    _logger.LogDebug("Controller: UID={UID}, Name={Name}, ID={ID}",
                        controller.UID,
                        controller.Controller ?? "NULL",
                        controller.ControllerId ?? "NULL");

                    // Проверяем обязательные поля
                    if (string.IsNullOrEmpty(controller.ControllerId))
                    {
                        _logger.LogWarning("Controller without ControllerId, skipping");
                        continue;
                    }

                    // Если UID пустой, генерируем на основе ControllerId
                    var uid = controller.UID;
                    if (uid == Guid.Empty)
                    {
                        // Генерируем детерминированный GUID на основе ControllerId
                        using (var md5 = System.Security.Cryptography.MD5.Create())
                        {
                            var hash = md5.ComputeHash(
                                System.Text.Encoding.UTF8.GetBytes($"controller_{controller.ControllerId}")
                            );
                            uid = new Guid(hash);
                        }

                        _logger.LogInformation("Generated UID {UID} for controller {ID}",
                            uid, controller.ControllerId);
                    }

                    dataTable.Rows.Add(new Dictionary<string, object>
                    {
                        ["UID"] = uid,
                        ["Controller"] = controller.Controller ?? string.Empty,
                        ["ControllerId"] = controller.ControllerId
                    });
                }

                _logger.LogInformation("Successfully parsed {Count} controllers", dataTable.TotalRows);
            }
            else
            {
                _logger.LogWarning("No controllers in response");
            }

            return dataTable;
        }

        public override DataTable CreateTVP()
        {
            var table = new DataTable();
            table.Columns.Add("UID", typeof(Guid));
            table.Columns.Add("Controller", typeof(string));
            table.Columns.Add("ControllerId", typeof(string));
            return table;
        }

        public override void PopulateTVP(DataTable table, DataTableDTO data)
        {
            foreach (var row in data.Rows)
            {
                var dataRow = table.NewRow();
                dataRow["UID"] = row.GetValueOrDefault("UID", string.Empty);
                dataRow["Controller"] = row.GetValueOrDefault("Controller", string.Empty);
                dataRow["ControllerId"] = row.GetValueOrDefault("ControllerId", string.Empty);
                table.Rows.Add(dataRow);
            }
        }
    }
}
