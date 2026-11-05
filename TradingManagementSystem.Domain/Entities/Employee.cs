using TradingManagementSystem.Domain.Enums;

namespace TradingManagementSystem.Domain.Entities;

public class Employee
{
    public string Id { get; set; } = string.Empty;
    public string FirstName { get; set; } = string.Empty;
    public string LastName { get; set; } = string.Empty;
    public string Login { get; set; } = string.Empty;
    public string PasswordHash { get; private set; } = string.Empty;
    public EmployeeRole Role { get; set; }
    // Unassigned for director/supply manager; required for trading-point manager/seller.
    public string? TradingPointId { get; set; }
    public bool IsActive { get; set; } = true;

    public void ChangePassword(string newPasswordHash)
    {
        if (string.IsNullOrWhiteSpace(newPasswordHash))
            throw new ArgumentException("Password hash cannot be empty.");
        PasswordHash = newPasswordHash;
    }
}
