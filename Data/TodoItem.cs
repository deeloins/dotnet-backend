namespace AuthTodoApp.Models
{
    using System.ComponentModel.DataAnnotations;

    public class TodoItem
    {
        public int Id { get; set; }
        public string? UserId { get; set; }

        [Required(ErrorMessage = "Title is required")]
        [StringLength(100, ErrorMessage = "Title length can't be more than 100 characters")]
        public string Title { get; set; }

        public bool IsDone { get; set; }
    }
}
