using MobileGwDataSync.Core.Models.DTO;

namespace MobileGwDataSync.API.Models.Responses.Sync
{
    public class SyncRunDetailDTO : SyncRunDTO
    {
        public List<SyncRunStepDTO> Steps { get; set; }
        public List<MetricDTO> Metrics { get; set; }
    }
}
