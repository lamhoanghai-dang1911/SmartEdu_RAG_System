namespace SmartEdu.Shared.DTOs
{
    public class ChatSessionDto
    {
        public int Id { get; set; }
        public string SessionId { get; set; } = string.Empty;
        public string? Title { get; set; }
        public int? SubjectId { get; set; }
        public string? SubjectName { get; set; }
        public int MessageCount { get; set; }
        public DateTime CreatedAt { get; set; }

    }
}
