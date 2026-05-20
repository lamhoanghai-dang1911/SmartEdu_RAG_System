using Microsoft.EntityFrameworkCore;
using System.Net.Http.Headers;
using System.Text.Json;
using System.Text;
using Microsoft.Extensions.Configuration;
using System.Linq;
using System.Collections.Generic;
using SmartEdu.Shared.Enums;
using SmartEdu.Business.Interfaces;
using SmartEdu.Data;
using SmartEdu.Data.Repositories;
using SmartEdu.Shared.DTOs;
using SmartEdu.Shared.Entities;

namespace SmartEdu.Business.Services
{
    public class ChatService : IChatService
    {
        private readonly IRepository<ChatSession> _sessionRepo;
        private readonly IRepository<ChatMessage> _messageRepo;
        private readonly AppDbContext _context;
        private readonly IHttpClientFactory _httpFactory;
        private readonly IConfiguration _configuration;

        // Inject IHttpClientFactory và IConfiguration để gọi OpenAI
        public ChatService(
            IRepository<ChatSession> sessionRepo,
            IRepository<ChatMessage> messageRepo,
            AppDbContext context,
            IHttpClientFactory httpFactory,
            IConfiguration configuration)
        {
            _sessionRepo = sessionRepo;
            _messageRepo = messageRepo;
            _context = context;
            _httpFactory = httpFactory;
            _configuration = configuration;
        }

        public async Task<ChatResponseDto> AskAsync(ChatRequestDto request)
        {
            // Tìm hoặc tạo session
            var session = await _context.ChatSessions
                .FirstOrDefaultAsync(s => s.SessionId == request.SessionId);

            if (session is null)
            {
                session = new ChatSession
                {
                    SessionId = request.SessionId,
                    SubjectId = request.SubjectId,
                    Title = request.Question.Length > 50
                        ? request.Question[..50] + "..."
                        : request.Question
                };
                await _sessionRepo.AddAsync(session);
                await _sessionRepo.SaveChangesAsync();
            }

            // Lưu câu hỏi user
            var userMessage = new ChatMessage
            {
                ChatSessionId = session.Id,
                Role = "user",
                Content = request.Question
            };
            await _messageRepo.AddAsync(userMessage);
            await _messageRepo.SaveChangesAsync();

            // Bắt đầu pipeline RAG:
            // 1. Tạo embedding cho câu hỏi
            var openAiKey = _configuration?["OpenAI:ApiKey"] ?? Environment.GetEnvironmentVariable("OPENAI_API_KEY");
            if (string.IsNullOrWhiteSpace(openAiKey))
            {
                var fallback = $"[Lỗi cấu hình] OpenAI API key chưa được thiết lập.";
                // Lưu assistant message lỗi
                var assistantErr = new ChatMessage
                {
                    ChatSessionId = session.Id,
                    Role = "assistant",
                    Content = fallback
                };
                await _messageRepo.AddAsync(assistantErr);
                await _messageRepo.SaveChangesAsync();
                return new ChatResponseDto { SessionId = request.SessionId, Answer = fallback };
            }

            var client = _httpFactory.CreateClient();
            client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", openAiKey);

            // Gọi OpenAI Embeddings cho câu hỏi
            var embedPayload = new { input = request.Question, model = "text-embedding-3-small" };
            var embedJson = JsonSerializer.Serialize(embedPayload);
            using var embedContent = new StringContent(embedJson, Encoding.UTF8, "application/json");
            using var embedResp = await client.PostAsync("https://api.openai.com/v1/embeddings", embedContent);
            embedResp.EnsureSuccessStatusCode();
            var embedRespJson = await embedResp.Content.ReadAsStringAsync();
            using var embedDoc = JsonDocument.Parse(embedRespJson);
            var qElements = embedDoc.RootElement.GetProperty("data")[0].GetProperty("embedding");
            // Convert embedding floats to double[] for similarity calculations
            var qVec = qElements.EnumerateArray().Select(x => (double)x.GetSingle()).ToArray();

            // 2. Lấy các chunk từ DB (Document.Status == Ready) và tính cosine similarity
            var chunks = await _context.DocumentChunks
                .Where(c => c.Document != null && c.Document.Status == DocumentStatus.Ready)
                .Include(c => c.Document)
                .ToListAsync();

            // Parse embedding json and compute similarity
            var scored = new List<(DocumentChunk chunk, double score)>();
            foreach (var c in chunks)
            {
                if (string.IsNullOrWhiteSpace(c.EmbeddingJson)) continue;
                try
                {
                    var vecf = JsonSerializer.Deserialize<float[]>(c.EmbeddingJson);
                    if (vecf == null) continue;
                    var vec = vecf.Select(v => (double)v).ToArray();
                    var sim = CosineSimilarity(qVec, vec);
                    scored.Add((chunk: c, score: sim));
                }
                catch { continue; }
            }

            // Lấy top 3 chunk có điểm cao nhất
            var top = scored.OrderByDescending(s => s.score).Take(3).ToList();

            // 3. Kết hợp context và gọi OpenAI Chat Completion (sử dụng gpt-4o-mini)
            var contextBuilder = new StringBuilder();
            var sources = new HashSet<string>();
            foreach (var (chunk, score) in top)
            {
                contextBuilder.AppendLine($"[Source: {chunk.Document.Title}]\n{chunk.Content}\n---\n");
                if (!string.IsNullOrWhiteSpace(chunk.Document.Title)) sources.Add(chunk.Document.Title);
            }

            var systemPrompt = "Bạn là trợ lý học tập thông minh. Sử dụng các đoạn ngữ cảnh dưới đây trích từ tài liệu để trả lời câu hỏi. Luôn trích dẫn nguồn bằng tiêu đề tài liệu.";
            var userPrompt = $"Context:\n{contextBuilder}\nUser question: {request.Question}";

            var chatReq = new
            {
                model = "gpt-4o-mini",
                messages = new[] {
                    new { role = "system", content = systemPrompt },
                    new { role = "user", content = userPrompt }
                },
                max_tokens = 800
            };

            var chatJson = JsonSerializer.Serialize(chatReq);
            using var chatContent = new StringContent(chatJson, Encoding.UTF8, "application/json");
            using var chatResp = await client.PostAsync("https://api.openai.com/v1/chat/completions", chatContent);
            chatResp.EnsureSuccessStatusCode();
            var chatRespJson = await chatResp.Content.ReadAsStringAsync();
            using var chatDoc = JsonDocument.Parse(chatRespJson);
            var answer = chatDoc.RootElement.GetProperty("choices")[0].GetProperty("message").GetProperty("content").GetString() ?? string.Empty;

            var answerText = answer;

            // Lưu câu trả lời assistant
            var assistantMessage = new ChatMessage
            {
                ChatSessionId = session.Id,
                Role = "assistant",
                Content = answerText
            };
            await _messageRepo.AddAsync(assistantMessage);
            await _messageRepo.SaveChangesAsync();

            return new ChatResponseDto
            {
                SessionId = request.SessionId,
                Answer = answerText,
                Sources = sources.ToList()
            };
        }

