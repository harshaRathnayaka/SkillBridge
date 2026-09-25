using System.ComponentModel.DataAnnotations;

namespace SkillBridge.Shared.Models;

public class RegisterRequest : IValidatableObject
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

    // One account, several paths — pick as many as apply. [Required] doesn't fit a List<T>
    // (an empty list still satisfies it), hence the explicit IValidatableObject check below.
    public List<Role> Roles { get; set; } = [];

    public IEnumerable<ValidationResult> Validate(ValidationContext validationContext)
    {
        if (Roles.Count == 0)
        {
            yield return new ValidationResult("Choose at least one role", [nameof(Roles)]);
        }
    }
}
