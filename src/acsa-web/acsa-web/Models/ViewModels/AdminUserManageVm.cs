using System;
using System.Collections.Generic;
using acsa_web.Models;
using acsa_web.Models.Matches;

namespace acsa_web.Models.ViewModels
{
    public class AdminUserManageVm
    {
        public string UserId { get; set; }
        public string Username { get; set; }

        public string? FullName { get; set; }
        public string? Email { get; set; }
        public string? PhoneNumber { get; set; }

        public bool IsBanned { get; set; }
        public DateTime? BanDate { get; set; }

        public DateTime RegisteredOn { get; set; }
        public bool IsAccountLinkedWithGame { get; set; }
        public string? PersistentUid { get; set; }

        public List<UserLog> RecentUserLogs { get; set; } = new();
        public List<GameMatchPlayer> RecentMatches { get; set; } = new();
    }
}
