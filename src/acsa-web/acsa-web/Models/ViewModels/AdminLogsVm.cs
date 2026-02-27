namespace acsa_web.Models.ViewModels
{
    public class AdminLogsVm
    {
        public string? AdminUserName { get; set; }

        public string? Q { get; set; }
        public AdminLogLevel? Level { get; set; }
        public int Take { get; set; } = 200;

        public List<AdminLog> Logs { get; set; } = new();
    }
}
