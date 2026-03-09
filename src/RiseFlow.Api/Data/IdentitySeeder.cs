using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.DependencyInjection;

namespace RiseFlow.Api.Data;

/// <summary>
/// Seeds core Identity data: roles and the initial SuperAdmin user.
/// Safe to run on every startup (idempotent).
/// </summary>
public static class IdentitySeeder
{
    public static async Task SeedAdminUserAsync(IServiceProvider services)
    {
        var roleManager = services.GetRequiredService<RoleManager<IdentityRole<Guid>>>();
        var userManager = services.GetRequiredService<UserManager<ApplicationUser>>();

        // 1. Ensure roles exist (reuse existing RoleSeeder logic)
        await RoleSeeder.SeedRolesAsync(roleManager).ConfigureAwait(false);

        // 2. Ensure SuperAdmin user exists
        var adminEmail = "ugochukwuhenry16@gmail.com";
        var adminUser = await userManager.FindByEmailAsync(adminEmail).ConfigureAwait(false);

        if (adminUser != null)
            return;

        var user = new ApplicationUser
        {
            UserName = adminEmail,
            Email = adminEmail,
            FullName = "RiseFlow Master Admin",
            EmailConfirmed = true,
            SchoolId = null,
            IsActive = true,
            CreatedAtUtc = DateTime.UtcNow
        };

        // NOTE: For production, consider moving the initial password to configuration / secrets.
        var result = await userManager.CreateAsync(user, "RiseFlow@2026!Secure").ConfigureAwait(false);
        if (result.Succeeded)
        {
            await userManager.AddToRoleAsync(user, Constants.Roles.SuperAdmin).ConfigureAwait(false);
        }
    }
}

