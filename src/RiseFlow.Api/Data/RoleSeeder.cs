using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using RiseFlow.Api.Constants;

namespace RiseFlow.Api.Data;

public static class RoleSeeder
{
    public static async Task SeedRolesAsync(RoleManager<IdentityRole<Guid>> roleManager)
    {
        foreach (var roleName in Roles.All)
        {
            if (await roleManager.RoleExistsAsync(roleName).ConfigureAwait(false))
                continue;
            await roleManager.CreateAsync(new IdentityRole<Guid>(roleName)).ConfigureAwait(false);
        }
    }
}
