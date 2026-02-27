using acsa_web.Data;
using acsa_web.Models;
using acsa_web.Services;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Identity.UI.Services;
using Microsoft.AspNetCore.Mvc;
using System.Diagnostics;

namespace acsa_web.Controllers
{
    public class HomeController : Controller
    {
        private readonly ILogger<HomeController> _logger;
        private readonly UserManager<ApplicationUser> _userManager;
        private readonly IEmailSender _emailSender;
        private readonly ApplicationDbContext _db;

        public HomeController(
            ILogger<HomeController> logger,
            UserManager<ApplicationUser> userManager,
            IEmailSender emailSender,
            ApplicationDbContext db)
        {
            _logger = logger;
            _userManager = userManager;
            _emailSender = emailSender;
            _db = db;
        }

        public IActionResult Index()
        {
            return View();
        }

        [ResponseCache(Duration = 0, Location = ResponseCacheLocation.None, NoStore = true)]
        public IActionResult Error(string? message = null)
        {
            var vm = new ErrorViewModel
            {
                RequestId = Activity.Current?.Id ?? HttpContext.TraceIdentifier,
                Message = message
            };
            return View(vm);
        }

        [HttpGet]
        public async Task<IActionResult> LinkAccountWithGame()
        {
            var user = await _userManager.GetUserAsync(User);

            if (user == null)
                return RedirectToPage("/Account/Login", new { area = "Identity" });

            ViewData["SharedSecret"] = user.UserName + ":" + user.SharedSecret.ToString().ToUpperInvariant();
            return View();
        }

