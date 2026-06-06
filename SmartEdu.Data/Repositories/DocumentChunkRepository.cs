using SmartEdu.Shared.Entities;

namespace SmartEdu.Data.Repositories;

public class DocumentChunkRepository : Repository<DocumentChunk>, IDocumentChunkRepository
{
    public DocumentChunkRepository(AppDbContext context) : base(context)
    {
    }
}
