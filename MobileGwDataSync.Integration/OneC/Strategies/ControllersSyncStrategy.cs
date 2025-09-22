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
            var response = JsonConvert.DeserializeObject<OneCControllersResponse>(jsonResponse);

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

            if (response?.Controllers != null)
            {
                foreach (var controller in response.Controllers)
                {
                    if (controller.UID == Guid.Empty)
                    {
                        _logger.LogWarning("Skipping controller with empty \"UID\"");
                        continue;
                    }

                    if (string.IsNullOrEmpty(controller.Controller))
                    {
                        _logger.LogWarning("Skipping controller with empty \"Controller\"");
                        continue;
                    }

                    if (string.IsNullOrEmpty(controller.ControllerId))
                    {
                        _logger.LogWarning("Skipping controller with empty \"ControllerId\"");
                        continue;
                    }

                    dataTable.Rows.Add(new Dictionary<string, object>
                    {
                        ["UID"] = controller.UID,
                        ["Controller"] = controller.Controller,
                        ["ControllerId"] = controller.ControllerId
                    });
                }
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
