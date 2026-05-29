using SmartEdu.Shared.Entities;
using SmartEdu.Shared.Enums;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using BCrypt.Net;

namespace SmartEdu.Data
{
    public static class DataSeeder
    {
        public static void Seed(AppDbContext context)
        {
            // Kiểm tra nếu đã có dữ liệu thì không tạo nữa
            if (context.Users.Any()) return;

            var users = new List<User>
        {
            new User
            {
                Username = "admin",
                FullName = "Quản trị viên",
                Role = UserRole.Admin,
                PasswordHash = BCrypt.Net.BCrypt.HashPassword("123")
            },
            new User
            {
                Username = "lecturer",
                FullName = "Giảng viên mẫu",
                Role = UserRole.Lecturer,
                PasswordHash = BCrypt.Net.BCrypt.HashPassword("123")
            },
            new User
            {
                Username = "student",
                FullName = "Sinh viên mẫu",
                Role = UserRole.Student,
                PasswordHash = BCrypt.Net.BCrypt.HashPassword("123")
            }
        };

            context.Users.AddRange(users);
            context.SaveChanges();
        }
    }
}
