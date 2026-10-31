using System.ComponentModel.DataAnnotations;

namespace TradingManagementSystem.Web.Models;

public class LoginInput
{
    [Required(ErrorMessage = "Введи логін")]
    public string Login { get; set; } = string.Empty;

    [Required(ErrorMessage = "Введи пароль")]
    [DataType(DataType.Password)]
    public string Password { get; set; } = string.Empty;
}

public class EmployeeInput
{
    [Required, StringLength(60)]
    public string FirstName { get; set; } = string.Empty;

    [Required, StringLength(60)]
    public string LastName { get; set; } = string.Empty;

    [Required, RegularExpression("^[a-zA-Z0-9._-]{3,32}$", ErrorMessage = "Логін: 3–32 символи, латинські букви, цифри, крапка, _ або -")]
    public string Login { get; set; } = string.Empty;

    [Required, MinLength(10, ErrorMessage = "Пароль повинен містити щонайменше 10 символів")]
    [StringLength(128)]
    [DataType(DataType.Password)]
    public string Password { get; set; } = string.Empty;

    [Required]
    public TradingManagementSystem.Domain.Enums.EmployeeRole Role { get; set; }
}

public class PasswordChangeInput
{
    [Required, DataType(DataType.Password)]
    public string CurrentPassword { get; set; } = string.Empty;

    [Required, MinLength(10), StringLength(128), DataType(DataType.Password)]
    public string NewPassword { get; set; } = string.Empty;
}
