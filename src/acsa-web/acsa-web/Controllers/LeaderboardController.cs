using acsa_web.Data;
using acsa_web.Models.ViewModels;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace acsa_web.Controllers
{
    public class LeaderboardController : Controller
    {
        private readonly ApplicationDbContext _db;

        public LeaderboardController(ApplicationDbContext db)
        {
            _db = db;
        }

        public async Task<IActionResult> Index(int take = 10)
        {
            // aggregate per PersistentUid
            var aggregated = await (
                from p in _db.GameMatchPlayers
                where p.PersistentUid != null && p.PersistentUid != ""
                join m in _db.GameMatches on p.MatchId equals m.Id
                group new { p, m } by p.PersistentUid into g
                select new
                {
                    PersistentUid = g.Key,
                    Kills = g.Sum(x => x.p.Kills),
                    Deaths = g.Sum(x => x.p.Deaths),
                    Flags = g.Sum(x => (int?)(x.p.Flags) ?? 0),
                    TotalTimeMs = g.Sum(x => (long)x.m.LengthMs),
                    LastPlayed = g.Max(x => (DateTime?)x.m.EndedAt)
                }
            ).ToListAsync();

            // map PersistentUid -> Username
            var userMap = await _db.Users
                .Where(u => u.PersistentUid != null && u.PersistentUid != "")
                .Select(u => new { u.UserName, u.PersistentUid })
                .ToDictionaryAsync(x => x.PersistentUid!, x => x.UserName);

            var model = aggregated
                .Select(x =>
                {
                    var deathsSafe = x.Deaths <= 0 ? 1 : x.Deaths;

                    return new LeaderboardEntryVm
                    {
                        UserName = userMap.TryGetValue(x.PersistentUid!, out var name)
                            ? name
                            : "unknown",

                        Kills = x.Kills,
                        Deaths = x.Deaths,
                        TotalFlagsCaptured = x.Flags,
                        TotalTimeMs = x.TotalTimeMs,
                        LastPlayed = x.LastPlayed,
                        KdRatio = Math.Round((double)x.Kills / deathsSafe, 2)
                    };
                })
                .OrderByDescending(x => x.KdRatio)
                .ThenByDescending(x => x.Kills)
                .Take(take)
                .ToList();

            // assign rank numbers
            for (int i = 0; i < model.Count; i++)
                model[i].Rank = i + 1;

            return View(model);
        }
    }
}
