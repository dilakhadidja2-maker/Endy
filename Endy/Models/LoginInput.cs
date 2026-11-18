using System.ComponentModel.DataAnnotations;

namespace Endy.Models;

public sealed class LoginInput
{
    [Required] public string UserName { get; set; } = string.Empty;
    [Required, DataType(DataType.Password)] public string Password { get; set; } = string.Empty;
    public bool RememberMe { get; set; }
}
