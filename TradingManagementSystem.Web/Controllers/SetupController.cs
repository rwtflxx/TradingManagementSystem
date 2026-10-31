using System.Net;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using MongoDB.Driver;
using TradingManagementSystem.Domain.Entities;
using TradingManagementSystem.Domain.Enums;
using TradingManagementSystem.Infrastructure.Data;
using TradingManagementSystem.Web.Models;

namespace TradingManagementSystem.Web.Controllers;

// First-run setup is ONLY available locally in Development and only while no employees exist.
[AllowAnonymous]
public class SetupController : Controller
{
    private readonly MongoDbContext _db;
    private readonly IWebHostEnvironment _environment;

    public SetupController(MongoDbContext db, IWebHostEnvironment environment)
    {
        _db = db;
        _environment = environment;
    }

    private bool LocalDevelopment =>
        _environment.IsDevelopment() &&
        HttpContext.Connection.RemoteIpAddress is { } ip &&
        IPAddress.IsLoopback(ip);

    [HttpGet]
    public async Task<IActionResult> Index()
    {
        if (!LocalDevelopment) return NotFound();
        if (await _db.Employees.Find(_ => true).AnyAsync())
            return RedirectToAction("Login", "Account");
        return View(new EmployeeInput { Role = EmployeeRole.Director });
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Index(EmployeeInput model)
    {
        if (!LocalDevelopment) return NotFound();
        if (await _db.Employees.Find(_ => true).AnyAsync())
            return Conflict("Перший користувач вже існує. Початкове налаштування завершено.");
        model.Role = EmployeeRole.Director; // Never accept a role from this form.
        if (!ModelState.IsValid) return View(model);
        var login = model.Login.Trim().ToLowerInvariant();
        await _db.Employees.Indexes.CreateOneAsync(
            new CreateIndexModel<Employee>(Builders<Employee>.IndexKeys.Ascending(x => x.Login),
                new CreateIndexOptions { Unique = true }));
        var person = new Employee
        {
            Id = Guid.NewGuid().ToString(),
            FirstName = model.FirstName.Trim(),
            LastName = model.LastName.Trim(),
            Login = login,
            Role = EmployeeRole.Director
        };
        person.ChangePassword(new PasswordHasher<Employee>().HashPassword(person, model.Password));
        try { await _db.Employees.InsertOneAsync(person); }
        catch (MongoWriteException) { return Conflict("Не вдалося створити першого користувача. Перевір базу."); }
        TempData["Flash"] = "Директора створено. Увійди зі своїм логіном і паролем.";
        return RedirectToAction("Login", "Account");
    }
}
