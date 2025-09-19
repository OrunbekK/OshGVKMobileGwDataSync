namespace MobileGwDataSync.Core.Models.DTO
{
    /// <summary>
    /// DTO для табличных данных из 1С
    /// </summary>
    public class DataTableDTO
    {
        public List<string> Columns { get; set; } = new();
        public List<Dictionary<string, object>> Rows { get; set; } = new();
        public int TotalRows => Rows.Count;
        public DateTime FetchedAt { get; set; }
        public string Source { get; set; } = string.Empty;
    }
}
