using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using SmartEdu.Business.Interfaces;
using SmartEdu.Shared.DTOs;

namespace SmartEdu.Web.Controllers;

[Authorize(Roles = "Admin")]
public class LecturerManagementController : Controller
{
    private readonly ISubjectService _subjectService;
    private readonly IUnitOfWork _uow;

    public LecturerManagementController(ISubjectService subjectService, IUnitOfWork uow)
    {
        _subjectService = subjectService;
        _uow = uow;
    }

    public async Task<IActionResult> Index()
    {
        var subjects = await _subjectService.GetAllAsync();
        ViewBag.Subjects = new SelectList(subjects, "Id", "Name");

        // load lecturers
        var lecturers = await _uow.Users.GetAllAsync(u => u.Role == SmartEdu.Shared.Enums.UserRole.Lecturer && !u.IsDeleted);
        ViewBag.Lecturers = new SelectList(lecturers, "Id", "FullName");

        // current assignments
        var assignments = await _uow.LecturerSubjects.GetAllWithIncludeAsync(ls => true, ls => ls.Lecturer, ls => ls.Subject);
        ViewBag.Assignments = assignments;

        return View();
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Assign(AssignLecturerDto dto)
    {
        if (!ModelState.IsValid) return RedirectToAction(nameof(Index));

        await _subjectService.AssignLecturerToSubject(dto);
        TempData["Success"] = "Gán giảng viên thành công.";
        return RedirectToAction(nameof(Index));
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Unassign(int lecturerId, int subjectId)
    {
        await _subjectService.RemoveLecturerFromSubject(lecturerId, subjectId);
        TempData["Success"] = "Hủy phân công thành công.";
        return RedirectToAction(nameof(Index));
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> SetLeader(int lecturerId, int subjectId)
    {
        var dto = new AssignLecturerDto { LecturerId = lecturerId, SubjectId = subjectId, IsLeader = true };
        await _subjectService.AssignLecturerToSubject(dto);
        TempData["Success"] = "Đã đặt trưởng môn.";
        return RedirectToAction(nameof(Index));
    }
}
