using Endy.Models;
using Endy.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace Endy.Controllers;

[Authorize(Roles = AppRoles.SecurityUser + "," + AppRoles.Admin)]
public sealed class TrackingController(AccessManagementService service, UserManager<ApplicationUser> userManager) : AppController
{
    public async Task<IActionResult> Index(CancellationToken cancellationToken)
    {
        SetNavigation();
        var visitors = await service.RequestQuery().Where(r => r.Status == RequestStatus.Approved).OrderByDescending(r => r.Visit == null ? DateTime.MinValue : r.Visit.CheckInTime).ThenBy(r => r.FullName).ToListAsync(cancellationToken);
        return View(visitors);
    }

    [HttpPost, Authorize(Policy = "VisitorSecurity"), ValidateAntiForgeryToken]
    public async Task<IActionResult> CheckIn(int id, CancellationToken cancellationToken)
    {
        var actor = await userManager.GetUserAsync(User);
        if (actor is null) return Challenge();
        var saved = await service.CheckInAsync(id, actor, cancellationToken);
        TempData[saved ? "Success" : "Error"] = saved ? "Customer check-in recorded." : "Check-in could not be recorded. Verify the permit is approved and has not already been used.";
        return RedirectToAction(nameof(Index));
    }

    [HttpPost, Authorize(Policy = "VisitorSecurity"), ValidateAntiForgeryToken]
    public async Task<IActionResult> CheckOut(int id, CancellationToken cancellationToken)
    {
        var actor = await userManager.GetUserAsync(User);
        if (actor is null) return Challenge();
        var saved = await service.CheckOutAsync(id, actor, cancellationToken);
        TempData[saved ? "Success" : "Error"] = saved ? "Customer check-out recorded." : "Check-out could not be recorded because this visit is not active.";
        return RedirectToAction(nameof(Index));
    }
}