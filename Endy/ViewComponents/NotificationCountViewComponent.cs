using System.Security.Claims;
using Endy.Data;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace Endy.ViewComponents;

public sealed class NotificationCountViewComponent(ApplicationDbContext db) : ViewComponent
{
    public async Task<IViewComponentResult> InvokeAsync()
    {
        var userId = HttpContext.User.FindFirstValue(ClaimTypes.NameIdentifier);
        var count = string.IsNullOrEmpty(userId) ? 0 : await db.Notifications.CountAsync(x => x.RecipientUserId == userId && !x.IsRead, HttpContext.RequestAborted);
        return View(count);
    }
}
