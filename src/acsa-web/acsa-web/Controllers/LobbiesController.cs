using acsa_web.Data;
using acsa_web.Extensions;
using acsa_web.Hubs;
using acsa_web.Models;
using acsa_web.Models.ViewModels;
using acsa_web.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.SignalR;
using Microsoft.EntityFrameworkCore;
using System.Net;
using System.Net.Sockets;
using System.Security.Claims;
using System.Text;
using System.Text.Json;

namespace acsa_web.Controllers
{
    [Authorize]
    public class LobbiesController : Controller
    {
        const string ServerWorkingDir = @"C:\Users\sahil\Documents\VS_Code\C++\assaultcube-acsa-fork\AC-1.3.0.2";

        private readonly UserManager<ApplicationUser> _userManager;
        private readonly ApplicationDbContext _db;
        private readonly IHubContext<LobbiesHub> _hub;
        private readonly AntiCheatCommandQueue _queue;

        public LobbiesController(UserManager<ApplicationUser> userManager, ApplicationDbContext db, IHubContext<LobbiesHub> hub, AntiCheatCommandQueue queue)
        {
            _userManager = userManager;
            _db = db;
            _hub = hub;
            _queue = queue;
        }

        [HttpGet]
        public async Task<IActionResult> Index([FromQuery] int? openCreate)
        {
            var shouldOpen = openCreate == 1 || (Request.Query["openCreate"] == "true");

            var me = await _userManager.GetUserAsync(User);
            if (me is null) return Challenge();
            if (!me.IsAccountLinkedWithGame) return RedirectToAction("LinkAccountWithGame", "Home");

            var myId = me.Id;

            var myActive = await _db.LobbyUsers
                .Where(x => x.UserId == myId && x.IsActive)
                .Select(x => new { x.LobbyId, LobbyName = x.Lobby.Name })
                .FirstOrDefaultAsync();

            var myActiveLobbyId = myActive?.LobbyId;

            var model = new LobbyListVm
            {
                MyActiveLobbyId = myActiveLobbyId,
                MyActiveLobbyName = myActive?.LobbyName,

                Lobbies = await _db.Lobbies
                    .Select(l => new LobbyItemVm
                    {
                        Id = l.Id,
                        Name = l.Name,
                        OwnerUserName = _db.Users
                            .Where(u => u.Id == l.OwnerUserId)
                            .Select(u => u.UserName)
                            .FirstOrDefault() ?? "Unknown",

                        PlayerCount = l.Users.Count(u => u.IsActive),
                        IsMember = l.Users.Any(u => u.UserId == myId && u.IsActive),
                        InAnotherLobby = myActiveLobbyId != null && myActiveLobbyId != l.Id,

                        IsMyActiveLobby = (myActiveLobbyId != null && l.Id == myActiveLobbyId)
                    })
                    .OrderByDescending(x => x.IsMyActiveLobby)
                    .ThenByDescending(x => x.PlayerCount)
                    .ThenBy(x => x.Name)
                    .ToListAsync()
            };

            ViewBag.OpenCreate = shouldOpen;
            return View(model);
        }

