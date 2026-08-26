namespace DentalSync.ViewModels
{
    public class AuthenticationLogViewModel
    {
        public string Search { get; set; } = "";
        public string Role { get; set; } = "";
        public List<string> Roles { get; set; } = new();
        public List<AuthenticationLogEntry> Logs { get; set; } = new();
        public int TotalLogs { get; set; }
        public int Page { get; set; } = 1;
        public int PageSize { get; set; } = 5;
        public int TotalPages => (int)Math.Ceiling((double)TotalLogs / PageSize);

        // KPI Metrics
        public int ActiveSessions { get; set; }
        public int FailedLogins { get; set; }
        public int LockedAccounts { get; set; }
    }

    public class AuthenticationLogEntry
    {
        public DateTime DateTime { get; set; }
        public string User { get; set; } = "";
        public string Event { get; set; } = ""; // e.g. "Login", "Logout"
        public string Status { get; set; } = ""; // e.g. "Success", "Failed"
        public string IpAddress { get; set; } = "";
    }
}
