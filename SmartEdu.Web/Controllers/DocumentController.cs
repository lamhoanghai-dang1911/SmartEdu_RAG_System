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

    [HttpGet]
    public async Task<IActionResult> GetDocumentChunks(int documentId)
    {
        var doc = await _documentService.GetByIdAsync(documentId);
        if (doc == null) return NotFound();

        int userId = User.GetUserId();
        bool isStaff = User.IsInRole("Lecturer") || User.IsInRole("Admin");
        if (!isStaff)
        {
            var canAccess = await _permissionService.CanUserAccessSubject(userId, doc.SubjectId);
            if (!canAccess) return Forbid();
        }

        // use service to map to DTOs
        var chunks = await _documentService.GetChunksByDocumentIdAsync(documentId);
        return Json(chunks);
    }

    [HttpGet]
    public async Task<IActionResult> CanUpload(int subjectId)
    {
        if (subjectId <= 0) return Json(new { canUpload = false, message = "Vui lòng chọn môn học." });
        // Only roles allowed to access Upload page are Lecturer and Admin (controller Upload GET is protected)
        if (User.IsInRole("Admin"))
        {
            return Json(new { canUpload = true, userId = User.GetUserId() });
        }

        if (!User.IsInRole("Lecturer"))
        {
            return Json(new { canUpload = false, message = "Bạn không có quyền upload tài liệu cho môn này. Chỉ trưởng môn được phép.", userId = User.GetUserId() });
        }

        int userId = User.GetUserId();
        var can = await _subject_service_canupload(userId, subjectId);
        if (!can)
            return Json(new { canUpload = false, message = "Bạn không có quyền upload tài liệu cho môn này. Chỉ trưởng môn được phép.", userId });

        return Json(new { canUpload = true, userId });
    }

    // wrapper to call subject service CanUploadDocument with defensive null checks
    private async Task<bool> _subject_service_canupload(int userId, int subjectId)
    {
        try
        {
            return await _subjectService.CanUploadDocument(userId, subjectId);
        }
        catch
        {
            return false;
        }
    }

    [HttpGet]
    [Authorize(Roles = "Lecturer, Admin")]
    public IActionResult TriggerEmbeddingGet(int id)
    {
        try
        {
            var scopeFactory = HttpContext.RequestServices.GetService(typeof(IServiceScopeFactory)) as IServiceScopeFactory;
            if (scopeFactory != null)
            {
                Task.Run(async () =>
                {
                    try
                    {
                        using var scope = scopeFactory.CreateScope();
                        var svc = scope.ServiceProvider.GetRequiredService<IDocumentService>();
                        await svc.TriggerEmbeddingAsync(id);
                    }
                    catch (Exception ex)
                    {
                        Console.WriteLine($"Background embedding failed (GET): {ex}");
                    }
                });
            }
            else
            {
                _ = Task.Run(() => _documentService.TriggerEmbeddingAsync(id));
            }

            TempData["Success"] = "Đã bắt đầu xử lý embedding (background). Mở Log để theo dõi tiến trình.";
            return RedirectToAction(nameof(Index));
        }
        catch (Exception ex)
        {
            var baseMsg = ex.GetBaseException()?.Message ?? ex.Message;
            TempData["Error"] = $"Lỗi khi khởi tạo embedding: {baseMsg}";
            return RedirectToAction(nameof(Index));
        }
    }

    [HttpGet]
    public async Task<IActionResult> GetLogs(int documentId)
    {
        // Only allow users who can access the document to read logs
        var doc = await _documentService.GetByIdAsync(documentId);
        if (doc == null) return NotFound();

        int userId = User.GetUserId();
        bool isStaff = User.IsInRole("Lecturer") || User.IsInRole("Admin");
        if (!isStaff)
        {
            var canAccess = await _permissionService.CanUserAccessSubject(userId, doc.SubjectId);
            if (!canAccess) return Forbid();
        }

        // Use UnitOfWork repository to fetch logs
        var uow = HttpContext.RequestServices.GetService(typeof(IUnitOfWork)) as IUnitOfWork;
        if (uow == null) return StatusCode(500);

        var logs = await uow.DocumentLogs.GetAllWithIncludeAsync(l => l.DocumentId == documentId, l => l.Document);
        var ordered = logs.OrderBy(l => l.Timestamp).Select(l => new { l.Id, l.LogMessage, Timestamp = l.Timestamp, l.Status });
        return Json(ordered);
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
    [RequestSizeLimit(10 * 1024 * 1024)]
    public async Task<IActionResult> Upload()
    {
        var subjects = await _subjectService.GetAllAsync();
        ViewBag.Subjects = new SelectList(subjects, "Id", "Name");
        return View();
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    [RequestSizeLimit(10 * 1024 * 1024)]
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
            int userId = User.GetUserId();
            // check permission: only leader of subject can upload
            bool canUpload = await _subjectService.CanUploadDocument(userId, subjectId);
            if (!canUpload)
                throw new InvalidOperationException("Bạn không có quyền upload tài liệu cho môn này. Chỉ trưởng môn được phép.");

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
    public IActionResult TriggerEmbedding(int id)
    {
        try
        {
            // run embedding in a background scope so we can return immediately
            var scopeFactory = HttpContext.RequestServices.GetService(typeof(IServiceScopeFactory)) as IServiceScopeFactory;
            if (scopeFactory != null)
            {
                Task.Run(async () =>
                {
                    try
                    {
                        using var scope = scopeFactory.CreateScope();
                        var svc = scope.ServiceProvider.GetRequiredService<IDocumentService>();
                        await svc.TriggerEmbeddingAsync(id);
                    }
                    catch (Exception ex)
                    {
                        Console.WriteLine($"Background embedding failed: {ex}");
                    }
                });
            }
            else
            {
                // fallback: call directly (will run on request scope)
                _ = Task.Run(() => _documentService.TriggerEmbeddingAsync(id));
            }

            // Return Accepted immediately; client will poll logs
            return Accepted();
        }
        catch (Exception ex)
        {
            var baseMsg = ex.GetBaseException()?.Message ?? ex.Message;
            return StatusCode(500, baseMsg);
        }
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
        if (doc.Status == SmartEdu.Shared.Enums.DocumentStatus.Pending)
        {
            TempData["Error"] = "Tài liệu này đang chờ xử lý, chưa thể tải xuống lúc này.";
            return RedirectToAction(nameof(Index));
        }

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