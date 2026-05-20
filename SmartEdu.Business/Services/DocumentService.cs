using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using SmartEdu.Business.Interfaces;
using SmartEdu.Data.Repositories;
using SmartEdu.Shared.Entities;
using SmartEdu.Shared.Enums;
using System.Net.Http.Headers;
using System.Text.Json;
using System.Text;
using UglyToad.PdfPig;
using DocumentFormat.OpenXml.Packaging;
using EntityDocument = SmartEdu.Shared.Entities.Document;
using Microsoft.Extensions.Configuration;
using System.Globalization;
using System.Linq;
using System.Collections.Generic;

namespace SmartEdu.Business.Services;

public class DocumentService : IDocumentService
{
    private readonly IRepository<EntityDocument> _docRepo;
    private readonly IRepository<DocumentChunk> _chunkRepo;
    private readonly IWebHostEnvironment _env;
    private readonly IHttpClientFactory _httpFactory;
    private readonly IConfiguration _configuration;

    public DocumentService(
        IRepository<EntityDocument> docRepo,
        IRepository<DocumentChunk> chunkRepo,
        IWebHostEnvironment env,
        IHttpClientFactory httpFactory,
        IConfiguration configuration)
    {
        _docRepo = docRepo;
        _chunkRepo = chunkRepo;
        _env = env;
        _httpFactory = httpFactory;
        _configuration = configuration;
    }

    public async Task<IEnumerable<EntityDocument>> GetAllAsync(int? subjectId = null)
    {
        var docs = await _docRepo.GetAllWithIncludeAsync(d => d.Subject);
        if (subjectId.HasValue)
            return docs.Where(d => d.SubjectId == subjectId.Value);
        return docs;
    }

    public async Task<EntityDocument?> GetByIdAsync(int id)
        => await _docRepo.GetByIdAsync(id);

    public async Task<Document> UploadAsync(IFormFile file, string title, int subjectId)
    {
        var ext = Path.GetExtension(file.FileName).ToLower();
        if (ext is not ".pdf" and not ".docx")
            throw new InvalidOperationException("Chỉ hỗ trợ PDF và DOCX.");

        string webRootPath = _env.WebRootPath;
        if (string.IsNullOrWhiteSpace(webRootPath))
        {
            webRootPath = Path.Combine(Directory.GetCurrentDirectory(), "wwwroot");
        }

        var uploadRoot = Path.Combine(webRootPath, "uploads");

        Directory.CreateDirectory(uploadRoot);

        var savedName = $"{Guid.NewGuid()}{ext}";
        var savedPath = Path.Combine(uploadRoot, savedName);

        await using var stream = File.Create(savedPath);
        await file.CopyToAsync(stream);

        var doc = new Document
        {
            Title = title,
            FileName = file.FileName,
            FilePath = savedPath,
            FileType = ext.TrimStart('.'),
            FileSize = file.Length,
            SubjectId = subjectId,
            Status = DocumentStatus.Pending
        };

        await _docRepo.AddAsync(doc);
        await _docRepo.SaveChangesAsync();
        return doc;
    }

    public async Task DeleteAsync(int id)
    {
        var doc = await _docRepo.GetByIdAsync(id);
        if (doc is null) return;
        doc.IsDeleted = true;
        doc.UpdatedAt = DateTime.UtcNow;
        _docRepo.Update(doc);
        await _docRepo.SaveChangesAsync();
    }

    // Cập nhật: Thực hiện xử lý thực tế cho embedding bằng OpenAI
    public async Task TriggerEmbeddingAsync(int documentId)
    {
        var doc = await _docRepo.GetByIdAsync(documentId);
        if (doc is null) return;
        doc.Status = DocumentStatus.Processing;
        doc.UpdatedAt = DateTime.UtcNow;
        _docRepo.Update(doc);
        await _docRepo.SaveChangesAsync();

        try
        {
            // BƯỚC 1: Trích xuất văn bản thực từ file (hỗ trợ PDF và DOCX)
            var ext = Path.GetExtension(doc.FilePath).ToLowerInvariant();
            // ưu tiên dùng doc.FileType nếu có, fallback về ext
            var fileType = (doc.FileType ?? ext.TrimStart('.')).ToLowerInvariant();
            string rawText = string.Empty;

            if (fileType == "pdf" || ext == ".pdf")
            {
                // PDF: dùng PdfPig để trích xuất text từng trang
                using var pdf = PdfDocument.Open(doc.FilePath);
                var sb = new StringBuilder();
                foreach (var page in pdf.GetPages())
                {
                    sb.AppendLine(page.Text);
                }
                rawText = sb.ToString();
            }
            else if (fileType == "docx" || ext == ".docx")
            {
                // DOCX: dùng Open XML SDK để trích xuất text (không cần license)
                rawText = ExtractTextFromDocx(doc.FilePath);
            }
            else
            {
                throw new InvalidOperationException("Chỉ hỗ trợ trích xuất văn bản cho PDF và DOCX.");
            }

            if (string.IsNullOrWhiteSpace(rawText))
            {
                throw new InvalidOperationException("Không thể trích xuất văn bản từ file.");
            }

            // BƯỚC 2: Chunking chung (kích thước ~800 ký tự, overlap 10%)
            var chunks = ChunkText(rawText, 800, 0.1);

            // Lấy key OpenAI từ cấu hình / biến môi trường
            var hfToken = _configuration["HuggingFace:Token"];
            if (string.IsNullOrWhiteSpace(hfToken))
                throw new InvalidOperationException("Hugging Face token không được cấu hình.");

            var client = _httpFactory.CreateClient();
            client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", hfToken);
            client.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));

