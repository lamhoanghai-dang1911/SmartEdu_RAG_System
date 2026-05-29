using System.ComponentModel.DataAnnotations;

namespace SmartEdu.Shared.DTOs
{
    public class SubjectCreateDto
    {
        [Required(ErrorMessage = "Tên môn học không được để trống")]
        [StringLength(100, ErrorMessage = "Tên môn học không được vượt quá 100 ký tự")]
        public string Name { get; set; }

        public string? Description { get; set; }
    }
}
