using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using MongoDB.Driver;
using TradingManagementSystem.Domain.Entities;
using TradingManagementSystem.Domain.Enums;
using TradingManagementSystem.Infrastructure.Data;
using TradingManagementSystem.Web.Models;

namespace TradingManagementSystem.Web.Controllers;

[Authorize(Roles = "Director,TradingPointManager")]
public class EmployeesController : Controller
{
    private readonly MongoDbContext _db;
    public EmployeesController(MongoDbContext db) => _db = db;
    private bool Director => User.IsInRole(nameof(EmployeeRole.Director));

    private async Task<Employee?> CurrentAsync()
    {
        var id = User.FindFirstValue(ClaimTypes.NameIdentifier);
        if (string.IsNullOrWhiteSpace(id)) return null;
        return await _db.Employees.Find(x => x.Id == id && x.IsActive).FirstOrDefaultAsync();
    }

    private bool CanManage(Employee actor, Employee subject) =>
        actor.Id != subject.Id &&
        (actor.Role == EmployeeRole.Director && subject.Role != EmployeeRole.Director ||
         actor.Role == EmployeeRole.TradingPointManager &&
         subject.Role == EmployeeRole.Seller &&
         !string.IsNullOrWhiteSpace(actor.TradingPointId) &&
         subject.TradingPointId == actor.TradingPointId);

    private async Task<List<TradingPoint>> PointsAsync(Employee actor) =>
        actor.Role == EmployeeRole.Director
            ? await _db.TradingPoints.Find(_ => true).ToListAsync()
            : await _db.TradingPoints.Find(p => p.Id == actor.TradingPointId).ToListAsync();

    private async Task SetPointOptionsAsync(Employee actor)
    {
        ViewBag.Points = (await PointsAsync(actor)).Select(p =>
            new SelectListItem(p.Name, p.Id)).ToList();
        ViewBag.IsDirector = Director;
    }

    [HttpGet]
    public async Task<IActionResult> Index()
    {
        var actor = await CurrentAsync();
        if (actor == null) return Unauthorized();
        if (!Director && string.IsNullOrWhiteSpace(actor.TradingPointId)) return Forbid();
        var list = await _db.Employees.Find(Director
            ? Builders<Employee>.Filter.Empty
            : Builders<Employee>.Filter.Where(e =>
                e.Role == EmployeeRole.Seller && e.TradingPointId == actor.TradingPointId))
            .ToListAsync();
        var points = await PointsAsync(actor);
        return View(new EmployeeManagementPage
        {
            Employees = list.OrderBy(e => e.LastName).ThenBy(e => e.FirstName).ToList(),
            PointNames = points.ToDictionary(p => p.Id, p => p.Name),
            IsDirector = Director,
            OwnPointId = actor.TradingPointId
        });
    }

    [HttpGet]
    public async Task<IActionResult> Create()
    {
        var actor = await CurrentAsync();
        if (actor == null) return Unauthorized();
        if (!Director && string.IsNullOrWhiteSpace(actor.TradingPointId)) return Forbid();
        await SetPointOptionsAsync(actor);
        return View(new EmployeeInput
        {
            Role = Director ? EmployeeRole.TradingPointManager : EmployeeRole.Seller,
            TradingPointId = Director ? null : actor.TradingPointId
        });
    }

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> Create(EmployeeInput model)
    {
        var actor = await CurrentAsync();
        if (actor == null) return Unauthorized();
        if (!Director && string.IsNullOrWhiteSpace(actor.TradingPointId)) return Forbid();

        if (Director)
        {
            if (model.Role != EmployeeRole.TradingPointManager && model.Role != EmployeeRole.SupplyManager && model.Role != EmployeeRole.Seller)
                ModelState.AddModelError(nameof(model.Role), "Директор створює завідувачів, менеджерів із постачання та продавців.");
        }
        else if (model.Role != EmployeeRole.Seller)
            ModelState.AddModelError(nameof(model.Role), "Завідувач може створювати лише продавців.");

        if (!Director) model.TradingPointId = actor.TradingPointId;
        if (model.Role == EmployeeRole.SupplyManager) model.TradingPointId = null;
        if (model.Role == EmployeeRole.TradingPointManager || model.Role == EmployeeRole.Seller)
        {
            if (string.IsNullOrWhiteSpace(model.TradingPointId) ||
                !await _db.TradingPoints.Find(p => p.Id == model.TradingPointId).AnyAsync())
                ModelState.AddModelError(nameof(model.TradingPointId), "Обери існуючу торгову точку.");
        }
        if (!ModelState.IsValid)
        {
            await SetPointOptionsAsync(actor);
            return View(model);
        }

        var login = model.Login.Trim().ToLowerInvariant();
        if (await _db.Employees.Find(e => e.Login == login).AnyAsync())
        {
            ModelState.AddModelError(nameof(model.Login), "Такий логін уже існує.");
            await SetPointOptionsAsync(actor);
            return View(model);
        }
        var person = new Employee
        {
            Id = Guid.NewGuid().ToString(),
            FirstName = model.FirstName.Trim(),
            LastName = model.LastName.Trim(),
            Login = login,
            Role = model.Role,
            TradingPointId = model.TradingPointId
        };
        person.ChangePassword(new PasswordHasher<Employee>().HashPassword(person, model.Password));
        try { await _db.Employees.InsertOneAsync(person); }
        catch (MongoWriteException ex) when (ex.WriteError?.Category == ServerErrorCategory.DuplicateKey)
        {
            ModelState.AddModelError(nameof(model.Login), "Такий логін уже існує.");
            await SetPointOptionsAsync(actor);
            return View(model);
        }
        TempData["Flash"] = "Працівника створено. Передай йому початковий пароль приватно.";
        return RedirectToAction(nameof(Index));
    }

