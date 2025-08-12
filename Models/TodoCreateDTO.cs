using System.ComponentModel.DataAnnotations;
public class TodoCreateDto
{
    [Required(ErrorMessage = "Title is required")]
    [StringLength(100)]
    public string Title { get; set; }

    public bool IsDone { get; set; }
}