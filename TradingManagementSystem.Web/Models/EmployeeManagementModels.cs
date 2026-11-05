using System.ComponentModel.DataAnnotations;
using TradingManagementSystem.Domain.Entities;

namespace TradingManagementSystem.Web.Models;

public sealed class EmployeeManagementPage
{
    public List<Employee> Employees { get; set; } = new();
    public Dictionary<string, string> PointNames { get; set; } = new();
    public bool IsDirector { get; set; }
    public string? OwnPointId { get; set; }
}

public sealed class EmployeeEditInput
{
    [Required, StringLength(60)]
    public string FirstName { get; set; } = string.Empty;

    [Required, StringLength(60)]
    public string LastName { get; set; } = string.Empty;

    [Required, RegularExpression("^[a-zA-Z0-9._-]{3,32}$", ErrorMessage = "Логін: 3–32 латинські символи, цифри, . _ -")]
    public string Login { get; set; } = string.Empty;

    public string? TradingPointId { get; set; }
}

public sealed class EmployeeEditPage
{
    public string Id { get; set; } = string.Empty;
    public string Role { get; set; } = string.Empty;
    public bool IsActive { get; set; }
    public EmployeeEditInput Details { get; set; } = new();
    public List<TradingPoint> TradingPoints { get; set; } = new();
    public bool CanChoosePoint { get; set; }
}
