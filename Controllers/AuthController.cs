using System.Security.Claims;
using EMarket.Models;
using EMarket.Services;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace EMarket.Controllers;

public class AuthController : Controller
{
    private readonly AccountService _accounts;

    public AuthController(AccountService accounts)
    {
        _accounts = accounts;
    }

    [HttpGet]
    public IActionResult Login(string? returnUrl = null)
    {
        if (User.Identity?.IsAuthenticated == true)
        {
            return RedirectToRoleHome();
        }

        return View(new LoginViewModel { ReturnUrl = returnUrl });
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Login(LoginViewModel model)
    {
        if (!ModelState.IsValid)
        {
            return View(model);
        }

        var account = await _accounts.SignInAsync(model.UserName, model.Password);
        if (account is null)
        {
            ModelState.AddModelError(string.Empty, "Tên đăng nhập hoặc mật khẩu không chính xác.");
            return View(model);
        }

        var claims = new List<Claim>
        {
            new(ClaimTypes.NameIdentifier, account.UserName),
            new(ClaimTypes.Name, account.DisplayName),
            new(ClaimTypes.Role, account.Role)
        };
        if (account.CustomerId is not null)
        {
            claims.Add(new Claim("CustomerId", account.CustomerId));
        }

        var identity = new ClaimsIdentity(claims, CookieAuthenticationDefaults.AuthenticationScheme);
        await HttpContext.SignInAsync(
            CookieAuthenticationDefaults.AuthenticationScheme,
            new ClaimsPrincipal(identity));

        if (!string.IsNullOrWhiteSpace(model.ReturnUrl) && Url.IsLocalUrl(model.ReturnUrl))
        {
            return Redirect(model.ReturnUrl);
        }

        return RedirectToRoleHome(account.Role);
    }

    [HttpGet]
    public IActionResult Register()
    {
        if (User.Identity?.IsAuthenticated == true)
        {
            return RedirectToRoleHome();
        }

        return View(new RegisterViewModel());
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Register(RegisterViewModel model)
    {
        if (User.Identity?.IsAuthenticated == true)
        {
            return RedirectToRoleHome();
        }

        if (!ModelState.IsValid)
        {
            return View(model);
        }

        if (!await _accounts.RegisterCustomerAsync(model))
        {
            ModelState.AddModelError(nameof(model.UserName), "Tên đăng nhập đã được sử dụng.");
            return View(model);
        }

        TempData["Success"] = "Đăng ký thành công. Hãy đăng nhập bằng tài khoản vừa tạo.";
        return RedirectToAction(nameof(Login));
    }

    [Authorize]
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Logout()
    {
        await HttpContext.SignOutAsync(CookieAuthenticationDefaults.AuthenticationScheme);
        return RedirectToAction("Index", "Home");
    }

    [HttpGet]
    public IActionResult Denied()
    {
        return View();
    }

    private IActionResult RedirectToRoleHome(string? role = null)
    {
        var isAdmin = role is null ? User.IsInRole("Admin") : role == "Admin";
        return isAdmin
            ? RedirectToAction("Index", "Home")
            : RedirectToAction("Index", "Home");
    }
}
