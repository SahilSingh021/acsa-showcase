using acsa_web.Models;
using Microsoft.AspNetCore.Identity;

namespace acsa_web.Data
{
    public static class IdentityDataInitializer
    {
        public static async Task SeedRolesAsync(IServiceProvider serviceProvider)
        {
            var roleManager = serviceProvider.GetRequiredService<RoleManager<IdentityRole>>();
            string[] roleNames = { "Admin", "User" };

            foreach (var role in roleNames)
            {
                if (!await roleManager.RoleExistsAsync(role))
                    await roleManager.CreateAsync(new IdentityRole(role));
            }
        }
        public static async Task SeedAdminUserAsync(IServiceProvider services)
        {
            var userManager = services.GetRequiredService<UserManager<ApplicationUser>>();

            const string AdminRole = "Admin";
            var config = services.GetRequiredService<IConfiguration>();
            var username = config["SeedAdmin:Username"];
            var email = config["SeedAdmin:Email"];
            var password = config["SeedAdmin:Password"];

            var user = await userManager.FindByEmailAsync(email);

            if (user == null)
            {
                user = new ApplicationUser
                {
                    UserName = username,
                    Email = email,
                    EmailConfirmed = true,
                };

                var createResult = await userManager.CreateAsync(user, password);
                if (!createResult.Succeeded)
                {
                    var errors = string.Join(", ", createResult.Errors.Select(e => $"{e.Code}:{e.Description}"));
                    throw new Exception($"Failed to create admin user: {errors}");
                }
            }
            else
            {
                // If the user already exists, confirm email
                if (!user.EmailConfirmed)
                {
                    user.EmailConfirmed = true;
                    var updateResult = await userManager.UpdateAsync(user);
                    if (!updateResult.Succeeded)
                    {
                        var errors = string.Join(", ", updateResult.Errors.Select(e => $"{e.Code}:{e.Description}"));
                        throw new Exception($"Failed to update admin user: {errors}");
                    }
                }
            }

            // Role assignment
            if (!await userManager.IsInRoleAsync(user, AdminRole))
            {
                var addRoleResult = await userManager.AddToRoleAsync(user, AdminRole);
                if (!addRoleResult.Succeeded)
                {
                    var errors = string.Join(", ", addRoleResult.Errors.Select(e => $"{e.Code}:{e.Description}"));
                    throw new Exception($"Failed to add admin role: {errors}");
                }
            }
        }
    }
}
