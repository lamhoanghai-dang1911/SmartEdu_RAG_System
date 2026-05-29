using Microsoft.EntityFrameworkCore;
using SmartEdu.Business.Interfaces;
using SmartEdu.Data.Repositories;
using SmartEdu.Shared.DTOs;
using SmartEdu.Shared.Entities;
using SmartEdu.Shared.Enums;

namespace SmartEdu.Business.Services
{
    public class SubjectService : ISubjectService
    {
        private readonly IRepository<Subject> _repo;
        private readonly IRepository<StudentSubject> _studentSubjectRepo;
        private readonly IRepository<User> _userRepo;

        public SubjectService(IRepository<Subject> repo, IRepository<StudentSubject> studentSubjectRepo, IRepository<User> userRepo)
        {
            _repo = repo;
            _studentSubjectRepo = studentSubjectRepo;
            _userRepo = userRepo;
        }

        public async Task<IEnumerable<SubjectDto>> GetAllAsync()
        {
            var all = await _repo.GetAllAsync();
            return all.Where(s => !s.IsDeleted).Select(s => new SubjectDto
            {
                Id = s.Id,
                Name = s.Name,
                Description = s.Description,
                CreatedAt = s.CreatedAt
            });
        }

        public async Task<SubjectDto?> GetByIdAsync(int id)
        {
            var subject = await _repo.GetByIdAsync(id);
            if (subject == null || subject.IsDeleted) return null;

            return new SubjectDto
            {
                Id = subject.Id,
                Name = subject.Name,
                Description = subject.Description,
                CreatedAt = subject.CreatedAt
            };
        }

        public async Task CreateAsync(SubjectCreateDto dto)
        {
            var subject = new Subject
            {
                Name = dto.Name,
                Description = dto.Description,
                CreatedAt = DateTime.UtcNow,
                IsDeleted = false
            };

            await _repo.AddAsync(subject);
            await _repo.SaveChangesAsync();
        }

        public async Task UpdateAsync(SubjectUpdateDto dto)
        {
            var existingSubject = await _repo.GetByIdAsync(dto.Id);
            if (existingSubject == null || existingSubject.IsDeleted)
                throw new InvalidOperationException("Không tìm thấy môn học");

            existingSubject.Name = dto.Name;
            existingSubject.Description = dto.Description;
            existingSubject.UpdatedAt = DateTime.UtcNow;

            _repo.Update(existingSubject);
            await _repo.SaveChangesAsync();
        }

        public async Task DeleteAsync(int id)
        {
            var subject = await _repo.GetByIdAsync(id);
            if (subject is null) return;

            subject.IsDeleted = true;
            subject.UpdatedAt = DateTime.UtcNow;

            _repo.Update(subject);
            await _repo.SaveChangesAsync();
        }

        public async Task<IEnumerable<SubjectDto>> GetSubjectsByUserIdAsync(int userId)
        {
            var enrollments = await _studentSubjectRepo.GetAllWithIncludeAsync(
                ss => ss.StudentId == userId && !ss.IsDeleted,
                ss => ss.Subject);

            return enrollments.Select(ss => ss.Subject)
                              .Where(s => s != null && !s.IsDeleted)
                              .Distinct()
                              .Select(s => new SubjectDto
                              {
                                  Id = s.Id,
                                  Name = s.Name,
                                  Description = s.Description,
                                  CreatedAt = s.CreatedAt,
                              });
        }

        public async Task AssignStudentToSubject(int studentId, int subjectId)
        {
            var existing = await _studentSubjectRepo.GetAllAsync();
            var item = existing.FirstOrDefault(ss => ss.StudentId == studentId && ss.SubjectId == subjectId);

            if (item == null)
            {
                await _studentSubjectRepo.AddAsync(new StudentSubject { StudentId = studentId, SubjectId = subjectId });
            }
            else if (item.IsDeleted)
            {
                item.IsDeleted = false;
                _studentSubjectRepo.Update(item);
            }
            await _studentSubjectRepo.SaveChangesAsync();
        }

        public async Task RemoveStudentFromSubject(int studentId, int subjectId)
        {
            var enrollments = await _studentSubjectRepo.GetAllAsync();
            var item = enrollments.FirstOrDefault(ss => ss.StudentId == studentId && ss.SubjectId == subjectId && !ss.IsDeleted);

            if (item != null)
            {
                item.IsDeleted = true;
                _studentSubjectRepo.Update(item);
                await _studentSubjectRepo.SaveChangesAsync();
            }
        }

        public async Task<(IEnumerable<UserDto> Enrolled, IEnumerable<UserDto> NotEnrolled)> GetStudentEnrollmentStatus(int subjectId)
        {
            var allStudents = await _userRepo.GetAllAsync(u => u.Role == UserRole.Student && !u.IsDeleted);

            var enrollments = await _studentSubjectRepo.GetAllWithIncludeAsync(
                ss => ss.SubjectId == subjectId && !ss.IsDeleted,
                ss => ss.User
            );

            var enrolledIds = enrollments.Select(e => e.StudentId).ToList();

            var enrolledDtos = enrollments.Select(e => new UserDto
            {
                Id = e.User.Id,
                Username = e.User.Username,
                FullName = e.User.FullName,
                Role = e.User.Role
            });

            var notEnrolledDtos = allStudents
                .Where(u => !enrolledIds.Contains(u.Id))
                .Select(u => new UserDto
                {
                    Id = u.Id,
                    Username = u.Username,
                    FullName = u.FullName,
                    Role = u.Role
                });

            return (Enrolled: enrolledDtos, NotEnrolled: notEnrolledDtos);
        }
    }
}