namespace acsa_web.Models.ViewModels
{
    public class PlayerMatchHistoryVm
    {
        public string UserName { get; set; } = "unknown";
        public int TotalMatches { get; set; }
        public int TotalKills { get; set; }
        public int TotalDeaths { get; set; }
        public int? TotalFlagsCaptured { get; set; }
        public double KdRatio { get; set; }

        public long TotalTimeMs { get; set; }
        public string TotalTimePretty => TimeFormat.PrettyDuration(TotalTimeMs);

        public List<MatchRow> Matches { get; set; } = new();

        public class MatchRow
        {
            public int MatchId { get; set; }
            public string Map { get; set; } = "";
            public string Mode { get; set; } = "";
            public int LengthMs { get; set; }
            public DateTime EndedAt { get; set; }

            public int Kills { get; set; }
            public int Deaths { get; set; }
            public int? Flags { get; set; }

            public string LengthPretty => TimeFormat.PrettyDuration(LengthMs);
        }
    }
}
