using DocumentFormat.OpenXml.Spreadsheet;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using SmartEdu.Business.Interfaces;
using SmartEdu.Data;
using SmartEdu.Data.Repositories;
using SmartEdu.Shared.DTOs;
using SmartEdu.Shared.Entities;
using SmartEdu.Shared.Enums;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;

namespace SmartEdu.Business.Services;

public class ChatService : IChatService
{
    private readonly IRepository<ChatSession> _sessionRepo;
    private readonly IRepository<ChatMessage> _messageRepo;
    private readonly IRepository<DocumentChunk> _chunkRepo;

    private readonly IHttpClientFactory _httpFactory;
    private readonly IConfiguration _configuration;
    //private readonly IServiceScopeFactory _serviceScopeFactory;

    public ChatService(
        IRepository<ChatSession> sessionRepo,
        IRepository<ChatMessage> messageRepo,
        IRepository<DocumentChunk> chunkRepo,
        IHttpClientFactory httpFactory,
        IConfiguration configuration
        //IServiceScopeFactory serviceScopeFactory
        )
    {
        _sessionRepo = sessionRepo;
        _messageRepo = messageRepo;
        _chunkRepo = chunkRepo;
        _httpFactory = httpFactory;
        _configuration = configuration;
        //_serviceScopeFactory = serviceScopeFactory;
    }

    public async Task<ChatResponseDto> AskAsync(ChatRequestDto request)
    {
        if (!request.SubjectId.HasValue || request.SubjectId.Value <= 0)
        {
            return new ChatResponseDto { SessionId = request.SessionId, Answer = "Vui lòng chọn môn học.", Sources = new List<string>() };
        }

        var sessions = await _sessionRepo.GetAllAsync(s => s.SessionId == request.SessionId);
        var session = sessions.FirstOrDefault();
        if (session == null)
        {
            session = new ChatSession
            {
                SessionId = request.SessionId,
                SubjectId = request.SubjectId,
                Title = request.Question.Length > 30 ? request.Question[..30] + "..." : request.Question,
                UserId = request.UserId
            };
            await _sessionRepo.AddAsync(session);
            await _sessionRepo.SaveChangesAsync();
        }
        else if (session.UserId != request.UserId)
        {
            throw new UnauthorizedAccessException("Bạn không có quyền truy cập phiên chat này.");
        }
        var ragTask = RunRagPipelineAsync(request, session);

        var ragResponse = await ragTask;

        return ragResponse;
    }

    private async Task<ChatResponseDto> RunRagPipelineAsync(ChatRequestDto request, ChatSession session)
    {
        var userMessage = new ChatMessage { ChatSessionId = session.Id, Role = "user", Content = request.Question };
        await _messageRepo.AddAsync(userMessage);
        await _messageRepo.SaveChangesAsync();

        float[] queryVector = await GetHuggingFaceEmbeddingAsync(request.Question);
        var chunks = await _chunkRepo.GetAllWithIncludeAsync(
            c => c.Document != null && c.Document.Status == DocumentStatus.Ready && c.Document.SubjectId == request.SubjectId.Value,
            c => c.Document
        );

        var topChunks = chunks.Select(c => new { Chunk = c, Score = CosineSimilarity(queryVector, JsonSerializer.Deserialize<float[]>(c.EmbeddingJson)) })
                              .OrderByDescending(s => s.Score).Take(3).ToList();

        var contextBuilder = new StringBuilder();
        var sources = new HashSet<string>();
        foreach (var item in topChunks)
        {
            contextBuilder.AppendLine($"[Nguồn: {item.Chunk.Document.Title}]\n{item.Chunk.Content}\n---\n");
            sources.Add(item.Chunk.Document.Title);
        }

        string answer = await GenerateGeminiResponseAsync(contextBuilder.ToString(), request.Question);
        var assistantMessage = new ChatMessage { ChatSessionId = session.Id, Role = "assistant", Content = answer };
        await _messageRepo.AddAsync(assistantMessage);
        await _messageRepo.SaveChangesAsync();

        return new ChatResponseDto { SessionId = request.SessionId, Answer = answer, Sources = sources.ToList() };
    }
    public async Task<IEnumerable<ChatMessageDto>> GetHistoryAsync(string sessionId, string userId)
    {
        int uid = int.Parse(userId);

        var sessions = await _sessionRepo.GetAllAsync(s => s.SessionId == sessionId && s.UserId == uid);
        var session = sessions.FirstOrDefault();
        if (session is null) return Enumerable.Empty<ChatMessageDto>();

        var messages = await _messageRepo.GetAllAsync(m => m.ChatSessionId == session.Id);

        return messages
            .OrderBy(m => m.CreatedAt)
            .Select(m => new ChatMessageDto
            {
                Id = m.Id,
                Role = m.Role,
                Content = m.Content,
                CreatedAt = m.CreatedAt
            }).ToList();
    }

