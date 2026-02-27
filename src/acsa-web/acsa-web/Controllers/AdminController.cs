using acsa_web.Data;
using acsa_web.Models;
using acsa_web.Models.ViewModels;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace acsa_web.Controllers
{
    [Authorize(Roles = "Admin")]
    public class AdminController : Controller
    {
        private readonly ApplicationDbContext _db;
        private readonly UserManager<ApplicationUser> _userManager;

        public AdminController(ApplicationDbContext db, UserManager<ApplicationUser> userManager)
        {
            _db = db;
            _userManager = userManager;
        }

        // Admin Logs
        [HttpGet]
        public async Task<IActionResult> Logs(string? q, AdminLogLevel? level, int take = 200)
        {
            take = Math.Clamp(take, 50, 1000);

            var query = _db.AdminLogs
                .AsNoTracking()
                .Include(x => x.AdminUser)
                .AsQueryable();

            if (level.HasValue)
                query = query.Where(x => x.Level == level.Value);

            if (!string.IsNullOrWhiteSpace(q))
            {
                var term = q.Trim();

                if (Enum.TryParse<AdminLogLevel>(term, true, out var parsedLevel))
                {
                    query = query.Where(x =>
                        x.Message.Contains(term) ||
                        x.Level == parsedLevel ||
                        (x.AdminUser != null && x.AdminUser.UserName.Contains(term))
                    );
                }
                else
                {
                    query = query.Where(x =>
                        x.Message.Contains(term) ||
                        (x.AdminUser != null && x.AdminUser.UserName.Contains(term))
                    );
                }
            }

            var logs = await query
                .OrderByDescending(x => x.CreatedAt)
                .Take(take)
                .ToListAsync();

            var vm = new AdminLogsVm
            {
                Q = q,
                Level = level,
                Take = take,
                Logs = logs
            };

            return View(vm);
        }

        // User Search
        [HttpGet]
        public IActionResult Users()
        {
            return View(new AdminUserSearchVm());
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Users(AdminUserSearchVm vm)
        {
            vm.Username = vm.Username?.Trim();

            if (string.IsNullOrWhiteSpace(vm.Username))
            {
                TempData["ErrorMessage"] = "Enter a username to search.";
                return View(new AdminUserSearchVm());
            }

            vm.Results = await _db.Users
                .AsNoTracking()
                .Where(u => u.UserName != null && u.UserName.Contains(vm.Username))
                .OrderBy(u => u.UserName)
                .Take(25)
                .ToListAsync();

            return View(vm);
        }

        // Manage User
        [HttpGet]
        public async Task<IActionResult> UserDetails(string id)
        {
            if (string.IsNullOrWhiteSpace(id))
                return NotFound();

            var user = await _db.Users.FirstOrDefaultAsync(u => u.Id == id);
            if (user == null)
                return NotFound();

            var logs = await _db.UserLogs
                .AsNoTracking()
                .Where(l => l.UserId == id)
                .OrderByDescending(l => l.CreatedAt)
                .Take(100)
                .ToListAsync();

            var matches = await _db.GameMatchPlayers
                .AsNoTracking()
                .Include(p => p.Match)
                .Where(p => p.UserId == id)
                .OrderByDescending(p => p.Match.EndedAt)
                .Take(50)
                .ToListAsync();

            var vm = new AdminUserManageVm
            {
                UserId = user.Id,
                Username = user.UserName,

                FullName = user.FullName,
                Email = user.Email,
                PhoneNumber = user.PhoneNumber,

                IsBanned = user.IsBanned,
                BanDate = user.BanDate,

                RegisteredOn = user.RegisteredOn,
                IsAccountLinkedWithGame = user.IsAccountLinkedWithGame,
                PersistentUid = user.PersistentUid,

                RecentUserLogs = logs,
                RecentMatches = matches
            };

            return View("UserDetails", vm);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> UpdateUser(AdminUserManageVm vm)
        {
            if (vm == null || string.IsNullOrWhiteSpace(vm.UserId))
                return NotFound();

            var user = await _db.Users.FirstOrDefaultAsync(u => u.Id == vm.UserId);
            if (user == null)
                return NotFound();

            // admin identity
            var adminId = _userManager.GetUserId(User);
            var admin = await _userManager.GetUserAsync(User);
            var adminName = admin?.UserName ?? "Admin";

            // before values
            var oldFullName = user.FullName ?? "";
            var oldEmail = user.Email ?? "";
            var oldPhone = user.PhoneNumber ?? "";
            var oldBanned = user.IsBanned;

            // normalize incoming
            var newFullName = (vm.FullName ?? "").Trim();
            var newEmail = (vm.Email ?? "").Trim();
            var newPhone = (vm.PhoneNumber ?? "").Trim();
            var newBanned = vm.IsBanned;

            // apply changes
            user.FullName = newFullName;
            user.Email = newEmail;
            user.PhoneNumber = newPhone;

            if (user.IsBanned != newBanned)
            {
                user.IsBanned = newBanned;
                user.BanDate = newBanned ? DateTime.UtcNow : null;
            }

            // build changes list
            var targetName = user.UserName ?? user.Id;
            var changes = new List<(string msg, AdminLogLevel level)>();

            if (!string.Equals(oldFullName, newFullName, StringComparison.Ordinal))
                changes.Add(($"{adminName} updated {targetName}'s full name.", AdminLogLevel.Update));

            if (!string.Equals(oldEmail, newEmail, StringComparison.OrdinalIgnoreCase))
                changes.Add(($"{adminName} updated {targetName}'s email.", AdminLogLevel.Update));

            if (!string.Equals(oldPhone, newPhone, StringComparison.Ordinal))
                changes.Add(($"{adminName} updated {targetName}'s phone number.", AdminLogLevel.Update));

            if (oldBanned != user.IsBanned)
            {
                if (user.IsBanned)
                    changes.Add(($"{adminName} banned {targetName}.", AdminLogLevel.Warning));
                else
                    changes.Add(($"{adminName} unbanned {targetName}.", AdminLogLevel.Update));
            }

            if (changes.Count == 0)
            {
                TempData["SuccessMessage"] = "No changes detected.";
                return RedirectToAction(nameof(UserDetails), new { id = user.Id });
            }

            // write admin logs (one row per change)
            foreach (var c in changes)
            {
                _db.AdminLogs.Add(new AdminLog
                {
                    AdminUserId = adminId!,
                    Message = c.msg,
                    Level = c.level,
                    CreatedAt = DateTime.UtcNow
                });
            }

            // save everything
            await _db.SaveChangesAsync();

            TempData["SuccessMessage"] = "User updated.";
            return RedirectToAction(nameof(UserDetails), new { id = user.Id });
        }

        [HttpGet]
        public async Task<IActionResult> MaliciousLogs(string? q)
        {
            const int take = 200;

            var query = _db.UserLogs
                .AsNoTracking()
                .Include(x => x.User)
                .Where(x => x.Level == UserLogLevel.Malicious);

            if (!string.IsNullOrWhiteSpace(q))
            {
                var term = q.Trim();
                query = query.Where(x =>
                    x.Message.Contains(term) ||
                    (x.User != null && x.User.UserName != null && x.User.UserName.Contains(term))
                );
            }

            var logs = await query
                .OrderByDescending(x => x.CreatedAt)
                .Take(take)
                .ToListAsync();

            ViewBag.Q = q;
            return View(logs);
        }
    }
}
