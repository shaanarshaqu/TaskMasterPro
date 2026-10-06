using System.Security.Claims;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using TodoMVC.Data;
using TodoMVC.Models;
using TodoMVC.Models.ViewModels;
using TodoMVC.Services;

namespace TodoMVC.Controllers;

public class AccountController : Controller
{
    private readonly TodoDbContext _dbContext;
    private readonly ILogger<AccountController> _logger;

    public AccountController(TodoDbContext dbContext, ILogger<AccountController> logger)
    {
        _dbContext = dbContext;
        _logger = logger;
    }

    [HttpGet]
    public IActionResult Login(string? returnUrl = null)
    {
        if (User.Identity?.IsAuthenticated == true)
        {
            return RedirectToAction("Index", "Todos");
        }

        ViewData["ReturnUrl"] = returnUrl;
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

        var normalizedUsername = model.Username.Trim();
        var user = await _dbContext.Users
            .Include(u => u.Credential)
            .FirstOrDefaultAsync(u => u.Username == normalizedUsername || u.Email == normalizedUsername);

        if (user == null || user.Credential == null)
        {
            ModelState.AddModelError(string.Empty, "Invalid username or password.");
            return View(model);
        }

        if (!user.IsActive)
        {
            ModelState.AddModelError(string.Empty, "This account has been deactivated. Please contact support.");
            return View(model);
        }

        var isValidPassword = PasswordHasher.VerifyPassword(model.Password, user.Credential.PasswordHash, user.Credential.PasswordSalt);
        if (!isValidPassword)
        {
            ModelState.AddModelError(string.Empty, "Invalid username or password.");
            return View(model);
        }

        // Update last login
        user.Credential.LastLoginAt = DateTime.UtcNow;
        user.Credential.UpdatedAt = DateTime.UtcNow;
        await _dbContext.SaveChangesAsync();

        // Create claims identity
        var claims = new List<Claim>
        {
            new(ClaimTypes.NameIdentifier, user.Id.ToString()),
            new(ClaimTypes.Name, user.Username),
            new(ClaimTypes.GivenName, user.FullName),
            new(ClaimTypes.Email, user.Email),
            new(ClaimTypes.Role, string.IsNullOrWhiteSpace(user.Role) ? "user" : user.Role.ToLowerInvariant())
        };

        var identity = new ClaimsIdentity(claims, CookieAuthenticationDefaults.AuthenticationScheme);
        var principal = new ClaimsPrincipal(identity);

        var authProperties = new AuthenticationProperties
        {
            IsPersistent = model.RememberMe,
            ExpiresUtc = model.RememberMe ? DateTimeOffset.UtcNow.AddDays(14) : DateTimeOffset.UtcNow.AddHours(8)
        };

        await HttpContext.SignInAsync(CookieAuthenticationDefaults.AuthenticationScheme, principal, authProperties);

        _logger.LogInformation("User {Username} (Role: {Role}) logged in successfully.", user.Username, user.Role);

        if (!string.IsNullOrEmpty(model.ReturnUrl) && Url.IsLocalUrl(model.ReturnUrl))
        {
            return Redirect(model.ReturnUrl);
        }

        // Default landing based on role
        if (user.Role.Equals("admin", StringComparison.OrdinalIgnoreCase))
        {
            return RedirectToAction("Reports", "Admin");
        }

        return RedirectToAction("Index", "Todos");
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Register(RegisterViewModel model)
    {
        if (!ModelState.IsValid)
        {
            // Pass model back to login view with register tab active
            ViewBag.ActiveTab = "register";
            return View("Login", new LoginViewModel());
        }

        var cleanUsername = model.Username.Trim();
        var cleanEmail = model.Email.Trim().ToLowerInvariant();

        // Validate uniqueness
        if (await _dbContext.Users.AnyAsync(u => u.Username.ToLower() == cleanUsername.ToLower()))
        {
            ModelState.AddModelError("RegisterUsername", "This username is already taken. Please choose another.");
            ViewBag.ActiveTab = "register";
            ViewBag.RegisterModel = model;
            return View("Login", new LoginViewModel());
        }

        if (await _dbContext.Users.AnyAsync(u => u.Email.ToLower() == cleanEmail))
        {
            ModelState.AddModelError("RegisterEmail", "An account with this email address already exists.");
            ViewBag.ActiveTab = "register";
            ViewBag.RegisterModel = model;
            return View("Login", new LoginViewModel());
        }

        // User role is strictly defaulted to "user" per requirements
        var newUser = new User
        {
            Username = cleanUsername,
            FullName = model.FullName.Trim(),
            Email = cleanEmail,
            Role = "user", // Default signup creates users with role "user"
            IsActive = true,
            CreatedAt = DateTime.UtcNow
        };

        var hash = PasswordHasher.HashPassword(model.Password, out var salt);

        var credential = new UserCredential
        {
            User = newUser,
            PasswordHash = hash,
            PasswordSalt = salt,
            UpdatedAt = DateTime.UtcNow
        };

        newUser.Credential = credential;

        _dbContext.Users.Add(newUser);
        await _dbContext.SaveChangesAsync();

        _logger.LogInformation("New user registered: {Username} (Role: user)", newUser.Username);

        // Sign the newly created user in automatically
        var claims = new List<Claim>
        {
            new(ClaimTypes.NameIdentifier, newUser.Id.ToString()),
            new(ClaimTypes.Name, newUser.Username),
            new(ClaimTypes.GivenName, newUser.FullName),
            new(ClaimTypes.Email, newUser.Email),
            new(ClaimTypes.Role, "user")
        };

        var identity = new ClaimsIdentity(claims, CookieAuthenticationDefaults.AuthenticationScheme);
        var principal = new ClaimsPrincipal(identity);

        await HttpContext.SignInAsync(CookieAuthenticationDefaults.AuthenticationScheme, principal);

        TempData["SuccessMessage"] = "Account created successfully! Welcome to your Todo Workspace.";
        return RedirectToAction("Index", "Todos");
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Logout()
    {
        await HttpContext.SignOutAsync(CookieAuthenticationDefaults.AuthenticationScheme);
        return RedirectToAction("Login", "Account");
    }

    [HttpGet]
    public IActionResult AccessDenied()
    {
        return View();
    }
}