    private async Task<float[]> GetHuggingFaceEmbeddingAsync(string question)
    {
        var hfToken = _configuration["HuggingFace:Token"];
        if (string.IsNullOrWhiteSpace(hfToken)) throw new Exception("Thiếu HuggingFace Token.");

        var client = _httpFactory.CreateClient();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", hfToken);
        client.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));

        string modelUrl = "https://router.huggingface.co/hf-inference/models/intfloat/multilingual-e5-base/pipeline/feature-extraction";

        // Model E5 yêu cầu prefix 'query: ' cho câu hỏi
        var payload = new { inputs = $"query: {question}" };
        using var content = new StringContent(JsonSerializer.Serialize(payload), Encoding.UTF8, "application/json");

        var resp = await client.PostAsync(modelUrl, content);
        resp.EnsureSuccessStatusCode();
        var respJson = await resp.Content.ReadAsStringAsync();

        using var docJson = JsonDocument.Parse(respJson);
        var root = docJson.RootElement;
        var vectorArray = root.ValueKind == JsonValueKind.Array && root[0].ValueKind != JsonValueKind.Number ? root[0] : root;

        return vectorArray.EnumerateArray().Select(x => x.GetSingle()).ToArray();
    }

    private async Task<string> GenerateGeminiResponseAsync(string context, string question)
    {
        var apiKey = _configuration["Gemini:ApiKey"];
        if (string.IsNullOrWhiteSpace(apiKey) || apiKey == "myKey")
            throw new Exception("Thiếu Gemini API Key hợp lệ trong User Secrets.");

        // Đảm bảo endpoint gọi bản 2.5-flash ổn định
        string url = $"https://generativelanguage.googleapis.com/v1beta/models/gemini-2.5-flash:generateContent?key={apiKey.Trim()}";

        string fullPrompt = @"Bạn là trợ lý học tập thông minh (SmartEdu AI).
Nhiệm vụ của bạn là trả lời câu hỏi dựa trên các đoạn ngữ cảnh trích từ tài liệu. Luôn trả lời bằng tiếng Việt tự nhiên, lịch sự. Nếu thông tin không có, hãy nói 'Tôi không tìm thấy thông tin'.

NGỮ CẢNH:
" + context + @"

CÂU HỎI:
" + question;

        // Cấu trúc Payload chuẩn hóa 100% cho Gemini 2.x - Đưa maxOutputTokens vào đúng vị trí
        var payload = new
        {
            contents = new[]
            {
                new { parts = new[] { new { text = fullPrompt } } }
            },
            generationConfig = new
            {
                temperature = 0.3,
                maxOutputTokens = 8192
            }
        };

        var client = _httpFactory.CreateClient();
        using var content = new StringContent(JsonSerializer.Serialize(payload), Encoding.UTF8, "application/json");

        var resp = await client.PostAsync(url, content);
        var respJson = await resp.Content.ReadAsStringAsync();

        if (!resp.IsSuccessStatusCode)
        {
            throw new Exception($"Lỗi báo từ Google: {respJson}");
        }

        using var docJson = JsonDocument.Parse(respJson);

        var root = docJson.RootElement;
        var candidates = root.GetProperty("candidates");

        if (candidates.GetArrayLength() == 0) return "Không có phản hồi từ AI.";

        var contentElement = candidates[0].GetProperty("content");
        var parts = contentElement.GetProperty("parts");

        StringBuilder fullAnswer = new StringBuilder();
        foreach (var part in parts.EnumerateArray())
        {
            if (part.TryGetProperty("text", out var textElement))
            {
                fullAnswer.Append(textElement.GetString());
            }
        }

        string finalResult = fullAnswer.ToString().Trim();
        return string.IsNullOrEmpty(finalResult) ? "Không có phản hồi từ AI." : finalResult;
    }

    private async Task<string> GenerateSessionTitleAsync(string firstMessage)
    {
        var apiKey = _configuration["Gemini:ApiKey"];
        if (string.IsNullOrWhiteSpace(apiKey) || apiKey == "myKey")
            return null;

        string url = $"https://generativelanguage.googleapis.com/v1beta/models/gemini-2.5-flash:generateContent?key={apiKey.Trim()}";

        string prompt = $"Hãy tóm tắt câu hỏi sau thành một tiêu đề ngắn gọn, tối đa 5-7 từ, chỉ trả về tiêu đề, không giải thích gì thêm: {firstMessage}";

        var payload = new
        {
            contents = new[]
            {
                new { parts = new[] { new { text = prompt } } }
            },
            generationConfig = new
            {
                temperature = 0.0,
                maxOutputTokens = 32,
                stop = new[] { "\n" }
            }
        };

        try
        {
            var client = _httpFactory.CreateClient();
            using var content = new StringContent(JsonSerializer.Serialize(payload), Encoding.UTF8, "application/json");
            var resp = await client.PostAsync(url, content);
            if (!resp.IsSuccessStatusCode) return null;
            var respJson = await resp.Content.ReadAsStringAsync();

            using var docJson = JsonDocument.Parse(respJson);
            var root = docJson.RootElement;
            if (!root.TryGetProperty("candidates", out var candidates)) return null;
            if (candidates.GetArrayLength() == 0) return null;

            var contentElement = candidates[0].GetProperty("content");
            var parts = contentElement.GetProperty("parts");

            var sb = new StringBuilder();
            foreach (var part in parts.EnumerateArray())
            {
                if (part.TryGetProperty("text", out var textEl)) sb.Append(textEl.GetString());
            }

            var title = sb.ToString().Trim();
            // Keep only single-line short title
            if (title.Contains('\n')) title = title.Split('\n')[0].Trim();
            if (title.Length > 150) title = title[..150];
            return string.IsNullOrWhiteSpace(title) ? null : title;
        }
        catch
        {
            return null;
        }
    }

    private async Task<ChatResponseDto> SaveAndReturnErrorAsync(int sessionId, string sessionGuid, string errorMsg)
    {
        var errMessage = new ChatMessage
        {
            ChatSessionId = sessionId,
            Role = "assistant",
            Content = errorMsg
        };
        await _messageRepo.AddAsync(errMessage);
        await _messageRepo.SaveChangesAsync();
        return new ChatResponseDto { SessionId = sessionGuid, Answer = errorMsg, Sources = new List<string>() };
    }

    private static double CosineSimilarity(float[] a, float[] b)
    {
        if (a == null || b == null || a.Length != b.Length) return 0;
        double dot = 0, na = 0, nb = 0;
        for (int i = 0; i < a.Length; i++)
        {
            dot += a[i] * b[i];
            na += a[i] * a[i];
            nb += b[i] * b[i];
        }
        if (na == 0 || nb == 0) return 0;
        return dot / (Math.Sqrt(na) * Math.Sqrt(nb));
    }

    public async Task<IEnumerable<ChatSessionDto>> GetSessionsByUserIdAsync(string userId)
    {
        int uid = int.Parse(userId);
        var sessions = await _sessionRepo.GetAllWithIncludeAsync(
            s => !s.IsDeleted && s.UserId == uid,
            s => s.Subject
        );

        return sessions
            .OrderByDescending(s => s.CreatedAt)
            .Select(s => new ChatSessionDto
            {
                SessionId = s.SessionId,
                Title = s.Title,
                SubjectId = s.SubjectId,
                SubjectName = s.Subject != null ? s.Subject.Name : "Tất cả",
                MessageCount = 0
            }).ToList();
    }

    public async Task DeleteSessionAsync(string sessionId, string userId)
    {
        int uid = int.Parse(userId);
        var sessions = await _sessionRepo.GetAllAsync(s => s.SessionId == sessionId && s.UserId == uid);
        var session = sessions.FirstOrDefault();

        if (session is null)
        {
            return;
        }

        session.IsDeleted = true;
        session.UpdatedAt = DateTime.UtcNow;

        _sessionRepo.Update(session);
        await _sessionRepo.SaveChangesAsync();
    }
}