        [HttpGet]
        public async Task<IActionResult> Support()
        {
            var user = await _userManager.GetUserAsync(User);
            if (user == null)
                return RedirectToPage("/Account/Login", new { area = "Identity" });

            ViewData["SupportName"] = string.IsNullOrWhiteSpace(user.FullName) ? user.UserName : user.FullName;
            ViewData["SupportEmail"] = user.Email ?? "";

            return View();
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> SendSupportEmail(string Subject, string Message)
        {
            var user = await _userManager.GetUserAsync(User);
            if (user == null)
                return RedirectToPage("/Account/Login", new { area = "Identity" });

            var name = string.IsNullOrWhiteSpace(user.FullName) ? user.UserName : user.FullName;
            var email = user.Email ?? "(no email)";

            var safeSubject = System.Net.WebUtility.HtmlEncode(Subject ?? "");
            var safeMessage = System.Net.WebUtility.HtmlEncode(Message ?? "").Replace("\n", "<br/>");
            var safeName = System.Net.WebUtility.HtmlEncode(name ?? "");
            var safeUsername = System.Net.WebUtility.HtmlEncode(user.UserName ?? "");
            var safeEmail = System.Net.WebUtility.HtmlEncode(email ?? "");
            var safeUserId = System.Net.WebUtility.HtmlEncode(user.Id ?? "");
            var sentAt = DateTime.UtcNow.ToString("yyyy-MM-dd HH:mm:ss 'UTC'");

            var body = $@"
            <!doctype html>
            <html>
                <head>
                  <meta charset=""utf-8"">
                  <meta name=""viewport"" content=""width=device-width, initial-scale=1"">
                  <title>Support Request</title>
                </head>

                <body style=""margin:0;padding:0;background:#f6f7fb;font-family:Segoe UI,Roboto,Arial,sans-serif;color:#111827;"">
                  <table role=""presentation"" width=""100%"" cellpadding=""0"" cellspacing=""0"" style=""background:#f6f7fb;padding:24px 12px;"">
                    <tr>
                      <td align=""center"">
                        <table role=""presentation"" width=""680"" cellpadding=""0"" cellspacing=""0"" style=""max-width:680px;width:100%;"">

                          <!-- header -->
                          <tr>
                            <td style=""padding:6px 0 14px 0;"">
                              <div style=""font-weight:800;font-size:14px;letter-spacing:0.3px;color:#1d4ed8;"">
                                AC Secure Arena • Support
                              </div>
                              <div style=""margin-top:4px;color:#6b7280;font-size:12px;"">
                                New support request submitted
                              </div>
                            </td>
                          </tr>

                          <!-- card -->
                          <tr>
                            <td style=""background:#ffffff;border:1px solid #e5e7eb;border-radius:14px;overflow:hidden;box-shadow:0 6px 22px rgba(17,24,39,0.06);"">

                              <div style=""padding:18px 18px 0 18px;"">
                                <div style=""color:#111827;font-size:18px;font-weight:900;"">Support Request</div>
                                <div style=""color:#6b7280;font-size:12px;margin-top:6px;"">
                                  Submitted: <span style=""font-weight:700;color:#374151;"">{sentAt}</span>
                                </div>
                              </div>

                              <div style=""padding:18px;"">

                                <!-- subject -->
                                <div style=""background:#f9fafb;border:1px solid #e5e7eb;border-radius:12px;padding:14px;"">
                                  <div style=""color:#6b7280;font-size:12px;text-transform:uppercase;letter-spacing:.08em;"">Subject</div>
                                  <div style=""color:#111827;font-size:15px;font-weight:800;margin-top:6px;"">{safeSubject}</div>
                                </div>

                                <div style=""height:12px;""></div>

                                <!-- user details -->
                                <div style=""background:#f9fafb;border:1px solid #e5e7eb;border-radius:12px;padding:14px;"">
                                  <div style=""color:#6b7280;font-size:12px;text-transform:uppercase;letter-spacing:.08em;"">User</div>

                                  <table role=""presentation"" width=""100%"" cellpadding=""0"" cellspacing=""0"" style=""margin-top:10px;"">
                                    <tr>
                                      <td style=""color:#6b7280;font-size:12px;width:120px;padding:6px 0;"">UserId</td>
                                      <td style=""color:#111827;font-size:12px;padding:6px 0;font-family:Consolas,Menlo,monospace;"">{safeUserId}</td>
                                    </tr>
                                    <tr>
                                      <td style=""color:#6b7280;font-size:12px;padding:6px 0;"">Username</td>
                                      <td style=""color:#111827;font-size:12px;padding:6px 0;font-weight:700;"">{safeUsername}</td>
                                    </tr>
                                    <tr>
                                      <td style=""color:#6b7280;font-size:12px;padding:6px 0;"">Name</td>
                                      <td style=""color:#111827;font-size:12px;padding:6px 0;"">{safeName}</td>
                                    </tr>
                                    <tr>
                                      <td style=""color:#6b7280;font-size:12px;padding:6px 0;"">Email</td>
                                      <td style=""color:#111827;font-size:12px;padding:6px 0;"">{safeEmail}</td>
                                    </tr>
                                  </table>
                                </div>

                                <div style=""height:12px;""></div>

                                <!-- message -->
                                <div style=""background:#ffffff;border:1px solid #e5e7eb;border-radius:12px;padding:14px;"">
                                  <div style=""color:#6b7280;font-size:12px;text-transform:uppercase;letter-spacing:.08em;"">Message</div>
                                  <div style=""color:#111827;font-size:13px;line-height:1.65;margin-top:10px;white-space:normal;"">
                                    {safeMessage}
                                  </div>
                                </div>

                              </div>

                              <div style=""padding:0 18px 18px 18px;"">
                                <div style=""color:#6b7280;font-size:11px;line-height:1.5;"">
                                  This email was generated automatically from the AC Secure Arena support form.
                                </div>
                              </div>

                            </td>
                          </tr>

                          <!-- footer -->
                          <tr>
                            <td style=""padding:14px 0 0 0;color:#9ca3af;font-size:11px;text-align:center;"">
                              © {DateTime.UtcNow.Year} AC Secure Arena
                            </td>
                          </tr>

                        </table>
                      </td>
                    </tr>
                  </table>
                </body>
            </html>";

            try
            {
                await _emailSender.SendEmailAsync(
                    "official.acsa@gmail.com",
                    "[Support] " + Subject,
                    body
                );

                _db.UserLogs.Add(new UserLog
                {
                    UserId = user.Id,
                    Level = UserLogLevel.Info,
                    Message = $"Support email sent. Subject: \"{Subject}\"",
                    CreatedAt = DateTime.UtcNow
                });

                await _db.SaveChangesAsync();

                TempData["SuccessMessage"] = "Message sent successfully.";
            }
            catch (Exception ex)
            {
                TempData["ErrorMessage"] = "Failed to send support email. Please try again.";
            }

            return RedirectToAction(nameof(Support));
        }
    }
}
