using acsa_web.Data;
using acsa_web.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.IdentityModel.Tokens;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using Microsoft.EntityFrameworkCore;

namespace acsa_web.Controllers
{
    [ApiController]
    [Route("api/auth")]
    public class AuthController : ControllerBase
    {
        private readonly UserManager<ApplicationUser> _userManager;
        private readonly IConfiguration _cfg;
        private readonly ApplicationDbContext _db;
        private readonly ILogger<AuthController> _logger;

        public AuthController(UserManager<ApplicationUser> userManager, IConfiguration cfg, ApplicationDbContext db, ILogger<AuthController> logger)
        {
            _userManager = userManager;
            _cfg = cfg;
            _db = db;
            _logger = logger;
        }

        static string XorEncryptToHex(string text, string key)
        {
            if (string.IsNullOrEmpty(text) || string.IsNullOrEmpty(key))
                return "";

            var t = Encoding.UTF8.GetBytes(text);
            var k = Encoding.UTF8.GetBytes(key);

            var o = new byte[t.Length];
            for (int i = 0; i < t.Length; i++)
                o[i] = (byte)(t[i] ^ k[i % k.Length]);

            return Convert.ToHexString(o);
        }

        public record LoginReq(string Email, string Password, string HardwareId, string? Ip);

        [HttpPost("login")]
        public async Task<IActionResult> Login([FromBody] LoginReq req)
        {
            if (string.IsNullOrWhiteSpace(req.Email) ||
                string.IsNullOrWhiteSpace(req.Password) ||
                string.IsNullOrWhiteSpace(req.HardwareId) ||
                string.IsNullOrWhiteSpace(req.Ip))
            {
                return BadRequest(new { error = "Missing fields." });
            }

            // auth
            var user = await _userManager.FindByEmailAsync(req.Email);
            if (user == null) return Unauthorized(new { error = "Invalid login" });

            var ok = await _userManager.CheckPasswordAsync(user, req.Password);
            if (!ok) return Unauthorized(new { error = "Invalid login" });

            var now = DateTime.UtcNow;

            // persistentUid
            var nameForUid = user.UserName;
            var secretStr = user.SharedSecret.ToString();
            var persistentUid = XorEncryptToHex(nameForUid, secretStr);

            if (user.PersistentUid != persistentUid)
            {
                user.PersistentUid = persistentUid;
                await _userManager.UpdateAsync(user);
            }

            // linking logic
            var finalIp = req.Ip.Trim();

            var oldHwid = await _db.UserHwids
                .Where(x => x.UserId == user.Id)
                .OrderByDescending(x => x.UsedAtDateTime)
                .Select(x => x.Value)
                .FirstOrDefaultAsync();

            var oldIp = await _db.UserIpAddresses
                .Where(x => x.UserId == user.Id)
                .OrderByDescending(x => x.UsedAtDateTime)
                .Select(x => x.Value)
                .FirstOrDefaultAsync();

            bool wasLinked = user.IsAccountLinkedWithGame;

            if (!user.IsAccountLinkedWithGame)
                user.IsAccountLinkedWithGame = true;

            // HWID upsert
            var hwidRow = await _db.UserHwids
                .FirstOrDefaultAsync(x => x.UserId == user.Id && x.Value == req.HardwareId);

            if (hwidRow != null)
                hwidRow.UsedAtDateTime = now;
            else
                _db.UserHwids.Add(new UserHwid
                {
                    UserId = user.Id,
                    Value = req.HardwareId,
                    UsedAtDateTime = now
                });

            // ip upsert
            var ipRow = await _db.UserIpAddresses
                .FirstOrDefaultAsync(x => x.UserId == user.Id && x.Value == finalIp);

            if (ipRow != null)
                ipRow.UsedAtDateTime = now;
            else
                _db.UserIpAddresses.Add(new UserIpAddress
                {
                    UserId = user.Id,
                    Value = finalIp,
                    UsedAtDateTime = now
                });

            // log
            string msg;
            if (!wasLinked)
            {
                msg =
                    "Account linked with game.\n" +
                    $"HWID: {req.HardwareId}\n" +
                    $"IP: {finalIp}";
            }
            else
            {
                var hwidLine = (oldHwid == req.HardwareId)
                    ? $"HWID: {req.HardwareId}"
                    : $"HWID: {(oldHwid ?? "none")} -> {req.HardwareId}";

                var ipLine = (oldIp == finalIp)
                    ? $"IP: {finalIp}"
                    : $"IP: {(oldIp ?? "none")} -> {finalIp}";

                msg = "Account link refreshed.\n" + hwidLine + "\n" + ipLine;
            }

            _db.UserLogs.Add(new UserLog
            {
                UserId = user.Id,
                Message = msg,
                Level = UserLogLevel.Update,
                CreatedAt = now
            });

            await _db.SaveChangesAsync();

            // issue JWT after linking
            var token = CreateJwt(user);

            _logger.LogInformation("AntiCheat link user {UserId}. HWID={Hwid}, IP={Ip}", user.Id, req.HardwareId, finalIp);

            return Ok(new { token, persistentUid });
        }

        private string CreateJwt(ApplicationUser user)
        {
            var key = _cfg["Jwt:Key"]!;
            var issuer = _cfg["Jwt:Issuer"]!;
            var audience = _cfg["Jwt:Audience"]!;

            var claims = new List<Claim>
            {
                new Claim(ClaimTypes.NameIdentifier, user.Id),
            };

            var creds = new SigningCredentials(
                new SymmetricSecurityKey(Encoding.UTF8.GetBytes(key)),
                SecurityAlgorithms.HmacSha256);

            var jwt = new JwtSecurityToken(
                issuer: issuer,
                audience: audience,
                claims: claims,
                expires: DateTime.UtcNow.AddHours(24),
                signingCredentials: creds);

            return new JwtSecurityTokenHandler().WriteToken(jwt);
        }
    }
}
