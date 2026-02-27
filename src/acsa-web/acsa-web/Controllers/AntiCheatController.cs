using acsa_web.Data;
using acsa_web.Models;
using acsa_web.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Security.Claims;

namespace acsa_web.Controllers
{
    [ApiController]
    [Route("api/ac")]
    [Authorize(AuthenticationSchemes = "Bearer")]
    public class AntiCheatController : ControllerBase
    {
        private readonly AntiCheatCommandQueue _queue;
        private readonly ApplicationDbContext _db;

        public AntiCheatController(AntiCheatCommandQueue queue, ApplicationDbContext db)
        {
            _queue = queue;
            _db = db;
        }

        // post /api/ac/clear
        [HttpPost("clear")]
        public IActionResult ClearQueue()
        {
            var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
            if (string.IsNullOrWhiteSpace(userId))
                return Unauthorized();

            var drained = _queue.Clear(userId);
            return Ok(new { drained });
        }

        // get /api/ac/next
        [HttpGet("next")]
        public IActionResult Next()
        {
            var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
            if (string.IsNullOrWhiteSpace(userId))
                return Unauthorized();

            if (_queue.TryDequeue(userId, out var payload) && payload != null)
                return Ok(payload);

            return NoContent();
        }

        public record UserLogDto(
            string message,
            UserLogLevel level
        );

        [HttpPost("saveuserlog")]
        public async Task<ActionResult<bool>> SaveUserLog([FromBody] UserLogDto dto)
        {
            if (dto is null || string.IsNullOrWhiteSpace(dto.message))
                return BadRequest(false);

            // identity comes from the JWT thats already validated
            var userId =
                User.FindFirstValue(ClaimTypes.NameIdentifier)
                ?? User.FindFirstValue("sub");

            if (string.IsNullOrWhiteSpace(userId))
                return Unauthorized(false);

            var log = new UserLog
            {
                UserId = userId,
                Message = dto.message,
                Level = dto.level,
                CreatedAt = DateTime.UtcNow
            };

            _db.UserLogs.Add(log);
            await _db.SaveChangesAsync();
            return Ok(true);
        }
    }
}
