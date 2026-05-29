using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using SmartEdu.Business.Interfaces;
using SmartEdu.Shared.DTOs;
using Microsoft.AspNetCore.Authorization;
using SmartEdu.Web.Extensions;

namespace SmartEdu.Web.Controllers
{
    [Authorize]
    public class ChatController : Controller
    {
        private readonly IChatService _chatService;
        private readonly ISubjectService _subjectService;
        private readonly IPermissionService _permissionService;
        private readonly IDocumentService _documentService;

        public ChatController(
            IChatService chatService,
            ISubjectService subjectService,
            IPermissionService permissionService,
            IDocumentService documentService)
        {
            _chatService = chatService;
            _subjectService = subjectService;
            _permissionService = permissionService;
            _documentService = documentService;
        }

        public async Task<IActionResult> Index(string? sessionId)
        {
            int userId = User.GetUserId();
            if (userId == 0) return Unauthorized();

            var sessions = await _chatService.GetSessionsByUserIdAsync(userId.ToString());
            var enrolledSubjects = await _subjectService.GetSubjectsByUserIdAsync(userId);

            var currentSession = sessions.FirstOrDefault(x => x.SessionId == sessionId);
            int? selectedSubjectId = currentSession?.SubjectId;

            ViewBag.Subjects = new SelectList(enrolledSubjects, "Id", "Name", selectedSubjectId);

            ViewBag.CurrentSessionTitle = currentSession?.Title ?? "Phiên mới";
            ViewBag.Sessions = sessions;
            ViewBag.CurrentSessionId = sessionId ?? Guid.NewGuid().ToString();

            IEnumerable<ChatMessageDto> history = Enumerable.Empty<ChatMessageDto>();
            if (!string.IsNullOrEmpty(sessionId))
            {
                history = await _chatService.GetHistoryAsync(sessionId, userId.ToString());
            }

            ViewBag.History = history;
            return View();
        }


        [HttpPost]
        public async Task<IActionResult> Ask([FromBody] ChatRequestDto request)
        {
            int userId = User.GetUserId();
            if (userId == 0) return Unauthorized();

            request.UserId = userId;

            if (string.IsNullOrWhiteSpace(request.Question))
                return BadRequest(new { error = "Câu hỏi không được để trống." });

            if (request.SubjectId.HasValue)
            {
                bool hasAccess = await _permissionService.CanUserAccessSubject(userId, request.SubjectId.Value);
                if (!hasAccess) return Forbid();

                bool hasDocs = await _documentService.HasReadyDocumentsAsync(request.SubjectId.Value);
                if (!hasDocs)
                {
                    return Ok(new
                    {
                        answer = "Môn học này hiện chưa có tài liệu nào được xử lý hoàn tất. Vui lòng đợi giảng viên tải lên và kích hoạt nhé! 📚"
                    });
                }
            }

            try
            {
                var response = await _chatService.AskAsync(request);
                return Ok(response);
            }
            catch (UnauthorizedAccessException)
            {
                return Forbid();
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { error = ex.Message });
            }
        }

        [HttpGet]
        public async Task<IActionResult> History(string sessionId)
        {
            int userId = User.GetUserId();
            if (userId == 0) return Unauthorized();

            var messages = await _chatService.GetHistoryAsync(sessionId, userId.ToString());
            return Ok(messages);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> DeleteSession(string sessionId)
        {
            int userId = User.GetUserId();
            if (userId == 0) return Unauthorized();

            await _chatService.DeleteSessionAsync(sessionId, userId.ToString());
            TempData["Success"] = "Đã xóa phiên chat!";
            return RedirectToAction(nameof(Index));
        }


    }
}
