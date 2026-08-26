namespace DentalSync.ViewModels
{
    public class AuditLogViewModel
    {
        public string Search { get; set; } = "";
        public string Role   { get; set; } = "";
        public List<string> Roles { get; set; } = new();
        public List<AuditLogEntry> Logs { get; set; } = new();
        public int TotalLogs  { get; set; }
        public int Page       { get; set; } = 1;
        public int PageSize   { get; set; } = 5;
        public int TotalPages => (int)Math.Ceiling((double)TotalLogs / PageSize);
    }

    public class AuditLogEntry
    {
        public DateTime DateTime   { get; set; }
        public string User        { get; set; } = "";
        public string Role        { get; set; } = "";
        public string Action      { get; set; } = "";
        public string Module      { get; set; } = "";
        public string Description { get; set; } = "";
        public string IpAddress   { get; set; } = "";
    }
}
