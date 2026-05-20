using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using SmartEdu.Business.Interfaces;

namespace SmartEdu.Web.Controllers;

public class DocumentController : Controller
{
    private readonly IDocumentService _documentService;
    private readonly ISubjectService _subjectService;

    public DocumentController(
        IDocumentService documentService,
        ISubjectService subjectService)
    {
        _documentService = documentService;
        _subjectService = subjectService;
    }

    // GET: /Document
    public async Task<IActionResult> Index(int? subjectId)
    {
        var docs = await _documentService.GetAllAsync(subjectId);
        var subjects = await _subjectService.GetAllAsync();

        ViewBag.Subjects = new SelectList(subjects, "Id", "Name", subjectId);
        ViewBag.SelectedSubjectId = subjectId;
        return View(docs);
    }

    // GET: /Document/Upload
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
            await _documentService.UploadAsync(file, title, subjectId);
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

    // GET: /Document/Details/5
    public async Task<IActionResult> Details(int id)
    {
        var doc = await _documentService.GetByIdAsync(id);
        if (doc is null) return NotFound();
        return View(doc);
    }

    // POST: /Document/Delete/5
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Delete(int id)
    {
        await _documentService.DeleteAsync(id);
        TempData["Success"] = "Xóa tài liệu thành công!";
        return RedirectToAction(nameof(Index));
    }

    // POST: /Document/TriggerEmbedding/5
    [HttpPost]
    [ValidateAntiForgeryToken]
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
}