            // Sử dụng model multilingual-e5-base
            string modelUrl = "https://router.huggingface.co/hf-inference/models/intfloat/multilingual-e5-base/pipeline/feature-extraction";

            int idx = 0;
            foreach (var text in chunks)
            {
                // E5 yêu cầu thêm tiền tố "passage: " cho đoạn văn bản lưu trữ, "query: " cho câu hỏi
                string formattedText = $"passage: {text}";

                var payload = new { inputs = formattedText };
                var json = JsonSerializer.Serialize(payload);
                using var content = new StringContent(json, Encoding.UTF8, "application/json");

                var resp = await client.PostAsync(modelUrl, content);

                // Xử lý lỗi Cold Start (Model đang ngủ) của API miễn phí
                if (resp.StatusCode == System.Net.HttpStatusCode.ServiceUnavailable)
                {
                    throw new InvalidOperationException("Server AI đang khởi động, vui lòng đợi 20 giây và bấm nút lại!");
                }

                resp.EnsureSuccessStatusCode();
                var respJson = await resp.Content.ReadAsStringAsync();

                // API Hugging Face trả về mảng trực tiếp: [0.01, 0.02, ...] hoặc [[0.01, ...]]
                using var docJson = JsonDocument.Parse(respJson);
                var vector = new List<float>();

                var root = docJson.RootElement;
                if (root.ValueKind == JsonValueKind.Array)
                {
                    var firstElement = root[0];
                    // Xử lý trường hợp mảng 1 chiều hoặc 2 chiều
                    var vectorArray = firstElement.ValueKind == JsonValueKind.Number ? root : firstElement;

                    foreach (var el in vectorArray.EnumerateArray())
                    {
                        vector.Add(el.GetSingle());
                    }
                }

                // Lưu chunk vào DB
                var chunkEntity = new DocumentChunk
                {
                    DocumentId = documentId,
                    Content = text,
                    ChunkIndex = idx++,
                    EmbeddingJson = JsonSerializer.Serialize(vector),
                    EmbeddingModel = "multilingual-e5-base",
                    CreatedAt = DateTime.UtcNow
                };

                await _chunkRepo.AddAsync(chunkEntity);
                await Task.Delay(300);
            }

            // Lưu các chunk và cập nhật trạng thái
            await _chunkRepo.SaveChangesAsync();
            doc.Status = DocumentStatus.Ready;
            doc.UpdatedAt = DateTime.UtcNow;
            _docRepo.Update(doc);
            await _docRepo.SaveChangesAsync();
        }
        catch (Exception)
        {
            // BƯỚC DỰ PHÒNG: Nếu quá trình băm dữ liệu bị lỗi, chuyển trạng thái sang Failed
            doc.Status = DocumentStatus.Failed;
            doc.UpdatedAt = DateTime.UtcNow;
            _docRepo.Update(doc);
            await _docRepo.SaveChangesAsync();
            throw;
        }
    }

    // Helper: trích xuất text từ file .docx sử dụng Open XML SDK (không cần license)
    private static string ExtractTextFromDocx(string path)
    {
        var sb = new StringBuilder();
        using (var doc = WordprocessingDocument.Open(path, false))
        {
            var body = doc.MainDocumentPart?.Document?.Body;
            if (body == null) return string.Empty;
                foreach (var para in body.Elements<DocumentFormat.OpenXml.Wordprocessing.Paragraph>())
                {
                    sb.AppendLine(para.InnerText);
                }
        }
        return sb.ToString();
    }

    // Tách văn bản thành các chunk có overlap
    private static IEnumerable<string> ChunkText(string text, int chunkSize = 800, double overlapFraction = 0.1)
    {
        if (string.IsNullOrWhiteSpace(text)) yield break;

        int overlap = (int)Math.Round(chunkSize * overlapFraction);
        int step = Math.Max(1, chunkSize - overlap);
        int pos = 0;
        while (pos < text.Length)
        {
            int len = Math.Min(chunkSize, text.Length - pos);
            yield return text.Substring(pos, len).Trim();
            pos += step;
        }
    }
}