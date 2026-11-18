using Endy.Data;
using Endy.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace Endy.Controllers;

[Authorize(Policy = "AccountManagement")]
public sealed class UsersController(ApplicationDbContext db, UserManager<ApplicationUser> userManager, RoleManager<IdentityRole> roleManager) : AppController
{
    public async Task<IActionResult> Index(string? search, string? role, int? departmentId, CancellationToken cancellationToken)
    {
        SetNavigation();
        var users = db.Users.Include(x => x.Department).AsQueryable();
        if (!string.IsNullOrWhiteSpace(search)) users = users.Where(x => x.FullName.Contains(search) || x.UserName!.Contains(search));
        if (departmentId.HasValue) users = users.Where(x => x.DepartmentId == departmentId.Value);
        var list = await users.OrderBy(x => x.FullName).ToListAsync(cancellationToken);
        var roles = new Dictionary<string, string>();
        foreach (var user in list)
        {
            var userRoles = await userManager.GetRolesAsync(user);
            roles[user.Id] = string.Join(", ", userRoles);
        }
        if (!string.IsNullOrWhiteSpace(role)) list = list.Where(x => roles[x.Id].Split(", ").Contains(role, StringComparer.Ordinal)).ToList();
        ViewBag.UserRoles = roles;
        ViewBag.Departments = await db.Departments.OrderBy(x => x.Name).ToListAsync(cancellationToken);
        ViewBag.Search = search; ViewBag.Role = role; ViewBag.DepartmentId = departmentId;
        return View(list);
    }

    [HttpGet]
    public async Task<IActionResult> Create(CancellationToken cancellationToken)
    {
        SetNavigation(); await LoadDepartmentsAsync(cancellationToken); ViewBag.IsCreate = true;
        return View(new UserFormInput());
    }

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> Create(UserFormInput input, CancellationToken cancellationToken)
    {
        SetNavigation(); await LoadDepartmentsAsync(cancellationToken); ViewBag.IsCreate = true;
        if (!AppRoles.All.Contains(input.Role, StringComparer.Ordinal)) ModelState.AddModelError(nameof(input.Role), "Select a valid role.");
        if (input.Role == AppRoles.User && !input.DepartmentId.HasValue) ModelState.AddModelError(nameof(input.DepartmentId), "Department employees must be assigned to a department.");
        if (string.IsNullOrWhiteSpace(input.Password)) ModelState.AddModelError(nameof(input.Password), "An initial password is required.");
        if (!ModelState.IsValid) return View(input);
        if (!await roleManager.RoleExistsAsync(input.Role)) return StatusCode(500, "The selected system role has not been configured.");
        var user = new ApplicationUser { UserName = input.UserName.Trim(), Email = input.Email?.Trim(), FullName = input.FullName.Trim(), DepartmentId = input.DepartmentId, IsActive = true };
        var created = await userManager.CreateAsync(user, input.Password!);
        if (!created.Succeeded)
        {
            foreach (var error in created.Errors) ModelState.AddModelError(string.Empty, error.Description);
            return View(input);
        }
        var assigned = await userManager.AddToRoleAsync(user, input.Role);
        if (!assigned.Succeeded)
        {
            await userManager.DeleteAsync(user);
            foreach (var error in assigned.Errors) ModelState.AddModelError(string.Empty, error.Description);
            return View(input);
        }
        TempData["Success"] = "User account created successfully.";
        return RedirectToAction(nameof(Index));
    }

    [HttpGet]
    public async Task<IActionResult> Edit(string id, CancellationToken cancellationToken)
    {
        var user = await db.Users.Include(x => x.Department).SingleOrDefaultAsync(x => x.Id == id, cancellationToken);
        if (user is null) return NotFound();
        SetNavigation(); await LoadDepartmentsAsync(cancellationToken);
        ViewBag.UserId = user.Id; ViewBag.IsActive = user.IsActive;
        var roles = await userManager.GetRolesAsync(user);
        return View(new UserFormInput { FullName = user.FullName, UserName = user.UserName!, Email = user.Email, Role = roles.FirstOrDefault() ?? AppRoles.User, DepartmentId = user.DepartmentId });
    }

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> Edit(string id, UserFormInput input, bool isActive, CancellationToken cancellationToken)
    {
        SetNavigation(); await LoadDepartmentsAsync(cancellationToken); ViewBag.UserId = id; ViewBag.IsActive = isActive;
        if (!AppRoles.All.Contains(input.Role, StringComparer.Ordinal)) ModelState.AddModelError(nameof(input.Role), "Select a valid role.");
        if (input.Role == AppRoles.User && !input.DepartmentId.HasValue) ModelState.AddModelError(nameof(input.DepartmentId), "Department employees must be assigned to a department.");
        var user = await userManager.FindByIdAsync(id);
        if (user is null) return NotFound();
        if (id == CurrentUserId && input.Role != AppRoles.SuperAdmin) ModelState.AddModelError(nameof(input.Role), "You cannot remove your own Super Admin permission.");
        if (id == CurrentUserId && !isActive) ModelState.AddModelError(string.Empty, "You cannot suspend your own Super Admin account.");
        if (!ModelState.IsValid) return View(input);
        user.FullName = input.FullName.Trim(); user.UserName = input.UserName.Trim(); user.Email = input.Email?.Trim(); user.DepartmentId = input.DepartmentId; user.IsActive = isActive;
        var updated = await userManager.UpdateAsync(user);
        if (!updated.Succeeded)
        {
            foreach (var error in updated.Errors) ModelState.AddModelError(string.Empty, error.Description);
            return View(input);
        }
        var existingRoles = await userManager.GetRolesAsync(user);
        var removed = await userManager.RemoveFromRolesAsync(user, existingRoles);
        if (!removed.Succeeded) return StatusCode(500, "Unable to update account permissions.");
        var assigned = await userManager.AddToRoleAsync(user, input.Role);
        if (!assigned.Succeeded) return StatusCode(500, "Unable to assign the selected account role.");
        var stampUpdated = await userManager.UpdateSecurityStampAsync(user);
        if (!stampUpdated.Succeeded) return StatusCode(500, "Account permissions changed, but active sessions could not be revoked.");
        TempData["Success"] = "User account updated successfully.";
        return RedirectToAction(nameof(Index));
    }

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> ResetPassword(string id, string newPassword)
    {
        var user = await userManager.FindByIdAsync(id);
        if (user is null) return NotFound();
        if (string.IsNullOrWhiteSpace(newPassword)) return BadRequest("A new password is required.");
        var token = await userManager.GeneratePasswordResetTokenAsync(user);
        var result = await userManager.ResetPasswordAsync(user, token, newPassword);
        if (!result.Succeeded)
        {
            TempData["Error"] = string.Join(" ", result.Errors.Select(x => x.Description));
            return RedirectToAction(nameof(Edit), new { id });
        }
        TempData["Success"] = "Password reset successfully.";
        return RedirectToAction(nameof(Edit), new { id });
    }

    private async Task LoadDepartmentsAsync(CancellationToken cancellationToken) =>
        ViewBag.Departments = await db.Departments.Where(x => x.IsActive).OrderBy(x => x.Name).ToListAsync(cancellationToken);
}
