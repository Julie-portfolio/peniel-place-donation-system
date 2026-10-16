using System.ComponentModel.DataAnnotations;

namespace PenielPlaceMinistriesCSharp.ViewModels;

public class LoginViewModel
{
    [Required(ErrorMessage = "Enter your email address.")]
    [EmailAddress(ErrorMessage = "Enter a valid email address, like name@example.com.")]
    [StringLength(254)]
    public string Email { get; set; } = "";

    [Required(ErrorMessage = "Enter your password.")]
    [DataType(DataType.Password)]
    [StringLength(128, ErrorMessage = "That password is too long.")]
    public string Password { get; set; } = "";

    public bool RememberMe { get; set; }
    public string? ReturnUrl { get; set; }
}

public class TwoFactorViewModel
{
    [Required(ErrorMessage = "Enter the 6-digit code.")]
    [RegularExpression(@"^\d{6}$", ErrorMessage = "The code is 6 numbers with no spaces or letters.")]
    public string Code { get; set; } = "";

    public string? ReturnUrl { get; set; }
}

public class AuthenticatorSetupViewModel
{
    [Required(ErrorMessage = "Enter the current 6-digit code from your authenticator app.")]
    [RegularExpression(@"^\d{6}$", ErrorMessage = "The code is 6 numbers with no spaces or letters.")]
    public string Code { get; set; } = "";

    // Display-only values. The POST action always reads the secret from UserManager,
    // never from the browser.
    public string SharedKey { get; set; } = "";
    public string AuthenticatorUri { get; set; } = "";
}

public class CreateVolunteerViewModel
{
    [Required(ErrorMessage = "Enter the volunteer's email address."), EmailAddress, StringLength(254)]
    public string Email { get; set; } = "";

    [Required(ErrorMessage = "Enter the volunteer's name."), StringLength(100)]
    [Display(Name = "Name")]
    public string DisplayName { get; set; } = "";

    [Required(ErrorMessage = "Enter a temporary password."), DataType(DataType.Password)]
    [StringLength(128, MinimumLength = 12, ErrorMessage = "The password must be at least 12 characters.")]
    [Display(Name = "Temporary password")]
    public string TemporaryPassword { get; set; } = "";
}
