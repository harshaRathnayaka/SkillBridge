using System.ComponentModel.DataAnnotations;

namespace SkillBridge.Shared.Models;

public class ForgotPasswordRequest
{
    [Required(ErrorMessage = "Enter your email")]
    [EmailAddress(ErrorMessage = "Enter a valid email address")]
    public string Email { get; set; } = string.Empty;
}
