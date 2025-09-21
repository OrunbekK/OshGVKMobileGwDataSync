using Microsoft.Extensions.Logging;
using MobileGwDataSync.Core.Exceptions;
using MobileGwDataSync.Core.Models.DTO;
using MobileGwDataSync.Integration.Models;
using Newtonsoft.Json;
using System.Data;

namespace MobileGwDataSync.Integration.OneC.Strategies
{
    public class SubscribersSyncStrategy : BaseSyncStrategy
    {
        public override string EntityName => "Subscribers";
        public override string Endpoint => "subscribers";

        public SubscribersSyncStrategy(ILogger<SubscribersSyncStrategy> logger) : base(logger) { }

        public override string GetTargetProcedure() => "USP_MA_MergeSubscribers";

        public override DataTableDTO ParseResponse(string jsonResponse)
        {
            var response = JsonConvert.DeserializeObject<OneCResponseWrapper>(jsonResponse);
            if (response == null || !response.Success)
                throw new DataSourceException("1C returned unsuccessful response");

            var dataTable = new DataTableDTO
            {
                Source = EntityName,
                FetchedAt = DateTime.UtcNow,
                Columns = new List<string> {
                    "Account", "Subscriber", "Address", "Balance",
                    "Type", "State", "ControllerId", "RouteId"
                }
            };

            if (response.Subscribers != null)
            {
                foreach (var subscriber in response.Subscribers.Where(s => !string.IsNullOrEmpty(s.Account)))
                {
                    dataTable.Rows.Add(new Dictionary<string, object>
                    {
                        ["Account"] = subscriber.Account,
                        ["Subscriber"] = subscriber.FIO ?? string.Empty,
                        ["Address"] = subscriber.Address ?? string.Empty,
                        ["Balance"] = subscriber.Balance,
                        ["Type"] = subscriber.Type,
                        ["State"] = subscriber.State ?? string.Empty,
                        ["ControllerId"] = subscriber.ControllerId ?? string.Empty,
                        ["RouteId"] = subscriber.RouteId ?? string.Empty
                    });
                }
            }

            return dataTable;
        }

        public override DataTable CreateTVP()
        {
            var table = new DataTable();
            table.Columns.Add("Account", typeof(string));
            table.Columns.Add("Subscriber", typeof(string));
            table.Columns.Add("Address", typeof(string));
            table.Columns.Add("Balance", typeof(decimal));
            table.Columns.Add("Type", typeof(byte));
            table.Columns.Add("State", typeof(string));
            table.Columns.Add("ControllerId", typeof(string));
            table.Columns.Add("RouteId", typeof(string));
            return table;
        }

        public override void PopulateTVP(DataTable table, DataTableDTO data)
        {
            foreach (var row in data.Rows)
            {
                var dataRow = table.NewRow();
                dataRow["Account"] = row.GetValueOrDefault("Account", string.Empty);
                dataRow["Subscriber"] = row.GetValueOrDefault("Subscriber", string.Empty);
                dataRow["Address"] = row.GetValueOrDefault("Address", string.Empty);
                dataRow["Balance"] = Convert.ToDecimal(row.GetValueOrDefault("Balance", 0m));
                dataRow["Type"] = Convert.ToByte(row.GetValueOrDefault("Type", 0));
                dataRow["State"] = row.GetValueOrDefault("State", string.Empty);
                dataRow["ControllerId"] = row.GetValueOrDefault("ControllerId", string.Empty);
                dataRow["RouteId"] = row.GetValueOrDefault("RouteId", string.Empty);
                table.Rows.Add(dataRow);
            }
        }
    }
}
