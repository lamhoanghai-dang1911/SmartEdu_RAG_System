using SmartEdu.Shared.Enums;

namespace SmartEdu.Shared.DTOs
{
    public class UserDto
    {
        public int Id { get; set; }
        public string Username { get; set; }
        public string FullName { get; set; }
        public UserRole Role { get; set; }
        public string Email { get; set; } = string.Empty;
        public string? StudentCode { get; set; }
    }
}
