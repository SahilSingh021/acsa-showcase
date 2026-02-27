using System.ComponentModel.DataAnnotations;

namespace acsa_web.Models.ViewModels
{
    public class LobbyItemVm
    {
        public int Id { get; set; }
        public string Name { get; set; }
        public string OwnerUserName { get; set; }
        public int PlayerCount { get; set; }
        public int MaxPlayers { get; set; } = 10;
        public bool IsFull => PlayerCount >= MaxPlayers;
        public bool IsMember { get; set; }
        public bool InAnotherLobby { get; set; }
        public bool IsMyActiveLobby { get; set; }
    }

    public class LobbyListVm
    {
        public IList<LobbyItemVm> Lobbies { get; set; } = new List<LobbyItemVm>();
        public int? MyActiveLobbyId { get; set; }
        public string MyActiveLobbyName { get; set; }
    }

    public class LobbyCreateVm
    {
        [Required]
        [StringLength(50, MinimumLength = 1, ErrorMessage = "Lobby name must be 1-50 characters.")]
        [RegularExpression(@"^\S+$", ErrorMessage = "Lobby name cannot contain spaces.")]
        public string Name { get; set; } = string.Empty;
    }
}
