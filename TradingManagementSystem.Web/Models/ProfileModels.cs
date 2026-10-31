using System.ComponentModel.DataAnnotations;

namespace TradingManagementSystem.Web.Models;

public class ProfileDetailsInput
{
    [Required(ErrorMessage = "Введи ім'я"), StringLength(60, ErrorMessage = "Максимум 60 символів")]
    public string FirstName { get; set; } = string.Empty;

    [Required(ErrorMessage = "Введи прізвище"), StringLength(60, ErrorMessage = "Максимум 60 символів")]
    public string LastName { get; set; } = string.Empty;

    [Required(ErrorMessage = "Введи логін")]
    [RegularExpression("^[a-zA-Z0-9._-]{3,32}$", ErrorMessage = "Логін: 3–32 символи латиницею, цифри, крапка, _ або -")]
    public string Login { get; set; } = string.Empty;
}

public class ProfilePasswordInput
{
    [Required(ErrorMessage = "Введи поточний пароль"), DataType(DataType.Password)]
    public string CurrentPassword { get; set; } = string.Empty;

    [Required(ErrorMessage = "Введи новий пароль"), MinLength(10, ErrorMessage = "Мінімум 10 символів")]
    [StringLength(128), DataType(DataType.Password)]
    public string NewPassword { get; set; } = string.Empty;

    [Required(ErrorMessage = "Підтвердь новий пароль"), DataType(DataType.Password)]
    [Compare(nameof(NewPassword), ErrorMessage = "Нові паролі не збігаються")]
    public string ConfirmPassword { get; set; } = string.Empty;
}

public class ProfilePageViewModel
{
    public string Id { get; set; } = string.Empty;
    public string Role { get; set; } = string.Empty;
    public bool IsActive { get; set; }
    public ProfileDetailsInput Details { get; set; } = new();
    public ProfilePasswordInput Password { get; set; } = new();
}
