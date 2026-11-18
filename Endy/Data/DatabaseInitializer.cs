using Endy.Models;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;

namespace Endy.Data;

public static class DatabaseInitializer
{
    private static readonly string[] RoleNames = [AppRoles.SuperAdmin, AppRoles.Admin, AppRoles.User, AppRoles.SecurityUser];
    private static readonly string[] DepartmentNames = ["Operations", "Commercial", "Supply Chain", "Finance", "Human Resources", "IT"];

    public static async Task InitializeAsync(IServiceProvider services, IConfiguration configuration)
    {
        using var scope = services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        await db.Database.MigrateAsync();

        var roleManager = scope.ServiceProvider.GetRequiredService<RoleManager<Microsoft.AspNetCore.Identity.IdentityRole>>();
        foreach (var role in RoleNames)
            if (!await roleManager.RoleExistsAsync(role) && !(await roleManager.CreateAsync(new Microsoft.AspNetCore.Identity.IdentityRole(role))).Succeeded)
                throw new InvalidOperationException($"Unable to create the '{role}' role.");

        foreach (var name in DepartmentNames)
            if (!await db.Departments.AnyAsync(x => x.Name == name)) db.Departments.Add(new Department { Name = name, IsActive = true });
        await db.SaveChangesAsync();

        var userName = configuration["BootstrapAdmin:UserName"];
        var password = configuration["BootstrapAdmin:Password"];
        if (string.IsNullOrWhiteSpace(userName) != string.IsNullOrWhiteSpace(password))
            throw new InvalidOperationException("Configure both BootstrapAdmin:UserName and BootstrapAdmin:Password, or neither.");
        var users = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();
        if (!string.IsNullOrWhiteSpace(userName))
        {
            var bootstrapUser = await users.FindByNameAsync(userName);
            if (bootstrapUser is null)
            {
                bootstrapUser = new ApplicationUser { UserName = userName, Email = configuration["BootstrapAdmin:Email"], EmailConfirmed = true, IsActive = true };
                var created = await users.CreateAsync(bootstrapUser, password!);
                if (!created.Succeeded) throw new InvalidOperationException("Unable to create the bootstrap administrator: " + string.Join("; ", created.Errors.Select(x => x.Description)));
            }
            var roles = await users.GetRolesAsync(bootstrapUser);
            if (!roles.Contains(AppRoles.SuperAdmin))
            {
                var assigned = await users.AddToRoleAsync(bootstrapUser, AppRoles.SuperAdmin);
                if (!assigned.Succeeded) throw new InvalidOperationException("Unable to assign the Super Admin role.");
            }
            if (!bootstrapUser.IsActive)
            {
                bootstrapUser.IsActive = true;
                var updated = await users.UpdateAsync(bootstrapUser);
                if (!updated.Succeeded) throw new InvalidOperationException("Unable to activate the bootstrap administrator.");
            }
        }
        if (!(await users.GetUsersInRoleAsync(AppRoles.SuperAdmin)).Any(x => x.IsActive))
            throw new InvalidOperationException("No active Super Admin exists. Configure BootstrapAdmin:UserName and BootstrapAdmin:Password before starting the application.");
    }
}
