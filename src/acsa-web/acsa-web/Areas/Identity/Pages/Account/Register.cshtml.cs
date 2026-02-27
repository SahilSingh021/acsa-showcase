// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
#nullable disable

using acsa_web.Data;
using acsa_web.Models;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Identity.UI.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.AspNetCore.WebUtilities;
using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Linq;
using System.Text;
using System.Text.Encodings.Web;
using System.Threading;
using System.Threading.Tasks;

namespace acsa_web.Areas.Identity.Pages.Account
{
    public class RegisterModel : PageModel
    {
        private readonly SignInManager<ApplicationUser> _signInManager;
        private readonly UserManager<ApplicationUser> _userManager;
        private readonly IUserStore<ApplicationUser> _userStore;
        private readonly IUserEmailStore<ApplicationUser> _emailStore;
        private readonly ILogger<RegisterModel> _logger;
        private readonly IEmailSender _emailSender;

        public RegisterModel(
            UserManager<ApplicationUser> userManager,
            IUserStore<ApplicationUser> userStore,
            SignInManager<ApplicationUser> signInManager,
            ILogger<RegisterModel> logger,
            IEmailSender emailSender)
        {
            _userManager = userManager;
            _userStore = userStore;
            _emailStore = GetEmailStore();
            _signInManager = signInManager;
            _logger = logger;
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
        public string ReturnUrl { get; set; }

        /// <summary>
        ///     This API supports the ASP.NET Core Identity default UI infrastructure and is not intended to be used
        ///     directly from your code. This API may change or be removed in future releases.
        /// </summary>
        public IList<AuthenticationScheme> ExternalLogins { get; set; }

        /// <summary>
        ///     This API supports the ASP.NET Core Identity default UI infrastructure and is not intended to be used
        ///     directly from your code. This API may change or be removed in future releases.
        /// </summary>
        public class InputModel
        {
            [Required]
            [Display(Name = "Username")]
            public string UserName { get; set; }
            /// <summary>
            ///     This API supports the ASP.NET Core Identity default UI infrastructure and is not intended to be used
            ///     directly from your code. This API may change or be removed in future releases.
            /// </summary>
            [Required]
            [EmailAddress]
            [Display(Name = "Email")]
            public string Email { get; set; }

            [Required]
            [Display(Name = "Full Name")]
            public string FullName { get; set; }

            [Required]
            [Phone]
            [Display(Name = "Phone Number")]
            public string PhoneNumber { get; set; }

            /// <summary>
            ///     This API supports the ASP.NET Core Identity default UI infrastructure and is not intended to be used
            ///     directly from your code. This API may change or be removed in future releases.
            /// </summary>
            [Required]
            [StringLength(100, ErrorMessage = "The {0} must be at least {2} and at max {1} characters long.", MinimumLength = 6)]
            [DataType(DataType.Password)]
            [Display(Name = "Password")]
            public string Password { get; set; }

            /// <summary>
            ///     This API supports the ASP.NET Core Identity default UI infrastructure and is not intended to be used
            ///     directly from your code. This API may change or be removed in future releases.
            /// </summary>
            [DataType(DataType.Password)]
            [Display(Name = "Confirm password")]
            [Compare("Password", ErrorMessage = "The password and confirmation password do not match.")]
            public string ConfirmPassword { get; set; }
        }


        public async Task OnGetAsync(string returnUrl = null)
        {
            ReturnUrl = returnUrl;
            ExternalLogins = (await _signInManager.GetExternalAuthenticationSchemesAsync()).ToList();
        }

        public async Task<IActionResult> OnPostAsync(string returnUrl = null)
        {
            returnUrl ??= Url.Content("~/");
            ExternalLogins = (await _signInManager.GetExternalAuthenticationSchemesAsync()).ToList();

            if (!ModelState.IsValid)
                return Page();

            // Clean inputs a bit
            var userName = Input.UserName?.Trim();
            var email = Input.Email?.Trim();
            var fullName = Input.FullName?.Trim();
            var phone = Input.PhoneNumber?.Trim();

            var user = new ApplicationUser
            {
                UserName = userName,
                Email = email,
                FullName = fullName,
                PhoneNumber = phone,
                RegisteredOn = DateTime.UtcNow,
                IsBanned = false
            };

            var result = await _userManager.CreateAsync(user, Input.Password);

            if (!result.Succeeded)
            {
                foreach (var error in result.Errors)
                    ModelState.AddModelError(string.Empty, error.Description);

                return Page();
            }

            _logger.LogInformation("User created a new account with password.");

            await _userManager.AddToRoleAsync(user, Roles.User);

            var userId = await _userManager.GetUserIdAsync(user);

            // Generate confirmation link
            var code = await _userManager.GenerateEmailConfirmationTokenAsync(user);
            code = WebEncoders.Base64UrlEncode(Encoding.UTF8.GetBytes(code));

            var callbackUrl = Url.Page(
                "/Account/ConfirmEmail",
                pageHandler: null,
                values: new { area = "Identity", userId, code, returnUrl },
                protocol: Request.Scheme);

            // Nice HTML email (email-client friendly: inline CSS + table layout)
            var safeUser = System.Net.WebUtility.HtmlEncode(user.UserName ?? "");
            var safeLink = HtmlEncoder.Default.Encode(callbackUrl);

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
                                  Welcome{(string.IsNullOrWhiteSpace(safeUser) ? "" : $", <b style='color:#111827'>{safeUser}</b>")} -
                                  you're almost ready to start playing.
                                </div>
                              </div>

                              <div style=""padding:22px;"">

                                <!-- Info box -->
                                <div style=""background:#f9fafb;border:1px solid #e5e7eb;border-radius:12px;
                                           padding:16px;font-size:14px;color:#374151;line-height:1.7;"">
                                  Click the button below to confirm your email address and activate your account.
                                  If you didn’t create this account, you can safely ignore this message.
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

            await _emailSender.SendEmailAsync(email, "Confirm your email - AC Secure Arena", html);

            if (_userManager.Options.SignIn.RequireConfirmedAccount)
            {
                return RedirectToPage("RegisterConfirmation", new { email, returnUrl });
            }

            await _signInManager.SignInAsync(user, isPersistent: false);
            return LocalRedirect(returnUrl);
        }

        private ApplicationUser CreateUser()
        {
            try
            {
                return Activator.CreateInstance<ApplicationUser>();
            }
            catch
            {
                throw new InvalidOperationException($"Can't create an instance of '{nameof(ApplicationUser)}'. " +
                    $"Ensure that '{nameof(ApplicationUser)}' is not an abstract class and has a parameterless constructor, or alternatively " +
                    $"override the register page in /Areas/Identity/Pages/Account/Register.cshtml");
            }
        }

        private IUserEmailStore<ApplicationUser> GetEmailStore()
        {
            if (!_userManager.SupportsUserEmail)
            {
                throw new NotSupportedException("The default UI requires a user store with email support.");
            }
            return (IUserEmailStore<ApplicationUser>)_userStore;
        }
    }
}
