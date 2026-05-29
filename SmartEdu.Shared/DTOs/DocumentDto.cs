using SmartEdu.Shared.Enums;

namespace SmartEdu.Shared.DTOs
{
    // 1. Tạo SmartEdu.Shared.DTOs.DocumentDto
    public class DocumentDto
    {
        public int Id { get; set; }
        public string Title { get; set; }
        public string FileName { get; set; }
        public string FileType { get; set; }
        public long FileSize { get; set; }
        public int SubjectId { get; set; }
        public DocumentStatus Status { get; set; }
        public DateTime CreatedAt { get; set; }
        // Có thể thêm SubjectName nếu cần hiển thị
        public string SubjectName { get; set; }
    }
}
