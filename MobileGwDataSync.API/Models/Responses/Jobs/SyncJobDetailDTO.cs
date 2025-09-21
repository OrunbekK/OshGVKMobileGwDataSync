using MobileGwDataSync.API.Controllers;

namespace MobileGwDataSync.API.Models.Responses.Jobs
{
    public class SyncJobDetailDTO : SyncJobDTO
    {
        public Dictionary<string, string> Configuration { get; set; }
        public List<SyncRunSummaryDTO> RecentRuns { get; set; }
        public DateTime? PreviousFireTime { get; set; }
    }
}
