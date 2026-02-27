using acsa_web.Models.Matches;
using Microsoft.AspNetCore.Identity;
using System.ComponentModel.DataAnnotations;

namespace acsa_web.Models
{
    public class ApplicationUser : IdentityUser
    {
        [MaxLength(200)]
        public string FullName { get; set; }
        public DateTime RegisteredOn { get; set; } = DateTime.UtcNow;
        public bool IsBanned { get; set; } = false;
        public DateTime? BanDate { get; set; }

        public Guid SharedSecret { get; set; } = Guid.NewGuid();
        public string? PersistentUid { get; set; }

        public bool IsAccountLinkedWithGame { get; set; } = false;

        public ICollection<UserHwid> Hwids { get; set; } = new List<UserHwid>();
        public ICollection<UserIpAddress> IpAddresses { get; set; } = new List<UserIpAddress>();

        public ICollection<UserLog> Logs { get; set; } = new List<UserLog>();
        public ICollection<GameMatchPlayer> MatchPlayers { get; set; } = new List<GameMatchPlayer>();
        public ICollection<AdminLog> AdminLogsPerformed { get; set; } = new List<AdminLog>();
    }

    public class UserHwid
    {
        public int Id { get; set; }

        [Required, MaxLength(64)]
        public string Value { get; set; }

        public DateTime UsedAtDateTime { get; set; } = DateTime.UtcNow;

        [Required]
        public string UserId { get; set; }
        public ApplicationUser User { get; set; }
    }

    public class UserIpAddress
    {
        public int Id { get; set; }

        [Required, MaxLength(45)]
        public string Value { get; set; }

        public DateTime UsedAtDateTime { get; set; } = DateTime.UtcNow;

        [Required]
        public string UserId { get; set; }
        public ApplicationUser User { get; set; }
    }

    public enum UserLogLevel
    {
        Info = 0,
        Update = 1,
        Malicious = 2,
        Warning = 3,
        Error = 4
    }

    public class UserLog
    {
        public int Id { get; set; }

        [Required]
        public string Message { get; set; }

        [Required]
        public UserLogLevel Level { get; set; } = UserLogLevel.Info;

        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

        [Required]
        public string UserId { get; set; }
        public ApplicationUser User { get; set; }
    }

    public enum AdminLogLevel
    {
        Info = 0,
        Update = 1,
        Warning = 2,
        Error = 3
    }

    public class AdminLog
    {
        public int Id { get; set; }

        [Required]
        public string Message { get; set; }

        [Required]
        public AdminLogLevel Level { get; set; } = AdminLogLevel.Info;

        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

        [Required]
        public string AdminUserId { get; set; }
        public ApplicationUser AdminUser { get; set; }
    }
}
