using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Identity;

namespace Endy.Models;

public static class AppRoles
{
    public const string SuperAdmin = "Super Admin";
    public const string Admin = "Admin";
    public const string User = "User";
    public const string SecurityUser = "Security User";
    public static readonly string[] All = [SuperAdmin, Admin, User, SecurityUser];
}

public sealed class ApplicationUser : IdentityUser
{
    [MaxLength(120)] public string FullName { get; set; } = string.Empty;
    public int? DepartmentId { get; set; }
    public Department? Department { get; set; }
    public bool IsActive { get; set; } = true;
}

public sealed class Department
{
    public int Id { get; set; }
    [Required, MaxLength(100)] public string Name { get; set; } = string.Empty;
    public bool IsActive { get; set; } = true;
}

public sealed class UserFormInput
{
    [Required, StringLength(120)] public string FullName { get; set; } = string.Empty;
    [Required, StringLength(50)] public string UserName { get; set; } = string.Empty;
    [EmailAddress] public string? Email { get; set; }
    [Required] public string Role { get; set; } = AppRoles.User;
    public int? DepartmentId { get; set; }
    [DataType(DataType.Password), StringLength(100, MinimumLength = 12)] public string? Password { get; set; }
}
