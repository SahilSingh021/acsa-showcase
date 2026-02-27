using acsa_web.Data;
using acsa_web.Models;
using acsa_web.Models.ViewModels;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace acsa_web.Controllers
{
    public class PlayersController : Controller
    {
        private readonly ApplicationDbContext _db;
        private readonly UserManager<ApplicationUser> _userManager;

        public PlayersController(ApplicationDbContext db, UserManager<ApplicationUser> userManager)
        {
            _db = db;
            _userManager = userManager;
        }

        [Authorize]
        [HttpGet]
        public async Task<IActionResult> History(int take = 50)
        {
            var user = await _userManager.GetUserAsync(User);

            if (user == null)
            {
                return RedirectToPage("/Account/Login", new { area = "Identity" });
            }

            if (string.IsNullOrWhiteSpace(user.PersistentUid))
            {
                TempData["ErrorMessage"] = "You do not have a linked game account.";
                return RedirectToAction("Index", "Leaderboard");
            }

            var uid = user.PersistentUid;

            // pull recent matches for this player
            var matches = await (
                from p in _db.GameMatchPlayers
                join m in _db.GameMatches on p.MatchId equals m.Id
                where p.PersistentUid == uid
                orderby m.EndedAt descending
                select new PlayerMatchHistoryVm.MatchRow
                {
                    MatchId = m.Id,
                    Map = m.Map,
                    Mode = m.Mode,
                    LengthMs = m.LengthMs,
                    EndedAt = m.EndedAt,
                    Kills = p.Kills,
                    Deaths = p.Deaths,
                    Flags = p.Flags
                }
            )
            .Take(take)
            .ToListAsync();

            // totals
            var totalKills = matches.Sum(x => x.Kills);
            var totalDeaths = matches.Sum(x => x.Deaths);
            var deathsSafe = totalDeaths <= 0 ? 1 : totalDeaths;
            var totalTimeMs = matches.Sum(x => x.LengthMs);
            var totalFlags = matches.Sum(x => x.Flags);

            var model = new PlayerMatchHistoryVm
            {
                UserName = user.UserName,
                TotalMatches = matches.Count,
                TotalKills = totalKills,
                TotalDeaths = totalDeaths,
                TotalFlagsCaptured = totalFlags,
                TotalTimeMs = totalTimeMs,
                KdRatio = Math.Round((double)totalKills / deathsSafe, 2),
                Matches = matches
            };

            return View(model);
        }
    }
}
