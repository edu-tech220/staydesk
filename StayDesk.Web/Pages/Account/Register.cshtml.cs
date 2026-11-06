using System.ComponentModel.DataAnnotations;
using System.Security.Claims;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using StayDesk.Web.ApiClient;

namespace StayDesk.Web.Pages.Account;

public class RegisterModel : PageModel
{
    private readonly StayDeskApiClient _api;

    public RegisterModel(StayDeskApiClient api)
    {
        _api = api;
    }

    [BindProperty]
    [Required(ErrorMessage = "El nombre es obligatorio.")]
    [MaxLength(200)]
    public string FullName { get; set; } = string.Empty;

    [BindProperty]
    [Required(ErrorMessage = "El correo es obligatorio.")]
    [EmailAddress]
    public string Email { get; set; } = string.Empty;

    [BindProperty]
    [Required(ErrorMessage = "La contrasena es obligatoria.")]
    [MinLength(8, ErrorMessage = "La contrasena debe tener al menos 8 caracteres.")]
    [DataType(DataType.Password)]
    public string Password { get; set; } = string.Empty;

    [BindProperty]
    [Compare(nameof(Password), ErrorMessage = "Las contrasenas no coinciden.")]
    [DataType(DataType.Password)]
    public string ConfirmPassword { get; set; } = string.Empty;

    public string? ErrorMessage { get; private set; }

    public void OnGet()
    {
    }

    public async Task<IActionResult> OnPostAsync(CancellationToken ct)
    {
        if (!ModelState.IsValid)
        {
            return Page();
        }

        var result = await _api.RegisterAsync(FullName, Email, Password, ct);
        if (!result.IsSuccess)
        {
            ErrorMessage = result.ErrorMessage;
            return Page();
        }

        var auth = result.Data!;
        var claims = new List<Claim>
        {
            new(ClaimTypes.Name, auth.FullName),
            new(ClaimTypes.Email, auth.Email),
            new(ClaimTypes.Role, auth.Role),
            new(StayDeskApiClient.AccessTokenClaimType, auth.Token)
        };

        var identity = new ClaimsIdentity(claims, CookieAuthenticationDefaults.AuthenticationScheme);
        await HttpContext.SignInAsync(CookieAuthenticationDefaults.AuthenticationScheme, new ClaimsPrincipal(identity));

        return RedirectToPage("/Index");
    }
}
