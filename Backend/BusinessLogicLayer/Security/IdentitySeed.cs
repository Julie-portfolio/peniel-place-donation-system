using Microsoft.AspNetCore.Identity;
using PenielPlaceMinistriesCSharp.Models;

namespace PenielPlaceMinistriesCSharp.Security;

public static class IdentitySeed
{
    public const string DevEmail = "leader@peniel.local";
    public const string DevPassword = "Dev-Only-Change-Me-123!";

    public static async Task SeedAsync(
        IServiceProvider services,
        IConfiguration configuration,
        IHostEnvironment environment)
    {
        var roleManager = services.GetRequiredService<RoleManager<IdentityRole>>();
        var userManager = services.GetRequiredService<UserManager<ApplicationUser>>();
        var logger = services.GetRequiredService<ILoggerFactory>().CreateLogger("IdentitySeed");

        foreach (var roleName in Roles.All)
        {
            if (await roleManager.RoleExistsAsync(roleName)) continue;

            var roleResult = await roleManager.CreateAsync(new IdentityRole(roleName));
            if (!roleResult.Succeeded)
                throw new InvalidOperationException(
                    $"Could not create role {roleName}: " +
                    string.Join("; ", roleResult.Errors.Select(e => e.Description)));
        }


        var email = configuration["BootstrapAdmin:Email"]?.Trim();
        var password = configuration["BootstrapAdmin:Password"];

        if (string.IsNullOrWhiteSpace(email) || string.IsNullOrWhiteSpace(password))
        {
            if (!environment.IsDevelopment())
            {
                logger.LogWarning(
                    "No BootstrapAdmin credentials configured. If no Leadership account exists yet, " +
                    "set BootstrapAdmin:Email and BootstrapAdmin:Password and restart.");
                return;
            }

            // Development convenience only: seed a throw-away leader if none exists.
            if ((await userManager.GetUsersInRoleAsync(Roles.Leadership)).Count > 0) return;

            email = DevEmail;
            password = DevPassword;
            logger.LogWarning(
                "Development mode: seeding Leadership account {Email} with the documented dev password. " +
                "Configure BootstrapAdmin for anything other than local development.", DevEmail);
        }

        var user = await userManager.FindByEmailAsync(email);
        if (user is null)
        {
            user = new ApplicationUser
            {
                UserName = email,
                Email = email,
                EmailConfirmed = true,
                DisplayName = "Peniel Place Leadership",
                LockoutEnabled = true
            };

            var createResult = await userManager.CreateAsync(user, password);
            if (!createResult.Succeeded)
                throw new InvalidOperationException(
                    "Could not create bootstrap leadership user: " +
                    string.Join("; ", createResult.Errors.Select(e => e.Description)));
        }

        if (!await userManager.IsInRoleAsync(user, Roles.Leadership))
        {
            var roleResult = await userManager.AddToRoleAsync(user, Roles.Leadership);
            if (!roleResult.Succeeded)
                throw new InvalidOperationException(
                    "Could not assign Leadership role: " +
                    string.Join("; ", roleResult.Errors.Select(e => e.Description)));
        }
    }
}
