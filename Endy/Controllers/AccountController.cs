using Endy.Data;
using Endy.Models;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace Endy.Controllers;

public sealed class AccountController(SignInManager<ApplicationUser> signInManager, ApplicationDbContext db) : Controller
{
    [HttpGet]
    public IActionResult Login(string? returnUrl = null) { ViewBag.ReturnUrl = returnUrl; return View(new LoginInput()); }

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> Login(LoginInput input, string? returnUrl = null)
    {
        ViewBag.ReturnUrl = returnUrl;
        if (!ModelState.IsValid) return View(input);
        var normalizedUserName = input.UserName.ToUpperInvariant();
        var account = await db.Users.SingleOrDefaultAsync(x => x.NormalizedUserName == normalizedUserName);
        if (account is null || !account.IsActive)
        {
            ModelState.AddModelError(string.Empty, "Invalid username or password, or the account is inactive.");
            return View(input);
        }
        var result = await signInManager.PasswordSignInAsync(account, input.Password, input.RememberMe, lockoutOnFailure: true);
        if (result.Succeeded)
            return Url.IsLocalUrl(returnUrl) ? LocalRedirect(returnUrl!) : RedirectToAction("Index", "Home");
        ModelState.AddModelError(string.Empty, result.IsLockedOut ? "This account is temporarily locked. Try again later." : "Invalid username or password.");
        return View(input);
    }

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> Logout()
    {
        await signInManager.SignOutAsync();
        return RedirectToAction(nameof(Login));
    }

    [HttpGet]
    public IActionResult AccessDenied() => View();
}