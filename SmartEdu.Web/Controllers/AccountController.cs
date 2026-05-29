using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using SmartEdu.Business.Interfaces;
using SmartEdu.Shared.DTOs;
using System.Security.Claims;

namespace SmartEdu.Web.Controllers
{
    public class AccountController : Controller
    {
        private readonly IAccountService _accountService;

        public AccountController(IAccountService accountService)
        {
            _accountService = accountService;
        }

        [HttpGet]
        public IActionResult Login() => View();

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Login(LoginViewModel model)
        {
            if (!ModelState.IsValid) return View(model);
            var user = await _accountService.AuthenticateAsync(model.Username, model.Password);

            if (user != null)
            {
                var claims = new List<Claim>
                {
                    new Claim(ClaimTypes.Name, user.Username),
                    new Claim(ClaimTypes.Role, user.Role.ToString()),
                    new Claim("UserId", user.Id.ToString())
                };

                var claimsIdentity = new ClaimsIdentity(claims, CookieAuthenticationDefaults.AuthenticationScheme);
                await HttpContext.SignInAsync(CookieAuthenticationDefaults.AuthenticationScheme, new ClaimsPrincipal(claimsIdentity));

                return user.Role switch
                {
                    SmartEdu.Shared.Enums.UserRole.Lecturer => RedirectToAction("Index", "Document"),
                    SmartEdu.Shared.Enums.UserRole.Student => RedirectToAction("Index", "Chat"),
                    SmartEdu.Shared.Enums.UserRole.Admin => RedirectToAction("ManageUsers", "Account"),
                    _ => RedirectToAction("Index", "Home")
                };
            }
            ModelState.AddModelError("", "❌ Sai tên đăng nhập hoặc mật khẩu.");
            return View(model);
        }

        public async Task<IActionResult> Logout()
        {
            await HttpContext.SignOutAsync(CookieAuthenticationDefaults.AuthenticationScheme);
            return RedirectToAction("Login");
        }

        public IActionResult AccessDenied()
        {
            var userRole = User.FindFirst(ClaimTypes.Role)?.Value;
            ViewBag.Message = $"Xin lỗi, tài khoản với quyền '{userRole}' không được phép truy cập trang này.";
            return View();
        }

        [Authorize(Roles = "Admin")]
        public async Task<IActionResult> ManageUsers()
            => View(await _accountService.GetAllUsersAsync());

        [Authorize(Roles = "Admin")]
        public IActionResult CreateUser() => View();

        [HttpPost]
        [Authorize(Roles = "Admin")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> CreateUser(UserCreateDto dto)
        {
            if (!ModelState.IsValid) return View(dto);

            try
            {
                await _accountService.CreateUserAsync(dto);
                TempData["Success"] = "Tạo tài khoản thành công!";
                return RedirectToAction(nameof(ManageUsers));
            }
            catch (InvalidOperationException ex)
            {
                ModelState.AddModelError("Username", ex.Message);
                return View(dto);
            }
        }

        [HttpPost]
        [Authorize(Roles = "Admin")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> DeleteUser(int id)
        {
            await _accountService.DeleteUserAsync(id);
            TempData["Success"] = "Đã xóa tài khoản thành công!";
            return RedirectToAction(nameof(ManageUsers));
        }
    }
}