    [HttpGet]
    public async Task<IActionResult> Edit(string id)
    {
        var actor = await CurrentAsync();
        if (actor == null) return Unauthorized();
        var employee = await _db.Employees.Find(e => e.Id == id).FirstOrDefaultAsync();
        if (employee == null) return NotFound();
        if (!CanManage(actor, employee)) return Forbid();
        return View(new EmployeeEditPage
        {
            Id = employee.Id,
            Role = employee.Role.ToString(),
            IsActive = employee.IsActive,
            CanChoosePoint = Director && (employee.Role == EmployeeRole.TradingPointManager || employee.Role == EmployeeRole.Seller),
            TradingPoints = await PointsAsync(actor),
            Details = new EmployeeEditInput
            {
                FirstName = employee.FirstName, LastName = employee.LastName,
                Login = employee.Login, TradingPointId = employee.TradingPointId
            }
        });
    }

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> Edit(string id, [Bind(Prefix = "Details")] EmployeeEditInput details)
    {
        var actor = await CurrentAsync();
        if (actor == null) return Unauthorized();
        var employee = await _db.Employees.Find(e => e.Id == id).FirstOrDefaultAsync();
        if (employee == null) return NotFound();
        if (!CanManage(actor, employee)) return Forbid();
        var pointId = employee.TradingPointId;
        if (Director && (employee.Role == EmployeeRole.TradingPointManager || employee.Role == EmployeeRole.Seller))
        {
            pointId = details.TradingPointId;
            if (string.IsNullOrWhiteSpace(pointId) ||
                !await _db.TradingPoints.Find(p => p.Id == pointId).AnyAsync())
                ModelState.AddModelError("Details.TradingPointId", "Обери існуючу торгову точку.");
        }
        if (!ModelState.IsValid)
            return View(new EmployeeEditPage
            {
                Id = employee.Id, Role = employee.Role.ToString(), IsActive = employee.IsActive,
                CanChoosePoint = Director && (employee.Role == EmployeeRole.TradingPointManager || employee.Role == EmployeeRole.Seller),
                TradingPoints = await PointsAsync(actor), Details = details
            });
        var login = details.Login.Trim().ToLowerInvariant();
        if (await _db.Employees.Find(e => e.Login == login && e.Id != id).AnyAsync())
        {
            ModelState.AddModelError("Details.Login", "Такий логін уже існує.");
            return View(new EmployeeEditPage
            {
                Id = employee.Id, Role = employee.Role.ToString(), IsActive = employee.IsActive,
                CanChoosePoint = Director && (employee.Role == EmployeeRole.TradingPointManager || employee.Role == EmployeeRole.Seller),
                TradingPoints = await PointsAsync(actor), Details = details
            });
        }
        try
        {
            await _db.Employees.UpdateOneAsync(e => e.Id == id,
                Builders<Employee>.Update.Set(e => e.FirstName, details.FirstName.Trim())
                    .Set(e => e.LastName, details.LastName.Trim())
                    .Set(e => e.Login, login)
                    .Set(e => e.TradingPointId, pointId));
        }
        catch (MongoWriteException ex) when (ex.WriteError?.Category == ServerErrorCategory.DuplicateKey)
        {
            ModelState.AddModelError("Details.Login", "Такий логін уже існує.");
            return View(new EmployeeEditPage
            {
                Id = employee.Id, Role = employee.Role.ToString(), IsActive = employee.IsActive,
                CanChoosePoint = Director && (employee.Role == EmployeeRole.TradingPointManager || employee.Role == EmployeeRole.Seller),
                TradingPoints = await PointsAsync(actor), Details = details
            });
        }
        TempData["Flash"] = "Дані працівника оновлено.";
        return RedirectToAction(nameof(Index));
    }

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> SetActive(string id, bool isActive)
    {
        var actor = await CurrentAsync();
        if (actor == null) return Unauthorized();
        var subject = await _db.Employees.Find(e => e.Id == id).FirstOrDefaultAsync();
        if (subject == null) return NotFound();
        if (!CanManage(actor, subject)) return Forbid();
        // Do not mutate role or point; update only active state, for existing account.
        await _db.Employees.UpdateOneAsync(e => e.Id == id,
            Builders<Employee>.Update.Set(e => e.IsActive, isActive));
        TempData["Flash"] = isActive ? "Обліковий запис активовано." : "Обліковий запис деактивовано.";
        return RedirectToAction(nameof(Index));
    }
}
