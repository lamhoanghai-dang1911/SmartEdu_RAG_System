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
        public DbSet<User> Users => Set<User>();
        public DbSet<StudentSubject> StudentSubjects { get; set; }

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
            modelBuilder.Entity<User>()
                .Property(u => u.Role)
                .HasConversion<string>();
            modelBuilder.Entity<StudentSubject>()
            .HasKey(ss => new { ss.StudentId, ss.SubjectId });

            modelBuilder.Entity<StudentSubject>()
                .HasOne(ss => ss.User)
                .WithMany() 
                .HasForeignKey(ss => ss.StudentId);

            modelBuilder.Entity<StudentSubject>()
                .HasOne(ss => ss.Subject)
                .WithMany() 
                .HasForeignKey(ss => ss.SubjectId);

            base.OnModelCreating(modelBuilder);
        }
    }
}
