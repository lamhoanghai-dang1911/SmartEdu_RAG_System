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
        private readonly IUnitOfWork _unitOfWork;

        public AccountController(IAccountService accountService, IUnitOfWork unitOfWork)
        {
            _accountService = accountService;
            _unitOfWork = unitOfWork;
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

        [HttpGet]
        public IActionResult ChangePassword()
        {
            return View();
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> ChangePassword(SmartEdu.Shared.DTOs.ChangePasswordDto dto)
        {
            if (!ModelState.IsValid) return View(dto);

            var userIdClaim = User.FindFirst("UserId")?.Value;
            if (!int.TryParse(userIdClaim, out var userId))
            {
                return RedirectToAction("Login");
            }

            var user = await _unitOfWork.Users.GetByIdAsync(userId);
            if (user == null) return RedirectToAction("Login");

            // Verify old password
            if (!BCrypt.Net.BCrypt.Verify(dto.OldPassword ?? string.Empty, user.PasswordHash))
            {
                ModelState.AddModelError(string.Empty, "Mật khẩu cũ không đúng.");
                return View(dto);
            }

            if (string.IsNullOrWhiteSpace(dto.NewPassword) || dto.NewPassword.Length < 8)
            {
                ModelState.AddModelError("NewPassword", "Mật khẩu mới phải có ít nhất 8 ký tự.");
                return View(dto);
            }

            if (dto.NewPassword != dto.ConfirmPassword)
            {
                ModelState.AddModelError("ConfirmPassword", "Mật khẩu xác nhận không khớp.");
                return View(dto);
            }

            // Hash and save new password
            user.PasswordHash = BCrypt.Net.BCrypt.HashPassword(dto.NewPassword);
            user.RequirePasswordChange = false;
            _unitOfWork.Users.Update(user);
            await _unitOfWork.SaveChangesAsync();

            TempData["Success"] = "Đổi mật khẩu thành công.";
            return RedirectToAction("Index", "Home");
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