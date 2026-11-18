using Endy.Data;
using Endy.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Diagnostics;

namespace Endy.Controllers;

[Authorize]
public sealed class HomeController(ApplicationDbContext db) : AppController
{
    public async Task<IActionResult> Index(CancellationToken cancellationToken)
    {
        SetNavigation();
        var query = db.AccessRequests.Include(x => x.Department).Include(x => x.Visit).AsQueryable();
        if (User.IsInRole(AppRoles.User)) query = query.Where(x => x.CreatedByUserId == CurrentUserId);
        if (User.IsInRole(AppRoles.SecurityUser)) query = query.Where(x => x.Status == RequestStatus.Approved);
        var requests = await query.ToListAsync(cancellationToken);
        return View(new DashboardViewModel
        {
            TotalRequests = requests.Count,
            PendingRequests = requests.Count(r => r.Status == RequestStatus.PendingReview),
            ApprovedRequests = requests.Count(r => r.Status == RequestStatus.Approved),
            RejectedRequests = requests.Count(r => r.Status == RequestStatus.Rejected),
            ModificationRequired = requests.Count(r => r.Status == RequestStatus.ModificationRequired),
            ActiveVisitors = requests.Count(r => r.VisitStatus is VisitStatus.Active or VisitStatus.ExpiringSoon),
            ExpiredVisits = requests.Count(r => r.VisitStatus == VisitStatus.Expired),
            TotalDepartments = User.IsInRole(AppRoles.Admin) || User.IsInRole(AppRoles.SuperAdmin) ? await db.Departments.CountAsync(x => x.IsActive, cancellationToken) : 0,
            RecentRequests = requests.OrderByDescending(r => r.RequestDate).Take(5).ToList()
        });
    }

    public IActionResult Privacy() => View();

    [AllowAnonymous, ResponseCache(Duration = 0, Location = ResponseCacheLocation.None, NoStore = true)]
    public IActionResult Error() => View(new ErrorViewModel { RequestId = Activity.Current?.Id ?? HttpContext.TraceIdentifier });
}
