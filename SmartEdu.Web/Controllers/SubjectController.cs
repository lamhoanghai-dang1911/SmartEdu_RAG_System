using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using ExcelDataReader;
using System.Data;
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

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> ImportStudents(int id, IFormFile file)
        {
            if (file == null || file.Length == 0)
                return BadRequest("Vui lòng chọn một file hợp lệ.");

            if (!file.FileName.EndsWith(".xlsx"))
                return BadRequest("Chỉ hỗ trợ định dạng .xlsx");

            var studentList = new List<StudentImportDto>();

            // Use ExcelDataReader to avoid EPPlus license requirements for reading .xlsx files
            System.Text.Encoding.RegisterProvider(System.Text.CodePagesEncodingProvider.Instance);
            using (var stream = file.OpenReadStream())
            using (var reader = ExcelDataReader.ExcelReaderFactory.CreateReader(stream))
            {
                var conf = new ExcelDataReader.ExcelDataSetConfiguration
                {
                    ConfigureDataTable = _ => new ExcelDataReader.ExcelDataTableConfiguration { UseHeaderRow = true }
                };

                var dataSet = reader.AsDataSet(conf);
                if (dataSet.Tables.Count == 0) return BadRequest("File Excel không có sheet nào.");

                var table = dataSet.Tables[0];

                // Find required columns by header
                int codeCol = -1, nameCol = -1, emailCol = -1;
                for (int c = 0; c < table.Columns.Count; c++)
                {
                    var header = table.Columns[c].ColumnName?.ToString().Trim().ToLower() ?? "";
                    if (header == "student code") codeCol = c;
                    else if (header == "full name") nameCol = c;
                    else if (header == "email") emailCol = c;
                }

                if (codeCol == -1 || nameCol == -1 || emailCol == -1)
                    return BadRequest("File Excel không đúng định dạng. Cần có các cột: 'Student Code', 'Full Name', 'Email'");

                foreach (System.Data.DataRow row in table.Rows)
                {
                    studentList.Add(new StudentImportDto
                    {
                        StudentCode = row[codeCol]?.ToString() ?? "",
                        FullName = row[nameCol]?.ToString() ?? "",
                        Email = row[emailCol]?.ToString() ?? ""
                    });
                }
            }

            try
            {
                await _subjectService.ImportStudentsAsync(id, studentList);
                TempData["Success"] = $"Import thành công {studentList.Count} sinh viên.";
                return RedirectToAction("ManageStudents", new { id = id });
            }
            catch (Exception ex)
            {
                TempData["Error"] = "Lỗi khi xử lý file: " + ex.Message;
                return RedirectToAction("ManageStudents", new { id = id });
            }
        }
    }
}
