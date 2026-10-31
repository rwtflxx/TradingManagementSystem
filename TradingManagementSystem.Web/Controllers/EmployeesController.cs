using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using MongoDB.Driver;
using TradingManagementSystem.Domain.Entities;
using TradingManagementSystem.Domain.Enums;
using TradingManagementSystem.Infrastructure.Data;
using TradingManagementSystem.Web.Models;

namespace TradingManagementSystem.Web.Controllers;

[Authorize(Roles = nameof(EmployeeRole.Director))]
public class EmployeesController : Controller
{
    private readonly MongoDbContext _db;
    public EmployeesController(MongoDbContext db) => _db = db;

    [HttpGet]
    public async Task<IActionResult> Index() =>
        View(await _db.Employees.Find(_ => true).ToListAsync());

    [HttpGet]
    public IActionResult Create() => View(new EmployeeInput { Role = EmployeeRole.Seller });

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Create(EmployeeInput model)
    {
        if (!Enum.IsDefined(model.Role))
            ModelState.AddModelError(nameof(model.Role), "Неправильна роль");
        if (!ModelState.IsValid) return View(model);
        var login = model.Login.Trim().ToLowerInvariant();
        if (await _db.Employees.Find(x => x.Login == login).AnyAsync())
        {
            ModelState.AddModelError(nameof(model.Login), "Такий логін уже існує");
            return View(model);
        }
        var employee = new Employee
        {
            Id = Guid.NewGuid().ToString(),
            Login = login,
            FirstName = model.FirstName.Trim(),
            LastName = model.LastName.Trim(),
            Role = model.Role
        };
        employee.ChangePassword(new PasswordHasher<Employee>().HashPassword(employee, model.Password));
        try { await _db.Employees.InsertOneAsync(employee); }
        catch (MongoWriteException)
        {
            ModelState.AddModelError(nameof(model.Login), "Логін уже використовується");
            return View(model);
        }
        TempData["Flash"] = "Працівника створено.";
        return RedirectToAction(nameof(Index));
    }
}
