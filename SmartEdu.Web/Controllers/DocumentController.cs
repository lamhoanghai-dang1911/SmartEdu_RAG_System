using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using SmartEdu.Business.Interfaces;
using SmartEdu.Web.Extensions;

namespace SmartEdu.Web.Controllers;

[Authorize]
public class DocumentController : Controller
{
    private readonly IDocumentService _documentService;
    private readonly ISubjectService _subjectService;
    private readonly IPermissionService _permissionService;
    private readonly IWebHostEnvironment _env;

    public DocumentController(
        IDocumentService documentService,
        ISubjectService subjectService,
        IPermissionService permissionService,
        IWebHostEnvironment env)
    {
        _documentService = documentService;
        _subjectService = subjectService;
        _permissionService = permissionService;
        _env = env;
    }

    public async Task<IActionResult> Index(int? subjectId)
    {
        int userId = User.GetUserId();
        bool isStaff = User.IsInRole("Lecturer") || User.IsInRole("Admin");
        var subjects = isStaff
            ? await _subjectService.GetAllAsync()
            : await _subjectService.GetSubjectsByUserIdAsync(userId);

        var docs = await _documentService.GetAllByUserIdAsync(userId, isStaff, subjectId);

        ViewBag.Subjects = new SelectList(subjects, "Id", "Name", subjectId);
        ViewBag.SelectedSubjectId = subjectId;

        return View(docs);
    }


    public async Task<IActionResult> Details(int id)
    {
        var doc = await _documentService.GetByIdAsync(id);
        if (doc is null) return NotFound();

        bool hasAccess = await _permissionService.CanUserAccessSubject(User.GetUserId(), doc.SubjectId);
        if (!hasAccess) return Forbid();

        return View(doc);
    }

    [Authorize(Roles = "Lecturer, Admin")]
    public async Task<IActionResult> Upload()
    {
        var subjects = await _subjectService.GetAllAsync();
        ViewBag.Subjects = new SelectList(subjects, "Id", "Name");
        return View();
    }

    // POST: /Document/Upload
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Upload(IFormFile file, string title, int subjectId)
    {
        if (file is null || file.Length == 0)
        {
            ModelState.AddModelError("file", "Vui lòng chọn file.");
            var subjects = await _subjectService.GetAllAsync();
            ViewBag.Subjects = new SelectList(subjects, "Id", "Name");
            return View();
        }

        try
        {
            await _documentService.UploadAsync(file, title, subjectId, _env.WebRootPath);
            TempData["Success"] = $"Upload '{title}' thành công!";
            return RedirectToAction(nameof(Index));
        }
        catch (InvalidOperationException ex)
        {
            ModelState.AddModelError("file", ex.Message);
            var subjects = await _subjectService.GetAllAsync();
            ViewBag.Subjects = new SelectList(subjects, "Id", "Name");
            return View();
        }
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    [Authorize(Roles = "Lecturer, Admin")]
    public async Task<IActionResult> Delete(int id)
    {
        await _documentService.DeleteAsync(id);
        TempData["Success"] = "Xóa tài liệu thành công!";
        return RedirectToAction(nameof(Index));
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    [Authorize(Roles = "Lecturer, Admin")]
    public async Task<IActionResult> TriggerEmbedding(int id)
    {
        try
        {
            await _documentService.TriggerEmbeddingAsync(id);
            TempData["Success"] = "Đã kích hoạt xử lý Embedding thành công!";
        }
        catch (Exception ex)
        {
            TempData["Error"] = $"Lỗi khi gọi AI: {ex.Message}";
        }

        return RedirectToAction(nameof(Index));
    }

    [HttpGet]
    [Authorize(Roles = "Lecturer, Admin")]
    public async Task<IActionResult> Edit(int id)
    {
        var doc = await _documentService.GetByIdAsync(id);
        if (doc == null) return NotFound();

        return View(doc);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    [Authorize(Roles = "Lecturer, Admin")]
    public async Task<IActionResult> Edit(int id, string title)
    {
        if (string.IsNullOrWhiteSpace(title))
        {
            ModelState.AddModelError("title", "Tiêu đề không được để trống.");
            var doc = await _documentService.GetByIdAsync(id);
            return View(doc);
        }

        try
        {
            await _documentService.UpdateTitleAsync(id, title);
            TempData["Success"] = "Cập nhật tiêu đề thành công!";
            return RedirectToAction(nameof(Index));
        }
        catch (Exception ex)
        {
            TempData["Error"] = $"Lỗi cập nhật: {ex.Message}";
            return RedirectToAction(nameof(Index));
        }
    }

    [HttpGet]
    public async Task<IActionResult> Download(int id)
    {
        var doc = await _documentService.GetByIdAsync(id);
        if (doc == null) return NotFound();

        int userId = User.GetUserId();
        bool isStaff = User.IsInRole("Lecturer") || User.IsInRole("Admin");

        if (!isStaff)
        {
            bool hasAccess = await _permissionService.CanUserAccessSubject(userId, doc.SubjectId);
            if (!hasAccess) return Forbid();
        }

        try
        {
            var fileDto = await _documentService.GetFileForDownloadAsync(id);
            if (fileDto == null) return NotFound();

            return PhysicalFile(fileDto.FilePath, fileDto.ContentType, fileDto.FileName);
        }
        catch (FileNotFoundException ex)
        {
            TempData["Error"] = ex.Message;
            return RedirectToAction(nameof(Index));
        }
    }
}