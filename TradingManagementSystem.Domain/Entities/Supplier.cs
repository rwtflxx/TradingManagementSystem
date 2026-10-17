
using System;
using System.Net.Mail;

namespace TradingManagementSystem.Domain.Entities;

public class Supplier
{
    public string Id { get; set; } = string.Empty;

    public string Name { get; set; } = string.Empty;

    public string Phone { get; set; } = string.Empty;

    public string Email { get; set; } = string.Empty;

    public string Address { get; set; } = string.Empty;

    public void UpdateData(
        string name,
        string phone,
        string email,
        string address)
    {
        if (string.IsNullOrWhiteSpace(name))
        {
            throw new ArgumentException(
                "Supplier name cannot be empty.");
        }

        if (!string.IsNullOrWhiteSpace(email) &&
            !MailAddress.TryCreate(email, out _))
        {
            throw new ArgumentException(
                "Supplier email is invalid.");
        }

        Name = name.Trim();
        Phone = phone?.Trim() ?? string.Empty;
        Email = email?.Trim() ?? string.Empty;
        Address = address?.Trim() ?? string.Empty;
    }
}