        [HttpGet]
        public async Task<IActionResult> ListPartial()
        {
            var me = await _userManager.GetUserAsync(User);
            if (me is null) return Unauthorized();

            var myId = me.Id;

            var myActive = await _db.LobbyUsers
                .Where(x => x.UserId == myId && x.IsActive)
                .Select(x => new { x.LobbyId, LobbyName = x.Lobby.Name })
                .FirstOrDefaultAsync();

            var myActiveLobbyId = myActive?.LobbyId;

            var lobbies = await _db.Lobbies
                .Select(l => new LobbyItemVm
                {
                    Id = l.Id,
                    Name = l.Name,
                    OwnerUserName = _db.Users
                        .Where(u => u.Id == l.OwnerUserId)
                        .Select(u => u.UserName)
                        .FirstOrDefault() ?? "Unknown",

                    PlayerCount = l.Users.Count(u => u.IsActive),
                    IsMember = l.Users.Any(u => u.UserId == myId && u.IsActive),
                    InAnotherLobby = myActiveLobbyId != null && myActiveLobbyId != l.Id,

                    IsMyActiveLobby = (myActiveLobbyId != null && l.Id == myActiveLobbyId)
                })
                .OrderByDescending(x => x.IsMyActiveLobby)
                .ThenByDescending(x => x.PlayerCount)
                .ThenBy(x => x.Name)
                .ToListAsync();

            return PartialView("_LobbyCardListPartial", lobbies);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> SaveSelection(int id, string map, int mode)
        {
            var me = await _userManager.GetUserAsync(User);
            if (me is null) return Challenge();

            var lobby = await _db.Lobbies.FirstOrDefaultAsync(l => l.Id == id);
            if (lobby == null) return NotFound();

            if (lobby.OwnerUserId != me.Id) return Forbid(); // only host can change

            lobby.SelectedMap = map;
            lobby.SelectedMode = mode;
            await _db.SaveChangesAsync();

            await _hub.Clients.Group($"lobby-{id}")
                .SendAsync("ReceiveMapPreview", map);
            await _hub.Clients.Group($"lobby-{id}")
                .SendAsync("ReceiveModeChanged", mode);

            return Ok();
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Create(LobbyCreateVm vm)
        {
            var user = await _userManager.GetUserAsync(User);
            if (user is null) return Challenge();

            if (!user.IsAccountLinkedWithGame)
                return RedirectToAction("LinkAccountWithGame", "Home");

            if (!ModelState.IsValid)
            {
                TempData["ErrorMessage"] = "Please provide a valid lobby name.";
                return RedirectToAction(nameof(Index));
            }

            var activeMembership = await _db.LobbyUsers
                .Include(lu => lu.Lobby)
                .FirstOrDefaultAsync(lu => lu.UserId == user.Id && lu.IsActive);

            if (activeMembership != null)
            {
                TempData["ErrorMessage"] = $"You are already in an active lobby ({activeMembership.Lobby?.Name ?? "Unknown"}). Leave it before creating a new one.";
                return RedirectToAction(nameof(Index));
            }

            var safeName = vm.Name.Trim();

            if (safeName.Contains(" ") || safeName.Length > 50)
            {
                TempData["ErrorMessage"] = "Lobby name must be 1-50 characters and contain no spaces.";
                return RedirectToAction(nameof(Index));
            }

            var lobby = new Lobby
            {
                Name = safeName,
                OwnerUserId = user.Id,
                CreatedAt = DateTime.UtcNow
            };

            var ownerLobbyUser = new LobbyUser
            {
                UserId = user.Id,
                Username = user.UserName,
                RegisteredOn = user.RegisteredOn,
                IsAccountLinkedWithGame = user.IsAccountLinkedWithGame,
                IsBanned = user.IsBanned,
                JoinedAt = DateTime.UtcNow,
                IsActive = true
            };

            lobby.Users.Add(ownerLobbyUser);

            _db.Lobbies.Add(lobby);
            await _db.SaveChangesAsync();

            await _hub.Clients.All.SendAsync("LobbyListChanged");

            TempData["SuccessMessage"] = "Lobby created successfully.";
            return RedirectToAction(nameof(Index));
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Join(int id)
        {
            var me = await _userManager.GetUserAsync(User);
            if (me is null) return Challenge();
            if (!me.IsAccountLinkedWithGame)
                return RedirectToAction("LinkAccountWithGame", "Home");

            var target = await _db.Lobbies.Include(l => l.Users).FirstOrDefaultAsync(l => l.Id == id);
            if (target == null)
            {
                TempData["ErrorMessage"] = "Lobby not found.";
                return RedirectToAction(nameof(Index));
            }

            var myActive = await _db.LobbyUsers.FirstOrDefaultAsync(x => x.UserId == me.Id && x.IsActive);
            if (myActive != null)
            {
                if (myActive.LobbyId == id)
                    return RedirectToAction("View", new { id });

                TempData["ErrorMessage"] = "You are already in another lobby. Leave it first to join a new one.";
                return RedirectToAction(nameof(Index));
            }

            if (target.Users.Count(u => u.IsActive) >= 10)
                return RedirectToAction("Error", "Home", new { message = "Lobby is full." });

            _db.LobbyUsers.Add(new LobbyUser
            {
                LobbyId = id,
                UserId = me.Id,
                Username = me.UserName,
                RegisteredOn = me.RegisteredOn,
                IsAccountLinkedWithGame = me.IsAccountLinkedWithGame,
                IsBanned = me.IsBanned,
                IsActive = true
            });

            await _db.SaveChangesAsync();

            // reload updated lobby
            var updatedLobby = await _db.Lobbies
                .Include(l => l.Users.Where(u => u.IsActive))
                .FirstAsync(l => l.Id == id);

            string playerListHtml = await this.RenderViewAsync("_PlayerListPartial", updatedLobby.Users, true);

            // broadcast new player list and count to the lobby
            await _hub.Clients.Group($"lobby-{id}").SendAsync("ReceivePlayerList", playerListHtml);
            await _hub.Clients.Group($"lobby-{id}").SendAsync("ReceivePlayersChanged", updatedLobby.Users.Count);

            // broadcast to index pages: update count (instant)
            await _hub.Clients.All.SendAsync("LobbyUpdated", id, updatedLobby.Users.Count);

            // broadcast refresh lobby list
            await _hub.Clients.All.SendAsync("LobbyListChanged");

            return RedirectToAction("View", new { id });
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Leave(int id)
        {
            var me = await _userManager.GetUserAsync(User);
            if (me is null) return Challenge();

            var lobby = await _db.Lobbies.Include(l => l.Users).FirstOrDefaultAsync(l => l.Id == id);
            if (lobby == null)
            {
                TempData["ErrorMessage"] = "Lobby not found.";
                return RedirectToAction(nameof(Index));
            }

            var membership = await _db.LobbyUsers
                .FirstOrDefaultAsync(x => x.LobbyId == id && x.UserId == me.Id);

            if (membership == null)
            {
                return RedirectToAction(nameof(Index));
            }

            using var tx = await _db.Database.BeginTransactionAsync();
            try
            {
                if (lobby.OwnerUserId == me.Id)
                {
                    // close server if running
                    if (lobby.ServerPid.HasValue)
                    {
                        await ServerHost.StopAsync(ServerWorkingDir, lobby.Id);
                        // mark port as unused
                        if (lobby.Port.HasValue && PortStore.PortDict.ContainsKey(lobby.Port.Value))
                        {
                            PortStore.PortDict[lobby.Port.Value] = "unused";
                        }

                        try
                        {
                            string configDir = Path.Combine(ServerWorkingDir, "config");
                            string configPath = Path.Combine(configDir, $"{lobby.Name}.txt");

                            if (System.IO.File.Exists(configPath))
                            {
                                System.IO.File.Delete(configPath);
                            }
                        }
                        catch (Exception ex)
                        {
                            Console.WriteLine($"[WARN] Could not delete config file: {ex.Message}");
                        }
                    }

                    // owner leaves then delete lobby
                    _db.Lobbies.Remove(lobby);
                    await _db.SaveChangesAsync();
                    await tx.CommitAsync();

                    // notify everyone in the lobby that it’s closed
                    _ = _hub.Clients.Group($"lobby-{id}")
                        .SendAsync("LobbyClosed", "Host has left! This lobby is closed.");

                    // update all index pages instantly
                    _ = _hub.Clients.All.SendAsync("LobbyListChanged");

                    TempData["SuccessMessage"] = "Lobby deleted.";
                    return RedirectToAction(nameof(Index));
                }
                else
                {
                    if (lobby.HasStarted && !lobby.HasFinished)
                    {
                        TempData["ErrorMessage"] = "You must finish the match before leaving.";
                        return RedirectToAction("View", new { id });
                    }

                    // normal player leaves
                    _db.LobbyUsers.Remove(membership);
                    await _db.SaveChangesAsync();
                    await tx.CommitAsync();

                    // reload player list
                    var updatedLobby = await _db.Lobbies
                        .Include(l => l.Users.Where(u => u.IsActive))
                        .FirstOrDefaultAsync(l => l.Id == id);

                    if (updatedLobby != null)
                    {
                        string playerListHtml = await this.RenderViewAsync("_PlayerListPartial", updatedLobby.Users, true);

                        // update all lobby members
                        _ = _hub.Clients.Group($"lobby-{id}")
                            .SendAsync("ReceivePlayerList", playerListHtml);
                        _ = _hub.Clients.Group($"lobby-{id}")
                            .SendAsync("ReceivePlayersChanged", updatedLobby.Users.Count);

                        // update count in lobby list page
                        _ = _hub.Clients.All.SendAsync("LobbyUpdated", id, updatedLobby.Users.Count);
                    }

                    // update all index pages instantly
                    _ = _hub.Clients.All.SendAsync("LobbyListChanged");

                    TempData["SuccessMessage"] = "You left the lobby.";
                    return RedirectToAction(nameof(Index));
                }
            }
            catch
            {
                await tx.RollbackAsync();
                TempData["ErrorMessage"] = "Could not process your request. Please try again.";
                return RedirectToAction(nameof(Index));
            }
        }

        [HttpGet]
        public async Task<IActionResult> View(int id)
        {
            var lobby = await _db.Lobbies
                .Include(l => l.Users.Where(u => u.IsActive))
                .FirstOrDefaultAsync(l => l.Id == id);

            if (lobby == null)
                return RedirectToAction(nameof(Index));

            if (lobby.HasStarted && !ServerHost.IsProcessRunning(lobby.ServerPid))
            {
                await ResetLobbyServerStateAsync(lobby);
                TempData["ErrorMessage"] = "Server was not running, lobby was reset.";

                lobby = await _db.Lobbies
                    .Include(l => l.Users.Where(u => u.IsActive))
                    .FirstOrDefaultAsync(l => l.Id == id);

                if (lobby == null)
                    return RedirectToAction(nameof(Index));
            }

            var currentUserId = User.FindFirst("sub")?.Value
                ?? User.FindFirst(ClaimTypes.NameIdentifier)?.Value;

            ViewBag.IsOwner = lobby.OwnerUserId == currentUserId;

            ViewBag.Maps = new[]
            {
                "ac_africa", "ac_arabian", "ac_arctic", "ac_arctic2",
                "ac_arid", "ac_complex", "ac_desert", "ac_desert2",
                "ac_desert3", "ac_douze", "ac_dusk", "ac_edifice",
                "ac_elevation", "ac_gothic", "ac_industrial", "ac_ingress",
                "ac_lainio", "ac_lotus", "ac_mines", "ac_nocturne",
                "ac_origin", "ac_rampart", "ac_rattrap", "ac_scaffold",
                "ac_snow", "ac_stellar", "ac_sunset", "ac_swamp",
                "ac_toxic", "ac_venison", "ac_wasteland", "ac_werk"
            };

            return View(lobby);
        }

        static async Task<string> GetPublicIPAsync()
        {
            try
            {
                using var http = new HttpClient();
                var ip = await http.GetStringAsync("https://api.ipify.org");
                return ip.Trim();
            }
            catch
            {
                return "";
            }
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Start(int id, string map, int mode)
        {
            var me = await _userManager.GetUserAsync(User);
            if (me is null) return Challenge();

            var lobby = await _db.Lobbies
                .Include(l => l.Users.Where(u => u.IsActive))
                .FirstOrDefaultAsync(l => l.Id == id);
            if (lobby == null) return RedirectToAction(nameof(Index));

            if (lobby.OwnerUserId != me.Id)
            {
                TempData["ErrorMessage"] = "Only the host can start the match.";
                return RedirectToAction("View", new { id });
            }

            var playerCount = lobby.Users.Count(u => u.IsActive);
            if (playerCount < 2)
            {
                TempData["ErrorMessage"] = "Need at least 2 players to start.";
                return RedirectToAction("View", new { id });
            }

            if (lobby.HasStarted)
            {
                TempData["ErrorMessage"] = "Match already started.";
                return RedirectToAction("View", new { id });
            }

            var usernames = lobby.Users.Select(u => u.Username).ToArray();
            var usersWithSecrets = await _db.Users.Where(u => usernames.Contains(u.UserName))
                                                  .Select(u => new { u.UserName, u.SharedSecret })
                                                  .ToListAsync();

            var f_userIds = usersWithSecrets.Select(u => $"{u.UserName}:{u.SharedSecret}").ToArray();

            var port = PortStore.PortDict.First(p => p.Value == "unused").Key;
            PortStore.PortDict[port] = "used";

            string xPass = Guid.NewGuid().ToString("N");
            string pPass = Guid.NewGuid().ToString("N");
            string tokenY = "2a4e3ad83a56d02368298860ac386533c653710d70e2f5e36c4df850a7731254";

            // build the launch command
            string args = string.Join('\n', new[]
            {
                $"-nAssaultCube Secure Arena! {lobby.Name}",
                "-oanti-temper secured server!",
                "-c10",
                $"-x{xPass}",
                $"-f{port}",
                $"-p{pPass}",
                $"-Y{tokenY}",
                $"--map={map}",
                $"--mode={mode}",
                $"--userids={string.Join(",", f_userIds)}",
                $"--lobbyId={lobby.Id.ToString()}",
                "-m"
            });

            // persist for consistency and refresh ui
            lobby.HasStarted = true;
            lobby.CurrentMap = map;
            lobby.CurrentMode = mode;
            lobby.XPassword = xPass;
            lobby.PPassword = pPass;
            lobby.TokenY = tokenY;
            lobby.Port = port;
            lobby.LaunchArgs = args;

            var pid = await ServerHost.StartAsync(
                lobbyId: lobby.Id,
                workingDir: ServerWorkingDir,
                argsText: args,
                port: port,
                lobbyName: lobby.Name);

            lobby.ServerPid = pid;
            await _db.SaveChangesAsync();

            // broadcast to everyone in this lobby, flip Start -> Connect
            await _hub.Clients.Group($"lobby-{id}")
                .SendAsync("ReceiveMatchStarted", map, mode, $"127.0.0.1:{port}", "Secure server session");

            // refresh the lobby list on the Index page
            await _hub.Clients.All.SendAsync("LobbyListChanged");

            // ajax submit support
            if (Request.Headers["X-Requested-With"] == "XMLHttpRequest")
                return Ok();

            TempData["SuccessMessage"] = "Match started!";
            return RedirectToAction("View", new { id });
        }

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
                .SendAsync("LobbyServerStopped", "Server is not running.");

            // refresh lobby list page cards
            await _hub.Clients.All.SendAsync("LobbyListChanged");
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Connect(int id)
        {
            var lobby = await _db.Lobbies
                .Include(l => l.Users.Where(u => u.IsActive))
                .FirstOrDefaultAsync(l => l.Id == id);
            if (lobby == null)
            {
                TempData["ErrorMessage"] = "Lobby not found.";
                return RedirectToAction("Index");
            }

            if (!lobby.HasStarted)
            {
                TempData["ErrorMessage"] = "Match hasn't started yet.";
                return RedirectToAction("View", new { id });
            }

            if (!ServerHost.IsProcessRunning(lobby.ServerPid))
            {
                await ResetLobbyServerStateAsync(lobby);
                TempData["ErrorMessage"] = "Server is not running anymore. Start the match again.";
                return RedirectToAction("View", new { id });
            }

            var userId = _userManager.GetUserId(User); // the user clicking connect
            if (string.IsNullOrEmpty(userId))
            {
                TempData["ErrorMessage"] = "Not logged in.";
                return RedirectToAction("View", new { id });
            }

            var ip = await GetPublicIPAsync();
            if (string.IsNullOrWhiteSpace(ip))
            {
                TempData["ErrorMessage"] = "Could not determine server IP address.";
                return RedirectToAction("View", new { id });
            }

            _queue.Enqueue(userId, new {
                command = "StartMatch",
                ip = ip,
                port = lobby.Port,
                password = lobby.PPassword
            });

            TempData["SuccessMessage"] = "Sent connect request.";
            return RedirectToAction("View", new { id });
        }
    }
}
