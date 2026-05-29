namespace SmartEdu.Shared.Entities
{
    public class StudentSubject
    {
        public int StudentId { get; set; }
        public int SubjectId { get; set; }
        public Subject Subject { get; set; }
        public User User { get; set; }
        public bool IsDeleted { get; set; } = false;
    }
}
