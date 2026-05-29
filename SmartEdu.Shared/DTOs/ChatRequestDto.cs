namespace SmartEdu.Shared.DTOs
{
    public class ChatRequestDto
    {
        public string SessionId { get; set; } = string.Empty;
        public string Question { get; set; } = string.Empty;
        public int? SubjectId { get; set; }
        public int UserId { get; set; }
    }
}
