using System.ComponentModel.DataAnnotations;

namespace PhilSLA.ExamPlatform.Candidate.Components.Pages;

public sealed class LoginFormModel
{
    [Required(ErrorMessage = "Email address is required.")]
    [EmailAddress(ErrorMessage = "Enter a valid email address.")]
    public string Email { get; set; } = string.Empty;

    [Required(ErrorMessage = "Password is required.")]
    public string Password { get; set; } = string.Empty;
}
