using acsa_web.Data;
using acsa_web.Hubs;
using acsa_web.Models;
using acsa_web.Models.Matches;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.SignalR;
using Microsoft.EntityFrameworkCore;
using System.Security.Claims;
using System.Text.Json;

[ApiController]
[Route("api/link")]
public class LinkController : ControllerBase
{
    private readonly ApplicationDbContext _db;
    private readonly ILogger<LinkController> _logger;
    private readonly IHubContext<LobbiesHub> _hub;

    public LinkController(ApplicationDbContext db, ILogger<LinkController> logger, IHubContext<LobbiesHub> hub)
    {
        _db = db;
        _logger = logger;
        _hub = hub;
    }
    public record EndGameResultsDto(
        int lobbyId,
        string map,
        string mode,
        int length_ms,
        List<string>? userids,
        List<PlayerDto> players
    );

    public record PlayerDto(
        string persistent_uid,
        string name,
        int team,
        int kills,
        int deaths,
        int? flags
    );

    private async Task ResetLobbyServerStateAsync(Lobby lobby)
    {
        lobby.HasStarted = false;
        lobby.ServerPid = null;
        lobby.Port = null;
        lobby.CurrentMap = null;
        lobby.CurrentMode = null;
        lobby.XPassword = null;
        lobby.PPassword = null;
        lobby.TokenY = null;
        lobby.LaunchArgs = null;

        await _db.SaveChangesAsync();

        await _hub.Clients.Group($"lobby-{lobby.Id}")
            .SendAsync("MatchEnded", lobby.Id);

        await _hub.Clients.All.SendAsync("LobbyListChanged");
    }

    [HttpPost("endgameresults")]
    public async Task<ActionResult<bool>> EndGameResults([FromBody] EndGameResultsDto dto)
    {
        if (dto is null)
            return BadRequest(false);

        if (string.IsNullOrWhiteSpace(dto.map) ||
            string.IsNullOrWhiteSpace(dto.mode) ||
            dto.players is null || dto.players.Count <= 1) // skip matches with less than 2 players, not a real match
            return BadRequest(false);

        var now = DateTime.UtcNow;

        var rawJson = JsonSerializer.Serialize(dto);

        _logger.LogInformation(
            "EndGameResults received: Map={Map} Mode={Mode} LengthMs={Len} Players={Count}",
            dto.map, dto.mode, dto.length_ms, dto.players.Count
        );

        // create match row
        var match = new GameMatch
        {
            Map = dto.map,
            Mode = dto.mode,
            LengthMs = dto.length_ms,
            EndedAt = now,
            RawJson = rawJson
        };

        _db.GameMatches.Add(match);
        await _db.SaveChangesAsync();

        // map persistent_uid -> AspNetUsers by Username
        var uids = dto.players
            .Select(p => p.persistent_uid)
            .Where(u => !string.IsNullOrWhiteSpace(u))
            .Distinct()
            .ToList();

        var userMap = await _db.Users
            .Where(u => u.PersistentUid != null && uids.Contains(u.PersistentUid))
            .Select(u => new { u.Id, u.PersistentUid })
            .ToDictionaryAsync(x => x.PersistentUid!, x => x.Id);

        // insert player rows
        foreach (var p in dto.players)
        {
            userMap.TryGetValue(p.persistent_uid, out var userId);

            _db.GameMatchPlayers.Add(new GameMatchPlayer
            {
                MatchId = match.Id,
                UserId = userId,
                PersistentUid = p.persistent_uid,
                DisplayName = p.name,
                Team = p.team,
                Kills = p.kills,
                Deaths = p.deaths,
                Flags = p.flags
            });
        }

        await _db.SaveChangesAsync();

        var lobby = await _db.Lobbies.FirstOrDefaultAsync(l => l.Id == dto.lobbyId);
        if (lobby != null && lobby.HasStarted)
        {
            await ResetLobbyServerStateAsync(lobby);
        }

        return Ok(true);
    }
}
