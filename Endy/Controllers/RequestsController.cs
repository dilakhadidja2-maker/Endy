using Endy.Data;
using Endy.Models;
using Endy.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace Endy.Controllers;

[Authorize(Roles = AppRoles.User + "," + AppRoles.Admin + "," + AppRoles.SuperAdmin)]
public sealed class RequestsController(AccessManagementService service, ApplicationDbContext db, Microsoft.AspNetCore.Identity.UserManager<ApplicationUser> userManager) : AppController
{
    public async Task<IActionResult> Index(string? search, string? status, int? departmentId, DateTime? from, DateTime? to, string? createdBy, CancellationToken cancellationToken)
    {
        SetNavigation();
        var items = service.RequestQuery();
        if (User.IsInRole(AppRoles.User)) items = items.Where(r => r.CreatedByUserId == CurrentUserId);
        if (!string.IsNullOrWhiteSpace(search)) items = items.Where(r => r.FullName.Contains(search) || r.RegistrationNumber.Contains(search));
        if (!string.IsNullOrWhiteSpace(status) && Enum.TryParse<RequestStatus>(status, true, out var parsed)) items = items.Where(r => r.Status == parsed);
        if (User.IsInRole(AppRoles.Admin) || User.IsInRole(AppRoles.SuperAdmin))
        {
            if (departmentId.HasValue) items = items.Where(r => r.DepartmentId == departmentId.Value);
            if (from.HasValue) { var startUtc = DateTime.SpecifyKind(from.Value.Date, DateTimeKind.Local).ToUniversalTime(); items = items.Where(r => r.RequestDate >= startUtc); }
            if (to.HasValue) { var endUtc = DateTime.SpecifyKind(to.Value.Date.AddDays(1), DateTimeKind.Local).ToUniversalTime(); items = items.Where(r => r.RequestDate < endUtc); }
            if (!string.IsNullOrWhiteSpace(createdBy)) items = items.Where(r => r.CreatedBy.Contains(createdBy));
            ViewBag.Departments = await db.Departments.Where(d => d.IsActive).OrderBy(d => d.Name).ToListAsync(cancellationToken);
        }
        ViewBag.Search = search; ViewBag.Status = status; ViewBag.DepartmentId = departmentId; ViewBag.From = from?.ToString("yyyy-MM-dd"); ViewBag.To = to?.ToString("yyyy-MM-dd"); ViewBag.CreatedBy = createdBy;
        return View(await items.OrderByDescending(r => r.RequestDate).ToListAsync(cancellationToken));
    }

    [Authorize(Roles = AppRoles.User)]
    public async Task<IActionResult> Create(CancellationToken cancellationToken) { SetNavigation(); await SetDepartmentViewAsync(cancellationToken); return View(new AccessRequestInput()); }

    [HttpPost, Authorize(Roles = AppRoles.User), ValidateAntiForgeryToken]
    public async Task<IActionResult> Create(AccessRequestInput input, CancellationToken cancellationToken)
    {
        SetNavigation(); await SetDepartmentViewAsync(cancellationToken);
        if (!ModelState.IsValid) return View(input);
        var user = await userManager.GetUserAsync(User);
        if (user is null) return Challenge();
        try { await service.CreateAsync(input, user, cancellationToken); }
        catch (InvalidOperationException exception) { ModelState.AddModelError(string.Empty, exception.Message); return View(input); }
        TempData["Success"] = "Access request submitted for administrator review.";
        return RedirectToAction(nameof(Index));
    }

    public async Task<IActionResult> Details(int id, CancellationToken cancellationToken)
    {
        SetNavigation();
        var item = await service.FindAsync(id, cancellationToken);
        if (item is null) return NotFound();
        if (User.IsInRole(AppRoles.User) && item.CreatedByUserId != CurrentUserId) return Forbid();
        ViewBag.CanReview = User.IsInRole(AppRoles.Admin) && item.CreatedByUserId != CurrentUserId;
        return View(item);
    }

    [HttpPost, Authorize(Policy = "RequestReview"), ValidateAntiForgeryToken]
    public async Task<IActionResult> Decide(int id, RequestStatus status, string? comment, CancellationToken cancellationToken)
    {
        var user = await userManager.GetUserAsync(User);
        if (user is null) return Challenge();
        var saved = await service.DecideAsync(id, status, comment, user, cancellationToken);
        if (!saved) TempData["Error"] = "The decision could not be saved. Check the request state, your permissions, and required comments.";
        else TempData["Success"] = "Request decision saved and the user has been notified.";
        return RedirectToAction(nameof(Details), new { id });
    }

    [Authorize(Roles = AppRoles.User)]
    public async Task<IActionResult> Edit(int id, CancellationToken cancellationToken)
    {
        SetNavigation();
        var item = await service.FindAsync(id, cancellationToken);
        if (item is null) return NotFound();
        if (item.CreatedByUserId != CurrentUserId) return Forbid();
        if (item.Status != RequestStatus.ModificationRequired) return BadRequest("Only requests requiring modification can be resubmitted.");
        return View(new AccessRequestInput { FullName = item.FullName, VehicleType = item.VehicleType, RegistrationNumber = item.RegistrationNumber, CompanyName = item.CompanyName, VisitDurationHours = item.VisitDurationHours, SecurityDepartment = item.SecurityDepartment });
    }

    [HttpPost, Authorize(Roles = AppRoles.User), ValidateAntiForgeryToken]
    public async Task<IActionResult> Edit(int id, AccessRequestInput input, CancellationToken cancellationToken)
    {
        SetNavigation();
        if (!ModelState.IsValid) return View(input);
        var user = await userManager.GetUserAsync(User);
        if (user is null) return Challenge();
        if (!await service.ResubmitAsync(id, input, user, cancellationToken)) return Forbid();
        TempData["Success"] = "Corrected request resubmitted for review.";
        return RedirectToAction(nameof(Details), new { id });
    }

    [HttpGet, Authorize(Roles = AppRoles.Admin)]
    public async Task<IActionResult> Permit(int id, CancellationToken cancellationToken)
    {
        var request = await service.FindAsync(id, cancellationToken);
        if (request is null) return NotFound();
        if (request.Status != RequestStatus.Approved) return Forbid();
        return View(request);
    }

    private async Task SetDepartmentViewAsync(CancellationToken cancellationToken)
    {
        var user = await userManager.GetUserAsync(User);
        ViewBag.DepartmentName = user?.DepartmentId is int departmentId
            ? await db.Departments.Where(x => x.Id == departmentId && x.IsActive).Select(x => x.Name).SingleOrDefaultAsync(cancellationToken) ?? "Department unavailable"
            : "Not assigned";
    }
}