using Endy.Models;
using Microsoft.AspNetCore.Mvc;
using System.Security.Claims;

namespace Endy.Controllers;

public abstract class AppController : Controller
{
    protected string CurrentUser => User.Identity?.Name ?? string.Empty;
    protected string CurrentUserId => User.FindFirstValue(ClaimTypes.NameIdentifier) ?? string.Empty;
    protected bool IsAdmin => User.IsInRole(AppRoles.Admin);
    protected void SetNavigation()
    {
        ViewBag.CurrentUser = CurrentUser;
        ViewBag.IsAdmin = IsAdmin;
        ViewBag.IsSuperAdmin = User.IsInRole(AppRoles.SuperAdmin);
        ViewBag.IsSecurityUser = User.IsInRole(AppRoles.SecurityUser);
        ViewBag.CanCreateRequest = User.IsInRole(AppRoles.User);
    }
}