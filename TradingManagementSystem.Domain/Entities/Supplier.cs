using System;
using System.Collections.Generic;
using System.Text;

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
        Name = name;
        Phone = phone;
        Email = email;
        Address = address;
    }
}