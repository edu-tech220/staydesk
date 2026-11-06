using System.ComponentModel.DataAnnotations;
using System.Security.Claims;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using StayDesk.Web.ApiClient;

namespace StayDesk.Web.Pages.Account;

public class LoginModel : PageModel
{
    private readonly StayDeskApiClient _api;

    public LoginModel(StayDeskApiClient api)
    {
        _api = api;
    }

    [BindProperty]
    [Required(ErrorMessage = "El correo es obligatorio.")]
    [EmailAddress]
    public string Email { get; set; } = string.Empty;

    [BindProperty]
    [Required(ErrorMessage = "La contrasena es obligatoria.")]
    [DataType(DataType.Password)]
    public string Password { get; set; } = string.Empty;

    public string? ReturnUrl { get; set; }

    public string? ErrorMessage { get; private set; }

    public void OnGet(string? returnUrl)
    {
        ReturnUrl = returnUrl;
    }

    public async Task<IActionResult> OnPostAsync(string? returnUrl, CancellationToken ct)
    {
        ReturnUrl = returnUrl;

        if (!ModelState.IsValid)
        {
            return Page();
        }

        var result = await _api.LoginAsync(Email, Password, ct);
        if (!result.IsSuccess)
        {
            ErrorMessage = result.ErrorMessage;
            return Page();
        }

        await SignInAsync(result.Data!);
        return LocalRedirect(string.IsNullOrEmpty(returnUrl) ? "/" : returnUrl);
    }

    private async Task SignInAsync(AuthResponse auth)
    {
        var claims = new List<Claim>
        {
            new(ClaimTypes.Name, auth.FullName),
            new(ClaimTypes.Email, auth.Email),
            new(ClaimTypes.Role, auth.Role),
            new(StayDeskApiClient.AccessTokenClaimType, auth.Token)
        };

        var identity = new ClaimsIdentity(claims, CookieAuthenticationDefaults.AuthenticationScheme);
        await HttpContext.SignInAsync(CookieAuthenticationDefaults.AuthenticationScheme, new ClaimsPrincipal(identity));
    }
}
