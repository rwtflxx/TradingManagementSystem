
using System.ComponentModel.DataAnnotations;

namespace TradingManagementSystem.Web.Models.Api;

public class CreateSupplierRequest
{
    [Required]
    public string Name { get; set; } = string.Empty;

    public string Phone { get; set; } = string.Empty;

    [EmailAddress]
    public string Email { get; set; } = string.Empty;

    public string Address { get; set; } = string.Empty;
}

public class UpdateSupplierRequest
{
    [Required]
    public string Name { get; set; } = string.Empty;

    public string Phone { get; set; } = string.Empty;

    [EmailAddress]
    public string Email { get; set; } = string.Empty;

    public string Address { get; set; } = string.Empty;
}
