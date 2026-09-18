using System.ComponentModel.DataAnnotations;

namespace SkillBridge.Shared.Models;

public class ResetPasswordRequest
{
    [Required]
    public string Email { get; set; } = string.Empty;

    [Required(ErrorMessage = "Enter the reset code from your email")]
    public string Token { get; set; } = string.Empty;

    [Required(ErrorMessage = "Enter a new password")]
    [MinLength(8, ErrorMessage = "Password must be at least 8 characters")]
    public string NewPassword { get; set; } = string.Empty;

    [Required(ErrorMessage = "Confirm your new password")]
    [Compare(nameof(NewPassword), ErrorMessage = "Passwords do not match")]
    public string ConfirmPassword { get; set; } = string.Empty;
}
