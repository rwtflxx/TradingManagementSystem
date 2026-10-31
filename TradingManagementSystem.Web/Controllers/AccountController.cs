using System.Security.Claims;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using MongoDB.Driver;
using TradingManagementSystem.Domain.Entities;
using TradingManagementSystem.Infrastructure.Data;
using TradingManagementSystem.Web.Models;

namespace TradingManagementSystem.Web.Controllers;

public class AccountController : Controller
{
    private readonly MongoDbContext _db;

    public AccountController(MongoDbContext db) => _db = db;

    [AllowAnonymous]
    [HttpGet]
    public IActionResult Login(string? returnUrl = null)
    {
        if (User.Identity?.IsAuthenticated == true)
            return RedirectToAction("Index", "Home");
        ViewData["ReturnUrl"] = Url.IsLocalUrl(returnUrl) ? returnUrl : null;
        return View(new LoginInput());
    }

    [AllowAnonymous]
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Login(LoginInput model, string? returnUrl = null)
    {
        if (!ModelState.IsValid) return View(model);
        var login = model.Login.Trim().ToLowerInvariant();
        var user = await _db.Employees.Find(x => x.Login == login && x.IsActive)
            .FirstOrDefaultAsync();
        var ok = user != null && new PasswordHasher<Employee>()
            .VerifyHashedPassword(user, user.PasswordHash, model.Password)
            != PasswordVerificationResult.Failed;
        if (!ok)
        {
            ModelState.AddModelError(string.Empty, "Невірний логін або пароль");
            return View(model);
        }
        var claims = new[]
        {
            new Claim(ClaimTypes.NameIdentifier, user!.Id),
            new Claim(ClaimTypes.Name, user.FirstName + " " + user.LastName),
            new Claim(ClaimTypes.Role, user.Role.ToString())
        };
        await HttpContext.SignInAsync(CookieAuthenticationDefaults.AuthenticationScheme,
            new ClaimsPrincipal(new ClaimsIdentity(claims, CookieAuthenticationDefaults.AuthenticationScheme)),
            new AuthenticationProperties { IsPersistent = false });
        if (Url.IsLocalUrl(returnUrl)) return LocalRedirect(returnUrl!);
        return RedirectToAction("Index", "Home");
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Logout()
    {
        await HttpContext.SignOutAsync(CookieAuthenticationDefaults.AuthenticationScheme);
        return RedirectToAction("Login");
    }

    [HttpGet]
    public IActionResult ChangePassword() => View(new PasswordChangeInput());

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> ChangePassword(PasswordChangeInput model)
    {
        if (!ModelState.IsValid) return View(model);
        var id = User.FindFirstValue(ClaimTypes.NameIdentifier);
        var user = await _db.Employees.Find(x => x.Id == id && x.IsActive).FirstOrDefaultAsync();
        if (user == null) return Unauthorized();
        var hasher = new PasswordHasher<Employee>();
        if (hasher.VerifyHashedPassword(user, user.PasswordHash, model.CurrentPassword) == PasswordVerificationResult.Failed)
        {
            ModelState.AddModelError(string.Empty, "Поточний пароль неправильний");
            return View(model);
        }
        user.ChangePassword(hasher.HashPassword(user, model.NewPassword));
        await _db.Employees.ReplaceOneAsync(x => x.Id == user.Id, user);
        TempData["Flash"] = "Пароль змінено.";
        return RedirectToAction("Index", "Home");
    }

    [HttpGet]
    public IActionResult AccessDenied() => View();
}
