using Microsoft.EntityFrameworkCore;
using SmartEdu.Shared.Entities;

namespace SmartEdu.Data
{
    public class AppDbContext : DbContext
    {
        public AppDbContext(DbContextOptions<AppDbContext> options) : base(options) { }

        public DbSet<Subject> Subjects => Set<Subject>();
        public DbSet<Document> Documents => Set<Document>();
        public DbSet<DocumentChunk> DocumentChunks => Set<DocumentChunk>();
        public DbSet<ChatSession> ChatSessions => Set<ChatSession>();
        public DbSet<ChatMessage> ChatMessages => Set<ChatMessage>();
        public DbSet<BenchmarkResult> BenchmarkResults { get; set; }

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            // Soft delete filter toàn cục
            modelBuilder.Entity<Document>().HasQueryFilter(d => !d.IsDeleted);
            modelBuilder.Entity<DocumentChunk>().HasQueryFilter(c => !c.IsDeleted);

            // Index tăng tốc tìm kiếm
            modelBuilder.Entity<DocumentChunk>()
                .HasIndex(c => c.DocumentId);

            modelBuilder.Entity<ChatMessage>()
                .HasIndex(m => m.ChatSessionId);

            base.OnModelCreating(modelBuilder);
        }
    }
}
