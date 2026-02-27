using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.SignalR;
using System.Threading.Tasks;

namespace acsa_web.Hubs
{
    [Authorize]
    public class LobbiesHub : Hub
    {
        private static string GroupName(int lobbyId) => $"lobby-{lobbyId}";

        public async Task JoinLobby(int lobbyId)
        {
            await Groups.AddToGroupAsync(Context.ConnectionId, GroupName(lobbyId));
        }

        // changes to map, broadcast to everyone
        public Task MapPreview(int lobbyId, string map)
            => Clients.Group(GroupName(lobbyId)).SendAsync("ReceiveMapPreview", map);

        // changes to mode, broadcast to everyone
        public Task ModeChanged(int lobbyId, int mode)
            => Clients.Group(GroupName(lobbyId)).SendAsync("ReceiveModeChanged", mode);

        // update player count
        public Task PlayersChanged(int lobbyId, int count)
            => Clients.Group(GroupName(lobbyId)).SendAsync("ReceivePlayersChanged", count);

        // host starts the match, flip everyone to Connect
        public Task MatchStarted(int lobbyId, string map, int mode, string connectHost, int connectPort)
            => Clients.Group(GroupName(lobbyId))
                      .SendAsync("ReceiveMatchStarted", map, mode, connectHost, connectPort);

        public async Task SendLobbyUpdated(int lobbyId, int playerCount)
        {
            await Clients.All.SendAsync("LobbyUpdated", lobbyId, playerCount);
        }
    }
}
