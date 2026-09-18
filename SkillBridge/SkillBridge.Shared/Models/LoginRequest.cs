using System.ComponentModel.DataAnnotations;

namespace SkillBridge.Shared.Models;

public class LoginRequest
{
    [Required(ErrorMessage = "Enter your email")]
    [EmailAddress(ErrorMessage = "Enter a valid email address")]
    public string Email { get; set; } = string.Empty;

    [Required(ErrorMessage = "Enter password")]
    public string Password { get; set; } = string.Empty;

    public bool RememberMe { get; set; }
}
