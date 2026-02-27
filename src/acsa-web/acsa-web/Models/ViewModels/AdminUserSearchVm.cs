using System.Collections.Generic;

namespace acsa_web.Models.ViewModels
{
    public class AdminUserSearchVm
    {
        public string? Username { get; set; }
        public List<ApplicationUser> Results { get; set; } = new();
    }
}
