using SmartEdu.Business.Interfaces;
using SmartEdu.Data.Repositories;
using SmartEdu.Shared.DTOs;
using SmartEdu.Shared.Entities;

namespace SmartEdu.Business.Services
{
    public class AccountService : IAccountService
    {
        private readonly IRepository<User> _userRepo;

        public AccountService(IRepository<User> userRepo)
        {
            _userRepo = userRepo;
        }

        public async Task<UserDto?> AuthenticateAsync(string username, string password)
        {
            var users = await _userRepo.GetAllAsync(u => u.Username == username && !u.IsDeleted);
            var user = users.FirstOrDefault();

            if (user != null && BCrypt.Net.BCrypt.Verify(password, user.PasswordHash))
            {
                return new UserDto
                {
                    Id = user.Id,
                    Username = user.Username,
                    FullName = user.FullName,
                    Role = user.Role
                };
            }
            return null;
        }

        public async Task<IEnumerable<UserDto>> GetAllUsersAsync()
        {
            var users = await _userRepo.GetAllAsync(u => !u.IsDeleted);
            return users.Select(u => new UserDto
            {
                Id = u.Id,
                Username = u.Username,
                FullName = u.FullName,
                Role = u.Role
            });
        }

        public async Task<UserDto?> GetUserByIdAsync(int id)
        {
            var user = await _userRepo.GetByIdAsync(id);
            if (user == null || user.IsDeleted) return null;
            return new UserDto
            {
                Id = user.Id,
                Username = user.Username,
                FullName = user.FullName,
                Role = user.Role
            };
        }

        public async Task CreateUserAsync(UserCreateDto dto)
        {
            if (await IsUsernameTakenAsync(dto.Username))
                throw new InvalidOperationException("Tên đăng nhập này đã tồn tại.");

            var user = new User
            {
                Username = dto.Username,
                FullName = dto.FullName,
                Role = dto.Role,
                PasswordHash = BCrypt.Net.BCrypt.HashPassword(dto.Password),
                IsDeleted = false
            };

            await _userRepo.AddAsync(user);
            await _userRepo.SaveChangesAsync();
        }

        public async Task UpdateUserAsync(UserDto dto)
        {
            var user = await _userRepo.GetByIdAsync(dto.Id);
            if (user == null || user.IsDeleted)
                throw new InvalidOperationException("Không tìm thấy tài khoản.");

            user.FullName = dto.FullName;
            user.Role = dto.Role;

            _userRepo.Update(user);
            await _userRepo.SaveChangesAsync();
        }

        public async Task DeleteUserAsync(int id)
        {
            var user = await _userRepo.GetByIdAsync(id);
            if (user != null && !user.IsDeleted)
            {
                user.IsDeleted = true;
                _userRepo.Update(user);
                await _userRepo.SaveChangesAsync();
            }
        }

        public async Task<bool> IsUsernameTakenAsync(string username)
        {
            var users = await _userRepo.GetAllAsync(u => u.Username == username && !u.IsDeleted);
            return users.Any();
        }
    }
}