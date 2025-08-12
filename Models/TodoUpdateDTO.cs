using System.ComponentModel.DataAnnotations;

namespace AuthTodoApp.Models
{
    public class TodoUpdateDto
    {
        [Required]
        public string Title { get; set; }

        public bool IsDone { get; set; }
    }
}