        public async Task<IEnumerable<ChatMessageDto>> GetHistoryAsync(string sessionId)
        {
            var session = await _context.ChatSessions
                .FirstOrDefaultAsync(s => s.SessionId == sessionId);
            if (session is null) return Enumerable.Empty<ChatMessageDto>();

            return await _context.ChatMessages
                .Where(m => m.ChatSessionId == session.Id)
                .OrderBy(m => m.CreatedAt)
                .Select(m => new ChatMessageDto
                {
                    Id = m.Id,
                    Role = m.Role,
                    Content = m.Content,
                    CreatedAt = m.CreatedAt
                })
                .ToListAsync();
        }

        public async Task<IEnumerable<ChatSessionDto>> GetAllSessionsAsync()
        {
            return await _context.ChatSessions
                .Where(s => !s.IsDeleted)
                .Include(s => s.Subject)
                .Include(s => s.Messages)
                .OrderByDescending(s => s.CreatedAt)
                .Select(s => new ChatSessionDto
                {
                    Id = s.Id,
                    SessionId = s.SessionId,
                    Title = s.Title,
                    SubjectName = s.Subject != null ? s.Subject.Name : "Tất cả",
                    MessageCount = s.Messages.Count,
                    CreatedAt = s.CreatedAt
                })
                .ToListAsync();
        }

        public async Task DeleteSessionAsync(string sessionId)
        {
            var session = await _context.ChatSessions
                .FirstOrDefaultAsync(s => s.SessionId == sessionId);
            if (session is null) return;

            session.IsDeleted = true;
            session.UpdatedAt = DateTime.UtcNow;
            _sessionRepo.Update(session);
            await _sessionRepo.SaveChangesAsync();
        }

        // Helper: tính cosine similarity giữa hai vector double
        private static double CosineSimilarity(double[] a, double[] b)
        {
            if (a == null || b == null) return 0;
            var len = Math.Min(a.Length, b.Length);
            double dot = 0, na = 0, nb = 0;
            for (int i = 0; i < len; i++)
            {
                dot += a[i] * b[i];
                na += a[i] * a[i];
                nb += b[i] * b[i];
            }
            if (na == 0 || nb == 0) return 0;
            return dot / (Math.Sqrt(na) * Math.Sqrt(nb));
        }
    }
}
