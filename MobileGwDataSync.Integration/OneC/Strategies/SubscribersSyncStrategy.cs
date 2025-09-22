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
            var response = JsonConvert.DeserializeObject<OneCSubscribersResponse>(jsonResponse);
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
    }
}
