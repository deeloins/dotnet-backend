using System.ComponentModel.DataAnnotations;

namespace AuthTodoApp.Models
{
    public class TodoUpdateDto
    {
        [Required(ErrorMessage = "Title is required")]
        [StringLength(100, ErrorMessage = "Title cannot be longer than 100 characters")]
        public string Title { get; set; }

        public bool IsDone { get; set; }
    }

}