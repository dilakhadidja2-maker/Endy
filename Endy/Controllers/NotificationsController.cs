using Endy.Data;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace Endy.Controllers;

[Authorize]
public sealed class NotificationsController(ApplicationDbContext db) : AppController
{
    public async Task<IActionResult> Index(CancellationToken cancellationToken)
    {
        SetNavigation();
        var notifications = await db.Notifications.Where(n => n.RecipientUserId == CurrentUserId).OrderByDescending(n => n.CreatedAt).ToListAsync(cancellationToken);
        foreach (var notification in notifications.Where(n => !n.IsRead)) notification.IsRead = true;
        await db.SaveChangesAsync(cancellationToken);
        return View(notifications);
    }
}