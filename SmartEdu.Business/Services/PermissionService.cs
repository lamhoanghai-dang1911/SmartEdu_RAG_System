using Microsoft.EntityFrameworkCore;
using SmartEdu.Business.Interfaces;
using SmartEdu.Data;
using SmartEdu.Data.Repositories;
using SmartEdu.Shared.Entities;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace SmartEdu.Business.Services
{
    public class PermissionService : IPermissionService
    {
        private readonly IRepository<StudentSubject> _studentSubjectRepo;

        public PermissionService(IRepository<StudentSubject> studentSubjectRepo)
        {
            _studentSubjectRepo = studentSubjectRepo;
        }

        public async Task<bool> CanUserAccessSubject(int userId, int subjectId)
        {
            var enrollments = await _studentSubjectRepo.GetAllAsync(
                ss => ss.StudentId == userId && ss.SubjectId == subjectId && !ss.IsDeleted
            );

            return enrollments.Any();
        }
    }
}
