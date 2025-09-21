using Microsoft.Extensions.Logging;
using MobileGwDataSync.Core.Models.DTO;
using Newtonsoft.Json;
using System.Data;

namespace MobileGwDataSync.Integration.OneC.Strategies
{
    public class ControllersSyncStrategy : BaseSyncStrategy
    {
        public override string EntityName => "Controllers";
        public override string Endpoint => "controllers";

        public ControllersSyncStrategy(ILogger<ControllersSyncStrategy> logger) : base(logger) { }

        public override string GetTargetProcedure() => "USP_MA_MergeControllers";

        public override DataTableDTO ParseResponse(string jsonResponse)
        {
            var response = JsonConvert.DeserializeObject<OneCControllersResponse>(jsonResponse);

            var dataTable = new DataTableDTO
            {
                Source = EntityName,
                FetchedAt = DateTime.UtcNow,
                Columns = new List<string> { "Id", "Name", "Address", "IsActive" }
            };

            if (response?.Controllers != null)
            {
                foreach (var controller in response.Controllers)
                {
                    dataTable.Rows.Add(new Dictionary<string, object>
                    {
                        ["Id"] = controller.Id,
                        ["Name"] = controller.Name,
                        ["Address"] = controller.Address ?? string.Empty,
                        ["IsActive"] = controller.IsActive
                    });
                }
            }

            return dataTable;
        }

        public override DataTable CreateTVP()
        {
            var table = new DataTable();
            table.Columns.Add("Id", typeof(string));
            table.Columns.Add("Name", typeof(string));
            table.Columns.Add("Address", typeof(string));
            table.Columns.Add("IsActive", typeof(bool));
            return table;
        }

        public override void PopulateTVP(DataTable table, DataTableDTO data)
        {
            foreach (var row in data.Rows)
            {
                var dataRow = table.NewRow();
                dataRow["Id"] = row.GetValueOrDefault("Id", string.Empty);
                dataRow["Name"] = row.GetValueOrDefault("Name", string.Empty);
                dataRow["Address"] = row.GetValueOrDefault("Address", string.Empty);
                dataRow["IsActive"] = Convert.ToBoolean(row.GetValueOrDefault("IsActive", false));
                table.Rows.Add(dataRow);
            }
        }
    }
}
