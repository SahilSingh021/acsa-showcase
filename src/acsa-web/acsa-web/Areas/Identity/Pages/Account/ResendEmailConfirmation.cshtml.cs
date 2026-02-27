// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
#nullable disable

using System;
using System.ComponentModel.DataAnnotations;
using System.Text;
using System.Text.Encodings.Web;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using acsa_web.Models;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Identity.UI.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.AspNetCore.WebUtilities;

namespace acsa_web.Areas.Identity.Pages.Account
{
    [AllowAnonymous]
    public class ResendEmailConfirmationModel : PageModel
    {
        private readonly UserManager<ApplicationUser> _userManager;
        private readonly IEmailSender _emailSender;

        public ResendEmailConfirmationModel(UserManager<ApplicationUser> userManager, IEmailSender emailSender)
        {
            _userManager = userManager;
            _emailSender = emailSender;
        }

        /// <summary>
        ///     This API supports the ASP.NET Core Identity default UI infrastructure and is not intended to be used
        ///     directly from your code. This API may change or be removed in future releases.
        /// </summary>
        [BindProperty]
        public InputModel Input { get; set; }

        /// <summary>
        ///     This API supports the ASP.NET Core Identity default UI infrastructure and is not intended to be used
        ///     directly from your code. This API may change or be removed in future releases.
        /// </summary>
        public class InputModel
        {
            /// <summary>
            ///     This API supports the ASP.NET Core Identity default UI infrastructure and is not intended to be used
            ///     directly from your code. This API may change or be removed in future releases.
            /// </summary>
            [Required]
            [EmailAddress]
            public string Email { get; set; }
        }

        public void OnGet()
        {
        }

        public async Task<IActionResult> OnPostAsync()
        {
            if (!ModelState.IsValid)
                return Page();

            // Always show the same message to avoid email enumeration
            TempData["SuccessMessage"] = "Verification email sent. Please check your email.";

            var user = await _userManager.FindByEmailAsync(Input.Email);
            if (user == null)
                return RedirectToPage();

            var userId = await _userManager.GetUserIdAsync(user);
            var code = await _userManager.GenerateEmailConfirmationTokenAsync(user);
            code = WebEncoders.Base64UrlEncode(Encoding.UTF8.GetBytes(code));

            var callbackUrl = Url.Page(
                "/Account/ConfirmEmail",
                pageHandler: null,
                values: new { userId, code },
                protocol: Request.Scheme);

            var safeLink = HtmlEncoder.Default.Encode(callbackUrl);
            var sentAt = DateTime.UtcNow.ToString("yyyy-MM-dd HH:mm:ss 'UTC'");

            var html = $@"
            <!doctype html>
            <html>
                <head>
                  <meta charset=""utf-8"">
                  <meta name=""viewport"" content=""width=device-width, initial-scale=1"">
                  <title>Confirm your email</title>
                </head>

                <body style=""margin:0;padding:0;background:#f5f7fb;font-family:Segoe UI,Roboto,Arial,sans-serif;color:#111827;"">
                  <table role=""presentation"" width=""100%"" cellpadding=""0"" cellspacing=""0"" style=""background:#f5f7fb;padding:24px 12px;"">
                    <tr>
                      <td align=""center"">

                        <table role=""presentation"" width=""680"" cellpadding=""0"" cellspacing=""0"" style=""max-width:680px;width:100%;"">

                          <!-- Header -->
                          <tr>
                            <td style=""padding:6px 0 14px 0;"">
                              <div style=""font-weight:900;font-size:14px;color:#2563eb;letter-spacing:0.4px;"">
                                AC Secure Arena
                              </div>
                            </td>
                          </tr>

                          <!-- Card -->
                          <tr>
                            <td style=""background:#ffffff;border:1px solid #e5e7eb;border-radius:16px;
                                       box-shadow:0 8px 24px rgba(17,24,39,0.06);overflow:hidden;"">

                              <div style=""padding:22px 22px 0 22px;"">
                                <div style=""font-size:22px;font-weight:900;color:#111827;"">
                                  Confirm your email
                                </div>

                                <div style=""margin-top:8px;font-size:14px;color:#4b5563;line-height:1.6;"">
                                  A new confirmation link was requested for your account.
                                </div>
                              </div>

                              <div style=""padding:22px;"">

                                <div style=""background:#f9fafb;border:1px solid #e5e7eb;border-radius:12px;
                                           padding:16px;font-size:14px;color:#374151;line-height:1.7;"">
                                  Click the button below to confirm your email address and activate your account.
                                </div>

                                <div style=""height:20px;""></div>

                                <!-- Button -->
                                <div style=""text-align:center;"">
                                  <a href=""{safeLink}""
                                     style=""display:inline-block;
                                            background:#2563eb;
                                            color:#ffffff;
                                            text-decoration:none;
                                            padding:14px 24px;
                                            border-radius:12px;
                                            font-weight:800;
                                            font-size:14px;
                                            box-shadow:0 4px 14px rgba(37,99,235,0.25);"">
                                    Confirm Email
                                  </a>
                                </div>

                                <div style=""height:20px;""></div>

                                <!-- Fallback link -->
                                <div style=""font-size:12px;color:#6b7280;line-height:1.6;"">
                                  If the button doesn’t work, copy and paste this link into your browser:
                                  <div style=""margin-top:8px;
                                              padding:10px;
                                              background:#f3f4f6;
                                              border-radius:8px;
                                              font-family:Consolas,Menlo,monospace;
                                              word-break:break-all;
                                              color:#374151;"">
                                    {safeLink}
                                  </div>
                                </div>

                              </div>

                              <!-- Footer -->
                              <div style=""padding:0 22px 20px 22px;
                                          font-size:11px;
                                          color:#9ca3af;
                                          line-height:1.5;"">
                                Sent: {sentAt}<br/>
                                © {DateTime.UtcNow.Year} AC Secure Arena
                              </div>

                            </td>
                          </tr>

                        </table>

                      </td>
                    </tr>
                  </table>
                </body>
            </html>";

            await _emailSender.SendEmailAsync(
                Input.Email,
                "Confirm your email - AC Secure Arena",
                html);

            return RedirectToPage();
        }
    }
}
