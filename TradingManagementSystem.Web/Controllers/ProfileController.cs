using System.Security.Claims;
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

[Authorize]
public class ProfileController : Controller
{
    private readonly MongoDbContext _db;
    public ProfileController(MongoDbContext db) => _db = db;

    private async Task<Employee?> FindCurrentAsync()
    {
        var id = User.FindFirstValue(ClaimTypes.NameIdentifier);
        if (string.IsNullOrWhiteSpace(id)) return null;
        return await _db.Employees.Find(e => e.Id == id && e.IsActive).FirstOrDefaultAsync();
    }

    private static ProfilePageViewModel BuildPage(Employee employee) => new()
    {
        Id = employee.Id,
        Role = employee.Role.ToString(),
        IsActive = employee.IsActive,
        Details = new ProfileDetailsInput
        {
            FirstName = employee.FirstName,
            LastName = employee.LastName,
            Login = employee.Login
        }
    };

    [HttpGet]
    public async Task<IActionResult> Index()
    {
        var employee = await FindCurrentAsync();
        return employee is null ? Unauthorized() : View(BuildPage(employee));
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> UpdateDetails([Bind(Prefix = "Details")] ProfileDetailsInput details)
    {
        var employee = await FindCurrentAsync();
        if (employee is null) return Unauthorized();
        if (string.IsNullOrWhiteSpace(details.FirstName)) ModelState.AddModelError("Details.FirstName", "Введи ім'я");
        if (string.IsNullOrWhiteSpace(details.LastName)) ModelState.AddModelError("Details.LastName", "Введи прізвище");
        if (!ModelState.IsValid)
        {
            var page = BuildPage(employee);
            page.Details = details;
            return View("Index", page);
        }

        var login = details.Login.Trim().ToLowerInvariant();
        if (await _db.Employees.Find(e => e.Login == login && e.Id != employee.Id).AnyAsync())
        {
            ModelState.AddModelError("Details.Login", "Цей логін уже зайнятий");
            var page = BuildPage(employee);
            page.Details = details;
            return View("Index", page);
        }

        var first = details.FirstName.Trim();
        var last = details.LastName.Trim();
        var update = Builders<Employee>.Update
            .Set(e => e.FirstName, first)
            .Set(e => e.LastName, last)
            .Set(e => e.Login, login);
        try
        {
            var result = await _db.Employees.UpdateOneAsync(e => e.Id == employee.Id && e.IsActive, update);
            if (result.MatchedCount == 0) return Unauthorized();
        }
        catch (MongoWriteException ex) when (ex.WriteError?.Category == ServerErrorCategory.DuplicateKey)
        {
            ModelState.AddModelError("Details.Login", "Цей логін уже зайнятий");
            var page = BuildPage(employee);
            page.Details = details;
            return View("Index", page);
        }

        // Refresh the display name in the current cookie; permissions remain unchanged.
        var claims = new[]
        {
            new Claim(ClaimTypes.NameIdentifier, employee.Id),
            new Claim(ClaimTypes.Name, first + " " + last),
            new Claim(ClaimTypes.Role, employee.Role.ToString()),
            new Claim("sf.point", employee.TradingPointId ?? string.Empty)
        };
        await HttpContext.SignInAsync(CookieAuthenticationDefaults.AuthenticationScheme,
            new ClaimsPrincipal(new ClaimsIdentity(claims, CookieAuthenticationDefaults.AuthenticationScheme)),
            new AuthenticationProperties { IsPersistent = false });
        TempData["Flash"] = "Дані профілю збережено.";
        return RedirectToAction(nameof(Index));
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> UpdatePassword([Bind(Prefix = "Password")] ProfilePasswordInput password)
    {
        var employee = await FindCurrentAsync();
        if (employee is null) return Unauthorized();
        if (!ModelState.IsValid) return View("Index", BuildPage(employee));

        var hasher = new PasswordHasher<Employee>();
        var valid = hasher.VerifyHashedPassword(employee, employee.PasswordHash, password.CurrentPassword);
        if (valid == PasswordVerificationResult.Failed)
        {
            ModelState.AddModelError("Password.CurrentPassword", "Неправильний поточний пароль");
            return View("Index", BuildPage(employee));
        }
        if (password.CurrentPassword == password.NewPassword)
        {
            ModelState.AddModelError("Password.NewPassword", "Новий пароль повинен відрізнятися від поточного");
            return View("Index", BuildPage(employee));
        }

        var newHash = hasher.HashPassword(employee, password.NewPassword);
        var filter = Builders<Employee>.Filter.Where(e => e.Id == employee.Id && e.IsActive) &
                     Builders<Employee>.Filter.Eq(e => e.PasswordHash, employee.PasswordHash);
        var result = await _db.Employees.UpdateOneAsync(filter,
            Builders<Employee>.Update.Set(e => e.PasswordHash, newHash));
        if (result.MatchedCount == 0)
        {
            ModelState.AddModelError(string.Empty, "Пароль уже змінено в іншій сесії. Онови сторінку.");
            return View("Index", BuildPage(employee));
        }
        TempData["Flash"] = "Пароль успішно змінено.";
        return RedirectToAction(nameof(Index));
    }
}
