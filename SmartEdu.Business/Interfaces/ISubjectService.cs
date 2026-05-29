using SmartEdu.Shared.DTOs;
using SmartEdu.Shared.Entities;

namespace SmartEdu.Business.Interfaces
{
    public interface ISubjectService
    {
        Task<IEnumerable<SubjectDto>> GetAllAsync();
        Task<SubjectDto?> GetByIdAsync(int id);
        Task CreateAsync(SubjectCreateDto dto);
        Task UpdateAsync(SubjectUpdateDto dto);
        Task<IEnumerable<SubjectDto>> GetSubjectsByUserIdAsync(int userId);
        Task DeleteAsync(int id);
        Task AssignStudentToSubject(int studentId, int subjectId);
        Task RemoveStudentFromSubject(int studentId, int subjectId);
        Task<(IEnumerable<UserDto> Enrolled, IEnumerable<UserDto> NotEnrolled)> GetStudentEnrollmentStatus(int subjectId);
    }
}
