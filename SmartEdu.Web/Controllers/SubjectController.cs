using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using SmartEdu.Business.Interfaces;
using SmartEdu.Shared.DTOs;
using SmartEdu.Shared.Entities;
using SmartEdu.Web.Extensions;

namespace SmartEdu.Web.Controllers
{
    [Authorize(Roles = "Admin, Lecturer")]
    public class SubjectController : Controller
    {
        private readonly ISubjectService _subjectService;

        public SubjectController(ISubjectService subjectService)
        {
            _subjectService = subjectService;
        }

        public async Task<IActionResult> Index()
        {
            if (User.IsInRole("Admin") || User.IsInRole("Lecturer"))
            {
                return View(await _subjectService.GetAllAsync());
            }

            int userId = User.GetUserId();
            var mySubjects = await _subjectService.GetSubjectsByUserIdAsync(userId);
            return View(mySubjects);
        }

        [HttpGet]
        [Authorize(Roles = "Admin")]
        public IActionResult Create() => View();

        [HttpPost]
        [ValidateAntiForgeryToken]
        [Authorize(Roles = "Admin")]
        public async Task<IActionResult> Create(SubjectCreateDto dto)
        {
            if (!ModelState.IsValid) return View(dto);

            await _subjectService.CreateAsync(dto);
            TempData["Success"] = "Tạo môn học thành công!";
            return RedirectToAction(nameof(Index));
        }

        [Authorize(Roles = "Admin")]
        public async Task<IActionResult> Edit(int id)
        {
            var subject = await _subjectService.GetByIdAsync(id);
            if (subject is null) return NotFound();

            var dto = new SubjectUpdateDto
            {
                Id = subject.Id,
                Name = subject.Name,
                Description = subject.Description
            };

            return View(dto);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        [Authorize(Roles = "Admin")]
        public async Task<IActionResult> Edit(SubjectUpdateDto dto)
        {
            if (!ModelState.IsValid) return View(dto);

            await _subjectService.UpdateAsync(dto);
            TempData["Success"] = "Cập nhật thành công!";
            return RedirectToAction(nameof(Index));
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        [Authorize(Roles = "Admin")]
        public async Task<IActionResult> Delete(int id)
        {
            await _subjectService.DeleteAsync(id);
            TempData["Success"] = "Xóa môn học thành công!";
            return RedirectToAction(nameof(Index));
        }

        [HttpGet]
        [Authorize(Roles = "Admin")]
        public async Task<IActionResult> ManageStudents(int id)
        {
            var subject = await _subjectService.GetByIdAsync(id);
            if (subject == null) return NotFound();
            var subjectDto = new SubjectDto
            {
                Id = subject.Id,
                Name = subject.Name,
                Description = subject.Description
            };

            var (enrolled, notEnrolled) = await _subjectService.GetStudentEnrollmentStatus(id);

            ViewBag.Subject = subjectDto;

            return View((Enrolled: enrolled, NotEnrolled: notEnrolled));
        }

        [HttpPost]
        [Authorize(Roles = "Admin")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> AssignStudent(int subjectId, int studentId)
        {
            await _subjectService.AssignStudentToSubject(studentId, subjectId);
            TempData["Success"] = "Đã thêm sinh viên vào môn học!";

            return RedirectToAction(nameof(ManageStudents), new { id = subjectId });
        }

        [HttpPost]
        [Authorize(Roles = "Admin")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> RemoveStudent(int subjectId, int studentId)
        {
            await _subjectService.RemoveStudentFromSubject(studentId, subjectId);
            TempData["Success"] = "Đã xóa sinh viên khỏi môn học!";

            return RedirectToAction(nameof(ManageStudents), new { id = subjectId });
        }
    }
}
