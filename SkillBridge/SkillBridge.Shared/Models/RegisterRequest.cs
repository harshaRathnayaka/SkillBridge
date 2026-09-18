using System.ComponentModel.DataAnnotations;

namespace SkillBridge.Shared.Models;

public class RegisterRequest
{
    [Required(ErrorMessage = "Enter your name")]
    public string DisplayName { get; set; } = string.Empty;

    [Required(ErrorMessage = "Enter your email")]
    [EmailAddress(ErrorMessage = "Enter a valid email address")]
    public string Email { get; set; } = string.Empty;

    [Required(ErrorMessage = "Enter a password")]
    [MinLength(8, ErrorMessage = "Password must be at least 8 characters")]
    public string Password { get; set; } = string.Empty;

    [Required(ErrorMessage = "Confirm your password")]
    [Compare(nameof(Password), ErrorMessage = "Passwords do not match")]
    public string ConfirmPassword { get; set; } = string.Empty;

    [Required(ErrorMessage = "Choose a role")]
    public Role? Role { get; set; }
}
