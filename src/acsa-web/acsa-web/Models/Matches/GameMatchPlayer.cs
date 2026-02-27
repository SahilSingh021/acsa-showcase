using System.ComponentModel.DataAnnotations;

namespace acsa_web.Models.Matches
{
    public class GameMatchPlayer
    {
        public int Id { get; set; }

        public int MatchId { get; set; }
        public GameMatch Match { get; set; }

        public string? UserId { get; set; }
        public ApplicationUser? User { get; set; }

        [MaxLength(64)]
        public string PersistentUid { get; set; }

        [MaxLength(64)]
        public string DisplayName { get; set; }

        public int? Team { get; set; }
        public int Kills { get; set; }
        public int Deaths { get; set; }
        public int? Flags { get; set; }
    }
}
