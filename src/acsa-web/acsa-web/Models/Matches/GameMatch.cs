using System.ComponentModel.DataAnnotations;

namespace acsa_web.Models.Matches
{
    public class GameMatch
    {
        public int Id { get; set; }

        [MaxLength(64)]
        public string Map { get; set; }

        [MaxLength(64)]
        public string Mode { get; set; }

        public int LengthMs { get; set; }

        public DateTime EndedAt { get; set; } = DateTime.UtcNow;

        public string? RawJson { get; set; }

        public ICollection<GameMatchPlayer> Players { get; set; }
            = new List<GameMatchPlayer>();
    }
}
