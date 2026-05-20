using Microsoft.AspNetCore.Http;
using SmartEdu.Shared.Entities;

namespace SmartEdu.Business.Interfaces
{
    public interface IDocumentService
    {
        Task<IEnumerable<Document>> GetAllAsync(int? subjectId = null);
        Task<Document?> GetByIdAsync(int id);
        Task<Document> UploadAsync(IFormFile file, string title, int subjectId);
        Task DeleteAsync(int id);
        Task TriggerEmbeddingAsync(int documentId);
    }
}
