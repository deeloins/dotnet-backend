using System.ComponentModel.DataAnnotations;
namespace AuthTodoApp.Models
{
    public class UserRegisterDto
    {
        public string Email { get; set; }
        public string Password { get; set; }
    }
}