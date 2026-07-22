using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;
using KissakiSignup.Web.Options;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.Extensions.Options;

namespace KissakiSignup.Web.Pages.Admin;

[AllowAnonymous]
public class LoginModel(IOptions<TournamentOptions> tournamentOptions) : PageModel
{
    [BindProperty]
    public string Password { get; set; } = string.Empty;

    [BindProperty(SupportsGet = true)]
    public string? ReturnUrl { get; set; }

    public async Task<IActionResult> OnPostAsync()
    {
        var configuredPassword = tournamentOptions.Value.AdminPassword;
        var isValid = !string.IsNullOrEmpty(configuredPassword) &&
            CryptographicOperations.FixedTimeEquals(
                SHA256.HashData(Encoding.UTF8.GetBytes(Password)),
                SHA256.HashData(Encoding.UTF8.GetBytes(configuredPassword)));

        if (!isValid)
        {
            ModelState.AddModelError(string.Empty, "Das Passwort ist nicht korrekt.");
            return Page();
        }

        var identity = new ClaimsIdentity(
            [new Claim(ClaimTypes.Name, "Admin")],
            CookieAuthenticationDefaults.AuthenticationScheme);
        await HttpContext.SignInAsync(
            CookieAuthenticationDefaults.AuthenticationScheme,
            new ClaimsPrincipal(identity));

        return Url.IsLocalUrl(ReturnUrl) ? LocalRedirect(ReturnUrl) : LocalRedirect("/admin");
    }
}
