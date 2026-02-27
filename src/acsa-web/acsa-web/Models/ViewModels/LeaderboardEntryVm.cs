namespace acsa_web.Models.ViewModels
{
    public class LeaderboardEntryVm
    {
        public int Rank { get; set; }
        public string UserName { get; set; } = "unknown";

        public int Kills { get; set; }
        public int Deaths { get; set; }
        public int? TotalFlagsCaptured { get; set; }

        public double KdRatio { get; set; }
        public long TotalTimeMs { get; set; }
        public DateTime? LastPlayed { get; set; }

        public string TotalTimePretty => TimeFormat.PrettyDuration(TotalTimeMs);
    }

    public static class TimeFormat
    {
        public static string PrettyDuration(long ms)
        {
            if (ms <= 0) return "0m";

            var ts = TimeSpan.FromMilliseconds(ms);

            if (ts.TotalDays >= 1)
                return $"{(int)ts.TotalDays}d {ts.Hours}h {ts.Minutes}m";

            if (ts.TotalHours >= 1)
                return $"{(int)ts.TotalHours}h {ts.Minutes}m";

            return $"{ts.Minutes}m";
        }
    }
}


