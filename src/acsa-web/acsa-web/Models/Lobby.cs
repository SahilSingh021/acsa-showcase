using System.ComponentModel.DataAnnotations;

namespace acsa_web.Models
{
    public class Lobby
    {
        public int Id { get; set; }

        [Required, MaxLength(100)]
        public string Name { get; set; }

        [MaxLength(450)]
        public string OwnerUserId { get; set; }
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
        public ICollection<LobbyUser> Users { get; set; } = new List<LobbyUser>();
        public bool IsFull => Users.Count >= 10;

        public string? SelectedMap { get; set; }
        public int? SelectedMode { get; set; }

        public bool HasStarted { get; set; } = false;
        public bool HasFinished { get; set; } = false;
        public string? CurrentMap { get; set; }
        public int? CurrentMode { get; set; }

        [MaxLength(128)]
        public string? XPassword { get; set; }
        [MaxLength(128)]
        public string? PPassword { get; set; }
        [MaxLength(256)]
        public string? TokenY { get; set; }
        public int? Port { get; set; }
        public string? LaunchArgs { get; set; }
        public int? ServerPid { get; set; }
    }

    public class LobbyUser
    {
        public int Id { get; set; }
        public int LobbyId { get; set; }
        public Lobby Lobby { get; set; }

        [Required, MaxLength(450)]
        public string UserId { get; set; }

        [Required, MaxLength(64)]
        public string Username { get; set; }
        public DateTime RegisteredOn { get; set; }
        public bool IsAccountLinkedWithGame { get; set; }
        public bool IsBanned { get; set; }
        public DateTime JoinedAt { get; set; } = DateTime.UtcNow;
        public bool IsActive { get; set; } = true;
    }
}
