using SmartEdu.Shared.Enums;

namespace SmartEdu.Shared.Entities
{
    // Nếu bạn có lớp BaseEntity, hãy kế thừa: public class User : BaseEntity
    public class User
    {
        public int Id { get; set; }
        public string Username { get; set; }
        public string PasswordHash { get; set; }
        public string FullName { get; set; }
        public UserRole Role { get; set; }
        public bool IsDeleted { get; set; } = false;
    }
}